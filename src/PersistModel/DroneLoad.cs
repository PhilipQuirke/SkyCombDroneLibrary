// Copyright SkyComb Limited 2025. All rights reserved. 
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.PersistModel
{
    // Load meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations from a datastore
    public class DroneLoad : DataStoreAccessor
    {
        private Drone Drone { get; }


        public DroneLoad(DroneDataStore data, Drone drone) : base(data)
        {
            Drone = drone;
        }


        // Load video config data from a XLS file 
        public List<string> VideoSettings(int col)
        {
            return Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, VideoInputTitleSuffix, Chapter1TitleRow, col);
        }


        // Load input config data from a XLS file 
        public void UserInputSettings(DroneConfigModel flightConfig)
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, UserInputTitle, Chapter1TitleRow, LhsColOffset);
            if (settings != null)
                flightConfig.LoadSettings(settings);
        }


        // Load input leg config data from a XLS file 
        public void LegSettings(DroneConfigModel flightConfig)
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, LegTitle, Chapter2TitleRow, LhsColOffset);
            if (settings != null)
                flightConfig.LoadLegSettings(settings);
        }


        // Load flight section data
        public FlightSections FlightInputSettings(int titleCol)
        {
            return new FlightSections(
                Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, FlightSectionTitleSuffix, Chapter2TitleRow, titleCol));
        }


        // Load flight step data
        public List<string> FlightStepsSettings()
        {
            return Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, FlightStepTitle, Chapter2TitleRow, RhsColOffset);
        }


        // Load effort data from a XLS file 
        public void EffortSettings()
        {
            var settings = Data.GetColumnSettingsIfAvailable(DroneSettingsTabName, EffortTitle, Chapter3TitleRow, LhsColOffset);
            if (settings != null)
                Drone.EffortDurations.LoadSettings(settings);
        }


        // Load all FlightSections from a XLS file 
        public void FlightSections(FlightSections flightInput)
        {
            try
            {
                if (Data.SelectWorksheet(SectionDataTabName))
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

                    flightInput.SetTardisMaxKey();
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
                if (Data.SelectWorksheet(StepDataTabName))
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
                if (Data.SelectWorksheet(LegDataTabName))
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


        public FlightSections LoadSettings(string videoName, VideoData video, int col)
        {
            if (video != null)
            {
                video.LoadSettings(VideoSettings(col));
                video.FileName = videoName;
            }

            return FlightInputSettings(col);
        }
    }
}