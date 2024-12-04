using Emgu.CV.Structure;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DrawSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundModel;
using SkyCombGround.PersistModel;
using SkyCombGroundLibrary.GroundLogic;
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


        // Generate a bitmap of the DSM/DEM/Swathe land overlaid with the drone path 
        public static (string title, DataPairList? metrics, string bitmapName, Emgu.CV.Image<Bgr, byte> pathImage)
            CreateDronePath(DroneDrawPath drawPath, GroundType type, int pixels)
        {
            drawPath.Initialise(new Size(pixels, pixels), null, type);

            var pathImage = drawPath.BaseImage.Clone();
            drawPath.CurrImage(ref pathImage);

            var bitmapName = "UNKNOWN";
            switch (type)
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
             int row, int col, string title)
        {
            var titleRow = row;
            Data.SetTitle(ref titleRow, col, title);

            // Generate a bitmap of the land overlaid with the drone path 
            var drawScope = (Drone != null ? new DroneDrawScope(Drone) : new DroneDrawScope(tardisModel));
            var drawPath = new DroneDrawPath(drawScope, false);
            drawPath.BackgroundColor = DroneColors.WhiteBgr; // So we dont paint under-necessary area.

            (var _, var _, var bitmapName, var pathImage) =
                CreateDronePath(drawPath, groundType, 650);

            var theBitmap = BitmapGenerator.CreateDpiIndependentBitmap(pathImage);

            Data.SaveBitmap(theBitmap, bitmapName, row, col - 1);
        }


        public void SetVideoFlightSectionData(int col, string title, VideoData? video)
        {
            if (video != null)
                Data.SetTitleAndDataListColumn(title + VideoInputTitleSuffix, Chapter1TitleRow, col, video.GetSettings());

            Sections.SetSections(Drone);
            Sections.SaveSummary(col, title + FlightSectionTitleSuffix);
        }


        // Save the Drone Settings data 
        public void SaveDroneSettings(Bitmap? countryBitmap, bool firstSave)
        {
            Data.SelectOrAddWorksheet(DroneSettingsTabName);
            Data.ClearWorksheet();

            Data.SetLargeTitle(DroneSummaryTitle);

            // Show User Input settings & Leg data on LHS
            Data.SetTitleAndDataListColumn(UserInputTitle, Chapter1TitleRow, LhsColOffset, Drone.DroneConfig.GetSettings());
            Data.SetTitleAndDataListColumn(LegTitle, Chapter2TitleRow, LhsColOffset, Drone.DroneConfig.GetLegSettings());

            if (Drone.HasInputVideo)
                // Show prime input Video, Flight summary data in middle
                SetVideoFlightSectionData(MidColOffset, "Prime", Drone.InputVideo);

            Steps.SetSteps(Drone);
            Steps.SaveSummary(Chapter2TitleRow, RhsColOffset);

            Data.FormatSummaryPage(15, 35, 35);

            if (firstSave && Data.SelectWorksheet(GroundReportTabName))
            {
                // We draw DEM, DSM and Country graphs on the Ground Summary tab

                if ((countryBitmap != null) && (Drone.FlightSections != null))
                {
                    var row = Chapter1TitleRow;
                    var col = 10;
                    Data.SetTitle(ref row, col, FlightLocationTitle);
                    GlobalLocation globalLocation = Drone.FlightSections.GlobalCentroid;
                    if (globalLocation != null)
                    {
                        OpenStreetMap map = new();
                        (var bitmap1, var bitmap2) = OpenStreetMap.GetTwoMaps(map, globalLocation, Drone.FlightSections.GlobalRange);

                        var imageHandler = new ExcelImageHandler(Data.Worksheet);
                        imageHandler.SaveBitmap(bitmap1, "CountryLarge", row - 1, col - 1, 50);
                        imageHandler.SaveBitmap(bitmap2, "CountrySmall", row - 1, col + 4, 50);
                    }
                }

                const int graphRow = 18;
                SaveDronePath(null, GroundType.DsmElevations, graphRow, 1, GroundModel.DsmTitle);
                SaveDronePath(null, GroundType.DemElevations, graphRow, 6, GroundModel.DemTitle);
                SaveDronePath(null, GroundType.SwatheSeen, graphRow, 15, GroundModel.SwatheTitle);
            }
        }


        // Save the effort data into the DroneSummaryTab
        public void SaveData_Effort()
        {
            Data.SelectOrAddWorksheet(DroneSettingsTabName);

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

                    Legs.SaveList(Drone);
                }

                // Changing OnGroundAt or CameraDownDeg changes the Step data values ImageCenter, ImageSizeM, etc.
                Steps.SaveList();

                if (full)
                    Steps.SaveDroneReport();

                if (effort != null)
                    // Record how long it took to save the data
                    Drone.EffortDurations.SaveDataStoreMs = (int)effort.Elapsed.TotalMilliseconds;
                SaveData_Effort();

                Data.SelectWorksheet(DroneReportTabName);

                Data.HideWorksheet(SectionDataTabName);
                Data.HideWorksheet(LegDataTabName);
                Data.HideWorksheet(StepDataTabName);

                Save();
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("SaveDrone.SaveData_Detail", ex);
            }
        }


        // Show elevation legend coloured cells
        public static void SaveElevationLegend(BaseDataStore dataStore, int startRow = 4, int startCol = 6, int droneReps = 3, int otherReps = 2)
        {
            for( int i = 0; i < droneReps; i++ )
                dataStore.Worksheet.Cells[startRow++, startCol].Style.Fill.SetBackground(DroneColors.InScopeDroneColor);
            for (int i = 0; i < otherReps; i++)
                dataStore.Worksheet.Cells[startRow++, startCol].Style.Fill.SetBackground(SurfaceLowColor);
            for (int i = 0; i < otherReps; i++)
                dataStore.Worksheet.Cells[startRow++, startCol].Style.Fill.SetBackground(GroundLineColor);
        }
    }
}