using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundSpace;
using System;
using System.Collections.Generic;


namespace SkyCombDrone.PersistModel
{
    // Load meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations from a datastore
    public class DroneLoad : DataStoreAccessor
    {
        Drone Drone { get; }


        public DroneLoad(DataStore data, Drone drone) : base(data)
        {
            Drone = drone;
        }


        // Load video config data from a XLS file 
        public List<string> VideoSettings(int col)
        {
            return Data.GetColumnSettingsIfAvailable(DroneTabName, VideoInputTitleSuffix, Chapter1TitleRow, col);
        }


        // Load input config data from a XLS file 
        public void UserInputSettings(DroneConfigModel flightConfig)
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneTabName, UserInputTitle, Chapter1TitleRow, LhsColOffset);
            if (settings != null)
                flightConfig.LoadSettings(settings);
        }


        // Load input leg config data from a XLS file 
        public void LegSettings(DroneConfigModel flightConfig)
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneTabName, LegTitle, Chapter3TitleRow, LhsColOffset);
            if (settings != null)
                flightConfig.LoadLegSettings(settings);
        }


        // Load flight input data from a XLS file 
        public FlightSections FlightInputSettings(int titleCol)
        {
            return new FlightSections(
            Data.GetColumnSettingsIfAvailable(DroneTabName, FlightSectionTitleSuffix, Chapter2TitleRow, titleCol));
        }


        // Load flight steps data from a XLS file 
        public List<string> FlightStepsSettings()
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneTabName, FlightStepTitle, Chapter2TitleRow, FarRhsColOffset);
            if (settings == null)
                settings = Data.GetColumnSettingsIfAvailable(DroneTabName, FlightStepTitle, Chapter2TitleRow, RhsColOffset);
            return settings;
        }


        // Load effort data from a XLS file 
        public void EffortSettings()
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneTabName, EffortTitle, Chapter4TitleRow, LhsColOffset);
            if (settings != null)
                Drone.EffortDurations.LoadSettings(settings);
        }


        // Load Ground elevation settings from a XLS file  
        public List<string> GroundSettings()
        {
            return Data.GetColumnSettingsIfAvailable(DroneTabName, GroundInputTitle, Chapter2TitleRow, LhsColOffset);
        }


        // Load all FlightSections from a XLS file 
        public void FlightSections(FlightSections flightInput)
        {
            try
            {
                if (Data.SelectWorksheet(Sections1TabName))
                {
                    int row = 2;
                    FlightSection prevSection = null;
                    var cell = Data.Worksheet.Cells[row, 1];
                    while (cell != null && cell.Value != null && cell.Value.ToString() != "")
                    {
                        var sectionIdString = cell.Value.ToString();
                        if (sectionIdString == "")
                            break;
                        var sectionId = ConfigBase.StringToNonNegInt(sectionIdString);

                        // Load the non-blank cells in this row into a FlightSection object
                        var thisSection = new FlightSection(Drone, sectionId);
                        thisSection.LoadSettings(Data.GetRowSettings(row, 1));
                        flightInput.AddSection(thisSection, prevSection);

                        row++;
                        cell = Data.Worksheet.Cells[row, 1];
                        prevSection = thisSection;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DroneLoad.FlightSections", ex);
            }
        }


        // Load all FlightSteps from a XLS file 
        public void FlightSteps(FlightSections flightSections, FlightSteps flightSteps)
        {
            int row = 2;
            try
            {
                if (Data.SelectWorksheet(Steps1TabName))
                {
                    var cell = Data.Worksheet.Cells[row, 1];
                    while (cell != null && cell.Value != null)
                    {
                        var sectionIdString = cell.Value.ToString();
                        if (sectionIdString == "")
                            break;
                        var sectionId = ConfigBase.StringToNonNegInt(sectionIdString);

                        // Each step must have a corresponding section.
                        FlightSection? section = null;
                        flightSections.Sections.TryGetValue(sectionId, out section);
                        Assert(section != null, "DroneLoad.FlightSteps: Missing section for id=" + sectionId);

                        // Load the non-blank cells in this row into a FlightStep object
                        flightSteps.AddStep(
                            new FlightStep(section, Data.GetRowSettings(row, 1)));

                        row++;
                        cell = Data.Worksheet.Cells[row, 1];
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DroneLoad.FlightSteps: Row=" + row, ex);
            }
        }


        // Load all FlightLegs from a XLS file 
        public void FlightLegs(FlightLegs flightLegs, bool hasFlightSteps)
        {
            int row = 2;
            try
            {
                if (Data.SelectWorksheet(Legs1TabName))
                {
                    var cell = Data.Worksheet.Cells[row, 1];
                    while (cell != null && cell.Value != null && cell.Value.ToString() != "")
                    {
                        // Load the non-blank cells in this row into a FlightStep object
                        var leg = new FlightLeg(Data.GetRowSettings(row, 1));
                        leg.AssertGood(hasFlightSteps);
                        flightLegs.Legs.Add(leg);

                        row++;
                        cell = Data.Worksheet.Cells[row, 1];
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DroneLoad.FlightLegs: Row=" + row, ex);
            }
        }


        // Load all Ground (DEM) of Surface (DSM) data from a XLS file 
        public void GroundDatums(GroundGrid Datums, string tabName)
        {
            int row = 2;
            try
            {
                if (Data.SelectWorksheet(tabName))
                {
                    var cell = Data.Worksheet.Cells[row, 1];
                    while (cell != null && cell.Value != null && cell.Value.ToString() != "")
                    {
                        // Load the non-blank cells in this row into a GroundDatum object
                        var data = new GroundDatum(Data.GetRowSettings(row, 1));
                        data.AssertGood();
                        Datums.Datums.Add(data);

                        row++;
                        cell = Data.Worksheet.Cells[row, 1];
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DroneLoad.GroundDatums: Row=" + row, ex);
            }
        }


        // Load all Ground (DEM) data from a XLS file 
        public void DemData(GroundGrid Datums)
        {
            GroundDatums(Datums, DemTabName);
        }


        // Load all Surface (DSM) data from a XLS file 
        public void DsmData(GroundGrid Datums)
        {
            GroundDatums(Datums, DsmTabName);
        }


        public FlightSections LoadSettings(string videoName, VideoData video, string logName, int col)
        {
            video.LoadSettings(VideoSettings(col));
            video.FileName = videoName;

            var flightSections = FlightInputSettings(col);
            flightSections.FileName = logName;
            return flightSections;
        }
    }
}