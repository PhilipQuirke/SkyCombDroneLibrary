using OfficeOpenXml.Drawing.Chart;
using SkyCombDrone.DroneModel;
using SkyCombDrone.CommonSpace;
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.PersistModel
{
    // Save graphs about a sequence of tardis datums
    public class TardisSaveGraph : DataStoreAccessor
    {
        public readonly int ChartWidth = 2 * StandardChartCols;


        public string TardisTabName;
        public string GraphTabName;


        protected int MinDatumId = 0;
        protected int MaxDatumId = 1;


        public TardisSaveGraph(DataStore data, string tardisTabName, string graphTabName) : base(data)
        {
            TardisTabName = tardisTabName;
            GraphTabName = graphTabName;
        }



        // Add a graph of the drone & ground elevations  
        public void AddElevationsGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            FlightStepSummaryModel summary,
            int theDsmSetting,
            int theDemSetting)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0) && (summary != null))
            {
                (float minAltitude, float maxAltitude) = summary.MinMaxVerticalAxisM();

                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "", "0", "0.0");
                chart.Legend.Position = eLegendPosition.Left;
                chart.YAxis.MinValue = minAltitude;
                chart.YAxis.MaxValue = maxAltitude;
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Altitude", TardisModel.AltitudeMSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor, 2);
                Data.AddScatterSerie(chart, TardisTabName, "DSM", theDsmSetting, TardisModel.TardisIdSetting, Colors.SurfaceLineColor, 2);
                Data.AddScatterSerie(chart, TardisTabName, "DEM", theDemSetting, TardisModel.TardisIdSetting, Colors.GroundLineColor, 2);

                Data.SetTitleAndDataListColumn("Metrics", 2 * StandardChartRows + 1, ChartWidth + 1, summary.GetSettings_Altitude(), true, 1);
            }
        }


        // Add a graph of the drone travel distance in lineal meters per step  
        public void AddTravelDistGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics = null)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "Distance", "0", "0.0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Distance", TardisModel.LinealMSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of the drone speed in meters per second 
        public void AddSpeedGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics = null)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "Speed", "0", "0.0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Mps", TardisModel.SpeedMpsSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of the drone delta yaw (change of direction) per step
        public void AddDeltaYawGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics = null)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "DYaw", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "DeltaYaw", TardisModel.DeltaYawDegSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of the drone pitch (up and done) per step
        public void AddPitchGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics = null)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "Pitch", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Pitch", TardisModel.PitchDegSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of the drone roll per step
        public void AddRollGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics = null)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                DataStore.SetAxises(chart, "", "Roll", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Roll", TardisModel.RollDegSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of whether the drone step is part of a leg or not       
        public void AddLegGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            int hasLegSetting)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows / 2, ChartWidth);
                DataStore.SetAxises(chart, "", "Leg", "0", "0");
                chart.Legend.Remove();
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = 1;
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Leg", hasLegSetting, TardisModel.TardisIdSetting, Colors.InScopeDroneColor);
            }
        }
    }
}