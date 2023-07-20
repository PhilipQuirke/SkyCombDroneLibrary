using SkyCombDrone.DroneLogic;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using System.Diagnostics;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSave : DataStoreAccessor
    {
        Drone Drone { get; }
        DroneSaveSections Sections { get; }
        DroneSaveSteps Steps { get; }
        DroneSaveLegs Legs { get; }


        public DroneSave(DataStore data, Drone drone) : base(data)
        {
            Drone = drone;
            Sections = new(data, drone);
            Steps = new(data, drone);
            Legs = new(data);
        }



        public void SetVideoFlightSectionData(int col, string title, VideoData video)
        {
            if (video != null)
                Data.SetTitleAndDataListColumn(title + VideoInputTitleSuffix, Chapter1TitleRow, col, video.GetSettings());

            Sections.SetSections(Drone);
            Sections.SaveSummary(col, title + FlightSectionTitleSuffix);
        }


        // Save the Drone Summary tab data. 
        // This includes settings the user can edit in the UI: RunFrom/ToS, CameraDownDeg, OnGroundAt
        public void SaveData_Summary()
        {
            Data.SelectOrAddWorksheet(DroneTabName);
            Data.ClearWorksheet();

            Data.SetTitles(DroneSummaryTitle);

            // Show User Input settings & Leg data on LHS
            Data.SetTitleAndDataListColumn(UserInputTitle, Chapter1TitleRow, LhsColOffset, Drone.Config.GetSettings());
            Data.SetTitleAndDataListColumn(LegTitle, Chapter2TitleRow, LhsColOffset, Drone.Config.GetLegSettings());

            // Show Thermal to Optical comparison data on far RHS
            if (Drone.HasInputVideo && Drone.HasDisplayVideo)
                Data.SetTitleAndDataListColumn("Thermal versus Optical:", Chapter1TitleRow, FarRhsColOffset, Drone.GetSettings());

            if (Drone.HasInputVideo)
                // Show prime input Video, Flight summary data in middle
                SetVideoFlightSectionData(MidColOffset, "Prime", Drone.InputVideo);

            if (Drone.HasDisplayVideo)
                // Show secondary display Video, Flight summary data on RHS
                SetVideoFlightSectionData(RhsColOffset, "Secondary", Drone.DisplayVideo);

            Steps.SetSteps(Drone);
            Steps.SaveSummary(Chapter2TitleRow, FarRhsColOffset);

            Data.FormatSummaryPage();

            Data.SetLastUpdateDateTime(DroneTabName);
        }


        // Save the effort data into the DroneSummaryTab
        public void SaveData_Effort()
        {
            Data.SelectOrAddWorksheet(DroneTabName);

            Data.SetTitleAndDataListColumn(EffortTitle, Chapter3TitleRow, LhsColOffset, Drone.EffortDurations.GetSettings());
        }


        // Save the Drone video, flight, ground, config meta-data to the datastore (xls)
        public void SaveData_Detail(bool full, Stopwatch? effort = null)
        {
            try
            {
                Sections.SetSections(Drone);
                Steps.SetSteps(Drone);

                if (full)
                {
                    Sections.SaveList();

                    Sections.SaveCharts();
                }

                if (full)
                    Legs.SaveList(Drone);

                // Changing OnGroundAt or CameraDownDeg changes the Step data values AltitudeM, ImageCenter, ImageSizeM
                Steps.SaveList();

                if (full)
                    Steps.SaveCharts();

                if (effort != null)
                    // Record how long it took to save the data
                    Drone.EffortDurations.SaveDataStoreMs = (int)effort.Elapsed.TotalMilliseconds;
                SaveData_Effort();

                SaveAndClose();
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("SaveDrone.SaveData_Detail", ex);
            }
        }
    }
}