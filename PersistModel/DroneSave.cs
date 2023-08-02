using Emgu.CV;
using SkyCombDrone.DrawSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using System.Diagnostics;
using System.Drawing;

namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSave : DataStoreAccessor
    {
        Drone Drone { get; }
        DroneSaveSections Sections { get; }
        DroneSaveSteps Steps { get; }
        DroneSaveLegs Legs { get; }


        public DroneSave(DroneDataStore data, Drone drone) : base(data)
        {
            Drone = drone;
            Sections = new(data, drone);
            Steps = new(data, drone);
            Legs = new(data);
        }


        public static (string, DataPairList?) SaveDronePath(
            BaseDataStore data, Drone? drone, TardisSummaryModel? tardisModel, 
            DrawPath.BackgroundType type, int row, int col, int pixels = 600)
        {
            // Generate a bitmap of the DSM land overlaid with the drone path 
            var drawScope = (drone != null ? new DroneDrawScope(drone) : new DroneDrawScope(tardisModel));
            var drawPath = new DrawPath(drawScope, false);

            drawPath.Initialise(new Size(pixels, pixels), null, type);
            var pathBitmap = drawPath.CurrImage().ToBitmap();

            data.SaveBitmap(pathBitmap,
                (type == DrawPath.BackgroundType.DsmElevations ? "DSM" :
                    (type == DrawPath.BackgroundType.DsmElevations ? "DEM" : "SEEN" )), row, col);

            return (drawPath.Title, drawPath.Metrics);
        }


        public void SetVideoFlightSectionData(int col, string title, VideoData? video)
        {
            if (video != null)
                Data.SetTitleAndDataListColumn(title + VideoInputTitleSuffix, Chapter1TitleRow, col, video.GetSettings());

            Sections.SetSections(Drone);
            Sections.SaveSummary(col, title + FlightSectionTitleSuffix);
        }


        // Save the Drone Summary tab data. 
        // This includes settings the user can edit in the UI: RunFrom/ToS, CameraDownDeg, OnGroundAt
        public void SaveData_Summary(Bitmap? countryBitmap)
        {
            (var newDroneTab, var _ ) = Data.SelectOrAddWorksheet(DroneTabName);
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

            if (newDroneTab && Data.SelectWorksheet(GroundTabName))
            {
                // We draw DEM, DSM and Country graphs on the GROUND summary tab
                // These plots combine ground and drone data.

                if (countryBitmap != null)
                {
                    var localBitmap = (Bitmap)countryBitmap.Clone();
                    new DrawPath(null, false).DrawCountryGraphLocationCross(Drone, ref localBitmap);
                    Data.SaveBitmap(localBitmap, "Country", 2, 3, 45);
                }

                DroneSave.SaveDronePath(Data, Drone, null, DrawPath.BackgroundType.DsmElevations, 2, 7);

                DroneSave.SaveDronePath(Data, Drone, null, DrawPath.BackgroundType.DemElevations, 2, 17);
            }

            // Update the Index tab with the current date/time
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