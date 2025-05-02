using OfficeOpenXml.Drawing.Chart;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DrawSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.PersistModel
{
    public class SaveDroneDrawScope(Drone drone) : DroneDrawScope(drone)
    {
        public override FlightStep? CurrRunFlightStep { get { return Drone.FlightSteps.Steps[CurrRunStepId]; } }
    };



    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSaveSteps : TardisSaveGraph
    {
        private Drone Drone;
        private FlightSteps Steps;
        private SaveDroneDrawScope DrawScope;


        public DroneSaveSteps(DroneDataStore data, Drone drone)
            : base(data, StepDataTabName, DroneReportTabName)
        {
            Drone = drone;
            Steps = drone.FlightSteps;
            DrawScope = new(Drone);
        }


        public void SetSteps(Drone drone)
        {
            Drone = drone;
            Steps = drone.FlightSteps;
            DrawScope = new(Drone);
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
                Data.SetChart(chart, ChartTitle, 0.14f);
                Data.SetAxises(chart, "Easting", "Northing", "0", "0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = 0;
                chart.XAxis.MaxValue = axisLength;
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = axisLength;

                Data.AddScatterSerie(chart, StepDataTabName, "Flight path", TardisModel.NorthingMSetting, TardisModel.EastingMSetting, DroneColors.InScopeDroneColor);
            }
        }


        private void AddGraph(int row_offset, string title, DroneDrawGraph drawer, string imageName, DataPairList? metrics, bool show_legend = false, int depth = 225)
        {
            var firstGraphRow = 9 + row_offset * 13;
            int metricsCol = ChartWidth - 3;

            drawer.Initialise(new Size(ChartFullWidthPixels, depth));
            var theBitmap = drawer.CurrBitmap(true);

            Data.SetTitle(ref firstGraphRow, 1, title);
            Data.SaveBitmap(theBitmap, imageName, firstGraphRow - 1, 0);
            if (show_legend)
                DroneSave.SaveElevationLegend(Data, firstGraphRow + 1, metricsCol - 1);
            if (metrics != null)
                Data.SetTitleAndDataListColumn("Metrics", firstGraphRow, metricsCol, metrics, true, 1);
        }


        // Add a graph of the drone & ground elevations as per Steps data
        public void AddElevationsGraph()
        {
            AddGraph(2,
                "Drone, Surface and Ground elevations",
                new DrawElevations(DrawScope),
                "StepsElevations", Steps.GetSettings_Altitude(), true);
        }


        // Add a graph of the drone speed as per Steps data 
        public void AddSpeedGraph()
        {
            AddGraph(3,
                "Drone flight speed (in Mps) vs Step",
                new DrawSpeed(DrawScope),
                "StepsSpeed", Steps.GetSettings_Speed());
        }


        // Add a graph of the drone delta yaw (change of direction) using smoothed Steps data
        public void AddDeltaYawGraph()
        {
            AddGraph(4,
                "Drone change in direction (aka Delta Yaw) in Degrees vs Step",
                new DrawDeltaYaw(DrawScope),
                "StepsDeltaYaw", Steps.GetSettings_DeltaYaw());
        }


        public void AddPitchGraph()
        {
            AddGraph(5,
                "Drone Pitch (in degrees) vs Step",
                new DrawPitch(DrawScope),
                "StepPitch", Steps.GetSettings_Pitch());
        }


        public void AddRollGraph()
        {
            AddGraph(6,
                "Drone flight speed (in Mps) vs Step",
                new DrawRoll(DrawScope),
                "StepRoll", Steps.GetSettings_Roll());
        }


        // Add a graph of whether the drone step is part of a leg or not using Step data
        public void AddLegGraph()
        {
            AddGraph(7,
                "Drone Step is part of a flight Leg",
                new DrawLeg(DrawScope),
                "StepsLegs", null, false, 100);
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
                AddSpeedGraph();
                AddDeltaYawGraph();
                AddPitchGraph();
                AddRollGraph();
                AddLegGraph();
            }
        }
    }
}