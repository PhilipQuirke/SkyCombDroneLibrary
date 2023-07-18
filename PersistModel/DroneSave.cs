using OfficeOpenXml.ConditionalFormatting;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Table.PivotTable;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneLogic;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundSpace;
using SkyCombGround.PersistModel;
using System;
using System.Diagnostics;
using System.Drawing;
using static System.Runtime.InteropServices.JavaScript.JSType;


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


        public void SetGroundInput(int col, GroundData ground)
        {
            if (ground != null)
                Data.SetTitleAndDataListColumn(GroundInputTitle, Chapter2TitleRow, col, ground.GetSettings());
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

            // Show User Input settings, Ground & Leg data on LHS
            Data.SetTitleAndDataListColumn(UserInputTitle, Chapter1TitleRow, LhsColOffset, Drone.Config.GetSettings());
            SetGroundInput(LhsColOffset, Drone.GroundData);
            Data.SetTitleAndDataListColumn(LegTitle, Chapter3TitleRow, LhsColOffset, Drone.Config.GetLegSettings());

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

            Data.SetTitleAndDataListColumn(EffortTitle, Chapter4TitleRow, LhsColOffset, Drone.EffortDurations.GetSettings());
        }


        // Save the ground/surface elevation data
        public void SaveData_GroundData(
            GroundGrid datums, string tabName, string pivotName,
            string chartName3D, string chartTitle3D, string chartNameContour,
            Color lowColor, Color highColor, bool includeSeen)
        {
            try
            {
                if ((datums == null) || (Data == null) || (Data.Worksheet == null))
                    return;

                int lastRow = datums.Datums.Count + 1;
                if (lastRow <= 1)
                    return;

                // The 3D surface graph supports 5000 points,so we round our datums into buckets.
                int roundFactor = (datums.Datums.Count < 5000 ? 1 : (datums.Datums.Count < 25000 ? 2 : (datums.Datums.Count < 125000 ? 4 : 8)));

                (var newTab, var ws) = Data.SelectOrAddWorksheet(tabName);
                if (newTab)
                    Data.Worksheet.Cells["A2:C" + lastRow].Style.Numberformat.Format = "#,##0.0";
                int row = 0;
                foreach (var datum in datums.Datums)
                    Data.SetDataListRowKeysAndValues(ref row, datum.GetSettings(roundFactor, includeSeen));
                if (newTab)
                {

                    // Create pivot of the (unordered) elevation data to support charting
                    // https://github.com/EPPlusSoftware/EPPlus/wiki/Pivot-Tables 
                    var pivotTable = ws.PivotTables.Add(ws.Cells["G33"], ws.Cells["C1:E" + lastRow], pivotName);
                    pivotTable.DataOnRows = true;
                    pivotTable.ColumnGrandTotals = false;
                    pivotTable.RowGrandTotals = false;
                    pivotTable.ShowDrill = false;

                    var rowField = pivotTable.RowFields.Add(pivotTable.Fields["NorthRndM"]);
                    rowField.Name = "NorthM";
                    rowField.Sort = eSortType.Descending;

                    var colField = pivotTable.ColumnFields.Add(pivotTable.Fields["EastRndM"]);
                    colField.Name = "EastM";
                    colField.Sort = eSortType.Ascending;

                    // Rounding could mean multiple datums per pivot cell,so use average ElevationM
                    var dataField = pivotTable.DataFields.Add(pivotTable.Fields["ElevationM"]);
                    dataField.Function = DataFieldFunctions.Average;
                    dataField.Format = "#,##0";
                    dataField.Name = "ElevationM";


                    // Add conditional formatting to the pivot table
                    var rangeStr =
                        "H35:" +
                        GenericDataStore.GetColumnName(250) +
                        "285";
                    var rangeCells = ws.Cells[rangeStr];

                    var cfRule = rangeCells.Worksheet.ConditionalFormatting.AddThreeColorScale(rangeCells);
                    cfRule.LowValue.Color = lowColor;
                    cfRule.MiddleValue.Color = Colors.MixColors(lowColor, 0.5f, highColor);
                    cfRule.MiddleValue.Type = eExcelConditionalFormattingValueObjectType.Percentile;
                    cfRule.MiddleValue.Value = 50;
                    cfRule.HighValue.Color = highColor;

                    Data.AddConditionalFormattingToPivotTable(pivotTable);


                    (float minAltitude, float _) = Drone.FlightSteps.MinMaxVerticalAxisM();

                    // Create 3D surface graph of the elevation data pivot
                    // Refer sample https://github.com/EPPlusSoftware/EPPlus.Sample.NetFramework/blob/master/18-PivotTables/PivotTablesSample.cs 
                    var pivotChart = ws.Drawings.AddSurfaceChart(chartName3D, eSurfaceChartType.Surface, pivotTable);
                    DataStore.SetChart(pivotChart, chartTitle3D, 0, 0, 0);
                    pivotChart.SetPosition(1, 0, 6, 0);
                    pivotChart.To.Column = 6 + 3 * StandardChartCols;
                    pivotChart.To.Row = 1 + LargeChartRows;
                    pivotChart.Legend.Remove();
                    pivotChart.YAxis.MinValue = minAltitude; // Y axis is vertical and shows elevation
                    pivotChart.YAxis.Title.Text = "Elevation";
                    pivotChart.XAxis.Title.Text = "Northing";
                    pivotChart.View3D.HeightPercent = 25;
                    pivotChart.View3D.DepthPercent = 200;  // Takes values from 20 to 2000. Choose 200 by trial and error 

                    if (!pivotChart.HasLegend)
                        pivotChart.Legend.Add();
                    pivotChart.Legend.Position = eLegendPosition.Left;
                }

                Data.SetLastUpdateDateTime(tabName);
            }
            catch (Exception ex)
            {
                throw Constants.ThrowException("SaveDrone.SaveData_GroundData", ex);
            }
        }


        // Save the Ground DEM data
        public void SaveData_DemData()
        {
            if (Data.SelectWorksheet(DemTabName))
                Data.ClearWorksheet();

            if (Drone.HasGroundData)
                SaveData_GroundData(
                    Drone.GroundData.DemGrid, DemTabName, "DemPivot",
                    "DemChart3D", "Ground Elevation (aka DEM) in metres",
                    "DemChartContour", Colors.GroundLowColor, Colors.GroundHighColor, true);
        }


        // Save the Surface DSM data
        public void SaveData_DsmData()
        {
            if (Data.SelectWorksheet(DsmTabName))
                Data.ClearWorksheet();

            if (Drone.HasGroundData)
                SaveData_GroundData(
                    Drone.GroundData.DsmGrid, DsmTabName, "DsmPivot",
                    "DsmChart3D", "Surface Elevation (aka DSM aka tree-top) in metres",
                    "DsmChartContour", Colors.SurfaceLowColor, Colors.SurfaceHighColor, false);
        }


        // Save the Drone video, flight, ground, config meta-data to the datastore (xls)
        public void SaveData_Detail(bool full, Stopwatch effort = null)
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

                // The DEM data also contains the "Ground Seen" data
                // which changes when legs are selected / deselected.
                SaveData_DemData();

                if (full)
                {
                    SaveData_DsmData();

                    Legs.SaveList(Drone);
                }

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
                throw Constants.ThrowException("SaveDrone.SaveData_Detail", ex);
            }
        }
    }
}