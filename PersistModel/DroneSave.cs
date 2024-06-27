using Emgu.CV;
using Emgu.CV.Structure;
using SkyCombDrone.DrawSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundModel;
using System.Diagnostics;
using System.Drawing;
using SkyCombDrone.CommonSpace;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSave : DataStoreAccessor
    {
        Drone Drone { get; }
        DroneSaveSections Sections { get; }
        DroneSaveSteps Steps { get; }
        DroneSaveLegs Legs { get; }
        DroneSaveWayPoints WayPoints { get; }


        public DroneSave(DroneDataStore data, Drone drone) : base(data)
        {
            Drone = drone;
            Sections = new(data, drone);
            Steps = new(data, drone);
            Legs = new(data);
            WayPoints = new(data);
        }


        // Generate a bitmap of the DSM/DEM/Swathe land overlaid with the drone path 
        public static (string title, DataPairList? metrics, string bitmapName, Emgu.CV.Image<Bgr,byte> pathImage) 
            CreateDronePath( DroneDrawPath drawPath, GroundType type, int pixels )
        {
            drawPath.Initialise(new Size(pixels, pixels), null, type);

            var pathImage = drawPath.BaseImage.Clone();
            drawPath.CurrImage(ref pathImage);

            var bitmapName = "UNKNOWN";
            switch(type)
            {
                case GroundType.DsmElevations: bitmapName = "DSM"; break;
                case GroundType.DemElevations: bitmapName = "DEM"; break;
                case GroundType.SwatheSeen: bitmapName = "SWATHE"; break;
            };

            return (drawPath.Title, drawPath.Metrics, bitmapName, pathImage);
        }


        // Generate and save a bitmap of the DSM/DEM/Swathe land overlaid with the drone path 
        public void SaveDronePath(
             TardisSummaryModel? tardisModel, GroundType groundType,
             int row, int col, string title, int pixels = 700)
        {
            var titleRow = row;
            Data.SetTitle(ref titleRow, col, title);

            // Generate a bitmap of the land overlaid with the drone path 
            var drawScope = (Drone != null ? new DroneDrawScope(Drone) : new DroneDrawScope(tardisModel));
            var drawPath = new DroneDrawPath(drawScope, false);
            drawPath.BackgroundColor = DroneColors.WhiteBgr; // So we dont paint under-necessary area.

            (var _, var _, var bitmapName, var pathImage) = 
                CreateDronePath( drawPath, groundType, pixels);

            Data.SaveBitmap(pathImage.ToBitmap(), bitmapName, row, col-1);
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
            Data.SetTitleAndDataListColumn(UserInputTitle, Chapter1TitleRow, LhsColOffset, Drone.DroneConfig.GetSettings());
            Data.SetTitleAndDataListColumn(LegTitle, Chapter2TitleRow, LhsColOffset, Drone.DroneConfig.GetLegSettings());

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

                if((countryBitmap != null) && (Drone.FlightSections != null))
                {
                    var row = Chapter1TitleRow;
                    var col = 10;
                    Data.SetTitle(ref row, col, FlightLocationTitle);
                    var localBitmap = (Bitmap)countryBitmap.Clone();
                    new DroneDrawPath(new DroneDrawScope(Drone), false).DrawCountryGraphLocationCross(
                        Drone.FlightSections.MinCountryLocation, ref localBitmap);
                    Data.SaveBitmap(localBitmap, "Country", row-1, col-1, 45);
                }

                SaveDronePath(null, GroundType.DsmElevations, 21, 1, GroundModel.DsmTitle);
                SaveDronePath(null, GroundType.DemElevations, 21, 7, GroundModel.DemTitle);
                SaveDronePath(null, GroundType.SwatheSeen, 21, 17, GroundModel.SwatheTitle);
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

                    Legs.SaveList(Drone);

                    WayPoints.SaveList(Drone);
                }

                // Changing OnGroundAt or CameraDownDeg changes the Step data values ImageCenter, ImageSizeM, etc.
                Steps.SaveList();

                if (full)
                    Steps.SaveCharts();

                if (effort != null)
                    // Record how long it took to save the data
                    Drone.EffortDurations.SaveDataStoreMs = (int)effort.Elapsed.TotalMilliseconds;
                SaveData_Effort();

                Data.SelectWorksheet(DroneTabName);

                // Data.HideWorksheet(Sections1TabName);
                // Data.HideWorksheet(Sections2TabName);

                Save();
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("SaveDrone.SaveData_Detail", ex);
            }
        }
    }
}