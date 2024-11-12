using OfficeOpenXml.Drawing.Chart;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DrawSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using System.Drawing;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSaveSteps : TardisSaveGraph
    {
        Drone Drone;
        FlightSteps Steps;


        public DroneSaveSteps(DroneDataStore data, Drone drone)
            : base(data, StepDataTabName, DroneReportTabName)
        {
            Drone = drone;
            Steps = drone.FlightSteps;
        }


        public void SetSteps(Drone drone)
        {
            Drone = drone;
            Steps = drone.FlightSteps;
        }


        // Save the summary settings about the steps
        public void SaveSummary(int row, int col)
        {
            if (Steps != null)
                Data.SetTitleAndDataListColumn(FlightStepTitle, row, col, Steps.GetSettings());
        }


        // Save smoothed and calculated "step" flight data list
        public void SaveList()
        {
            if (Data.SelectWorksheet(StepDataTabName))
                Data.ClearWorksheet();

            if (Steps == null)
                return;

            if (Steps.Steps.Count > 0)
            {
                Data.SelectOrAddWorksheet(StepDataTabName);
                int stepRow = 0;
                foreach (var step in Steps.Steps)
                    Data.SetDataListRowKeysAndValues(ref stepRow, step.Value.GetSettings());

                Data.SetNumberColumnNdp(TardisModel.PitchDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.YawDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.DeltaYawDegSetting, DegreesNdp);

                // Highlight in red any section where the TimeMs exceeds FlightConfig.MaxLegGapDurationMs. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.TimeMsSetting, stepRow, Drone.DroneConfig.MaxLegGapDurationMs);

                if (Drone.DroneConfig.GimbalDataAvail == GimbalDataEnum.ManualNo)
                    // Highlight in red any step where the PitchRad exceeds FlightConfig.MaxLegPitchDeg. This implies not part of a leg.
                    Data.AddConditionalRuleBad(TardisModel.PitchDegSetting, stepRow, Drone.DroneConfig.MaxLegStepPitchDeg);

                // Highlight in red any cells where the DeltaYaw exceeds FlightConfig.MaxLegStepDeltaYawDeg. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.DeltaYawDegSetting, stepRow, Drone.DroneConfig.MaxLegStepDeltaYawDeg);
            }
        }


        // Add a graph of the drone flight path using smoothed Steps data
        public void AddNorthingEastingPathGraph()
        {
            const string ChartName = "StepsNorthingEasting";
            const string ChartTitle = "Drone flight path (Northing / Easting)";

            (var chartWs, var lastRow) = Data.PrepareChartArea(DroneReportTabName, ChartName, StepDataTabName);
            if ((lastRow > 0) && (Steps.Steps.Count > 0))
            {
                // Make sure
                var axisLength = Math.Ceiling(Math.Max(
                    Steps.NorthingRangeM(),
                    Steps.EastingRangeM()));

                var chart = chartWs.Drawings.AddScatterChart(ChartName, eScatterChartType.XYScatter);
                Data.SetChart(chart, ChartTitle, 0.14f, 0, LargeChartRows);
                Data.SetAxises(chart, "Easting", "Northing", "0", "0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = 0;
                chart.XAxis.MaxValue = axisLength;
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = axisLength;

                Data.AddScatterSerie(chart, StepDataTabName, "Flight path", TardisModel.NorthingMSetting, TardisModel.EastingMSetting, DroneColors.InScopeDroneColor);
            }
        }


        // Add a graph of the drone & ground elevations as per smoothed Steps data
        public void AddElevationsGraph()
        {
            (var _, var lastRow) = Data.PrepareChartArea(GraphTabName, "StepsElevations", TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0) && (Steps != null))
            {
                var FirstGraphRow = 2 * StandardChartRows + 3;

                // Generate a bitmap of the DSM land overlaid with the drone path 
                var drawScope = new DroneDrawScope(Drone);
                var drawAltitudes = new DrawElevations(drawScope);
                drawAltitudes.Initialise(new Size(ChartFullWidthPixels, 300));
                var pathBitmap = drawAltitudes.CurrBitmap();

                Data.SaveBitmap(pathBitmap, "StepsElevations", FirstGraphRow, 0);

                Data.SetTitleAndDataListColumn("Metrics", FirstGraphRow + 1, ChartWidth + 1, Steps.GetSettings_Altitude(), true, 1);
            }
        }


        // Add a graph of the drone travel distance in meters per step using smoothed Steps data
        public void AddTravelDistGraph()
        {
            AddTravelDistGraph(
                3,
                "StepsTravelDist",
                "Smoothed drone travel distance (in lineal M) vs Step",
                Steps.GetSettings_Lineal());
        }


        // Add a graph of the drone speed as per smoothed Steps data 
        public void AddSpeedGraph()
        {
            AddSpeedGraph(
                4,
                "StepsSpeed",
                "Smoothed drone flight speed (in Mps) vs Step",
                Steps.GetSettings_Speed());
        }


        // Add a graph of the drone delta yaw (change of direction) using smoothed Steps data
        public void AddDeltaYawGraph()
        {
            AddDeltaYawGraph(
                5,
                "StepsDeltaYaw",
                "Smoothed drone change in direction (aka Delta Yaw) in Degrees vs Step",
                Steps.GetSettings_DeltaYaw());
        }


        // Add a pitch graph  
        public void AddPitchGraph()
        {
            AddPitchGraph(
                6,
                "StepPitch",
                "Drone Pitch (in degrees) vs Step",
                Steps.GetSettings_Pitch());
        }


        // Add a roll graph  
        public void AddRollGraph()
        {
            AddRollGraph(
                7,
                "StepRoll",
                "Drone Roll (in degrees) vs Step",
                Steps.GetSettings_Roll());
        }


        // Add a graph of whether the drone step is part of a leg or not using Step data
        public void AddLegGraph()
        {
            AddLegGraph(
                8,
                "StepsLegs",
                "Drone Step is part of a flight Leg",
                FlightStep.HasLegSetting);
        }


        public void SaveDroneReport()
        {
            if (Data.SelectWorksheet(DroneReportTabName))
                Data.ClearWorksheet();

            if (Steps == null)
                return;

            Data.SelectOrAddWorksheet(DroneReportTabName);

            Data.SetLargeTitle(DroneReportTitle);

            AddNorthingEastingPathGraph();

            MinDatumId = 0;
            // Show the same max step Id (rounded up to nearest 50) value on all the graphs x axis
            MaxDatumId = (int)(Math.Ceiling(Steps.MaxTardisId / 50.0) * 50.0);

            if (MaxDatumId > 0)
            {
                AddElevationsGraph();
                AddTravelDistGraph();
                AddSpeedGraph();
                AddDeltaYawGraph();
                AddPitchGraph();
                AddRollGraph();
                AddLegGraph();
            }
        }
    }
}