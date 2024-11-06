using OfficeOpenXml.Drawing.Chart;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneModel;
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


        public TardisSaveGraph(DroneDataStore data, string tardisTabName, string graphTabName) : base(data)
        {
            TardisTabName = tardisTabName;
            GraphTabName = graphTabName;
        }


        // Add a graph of the drone travel distance in lineal meters per step  
        public void AddTravelDistGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                Data.SetAxises(chart, "", "Distance", "0", "0.0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Distance", TardisModel.LinealMSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);

                if (metrics != null)
                    Data.SetTitleAndDataListColumn("Metrics", chartRowOffset * StandardChartRows + 1, ChartWidth + 1, metrics, true, 1);
            }
        }


        // Add a graph of the drone speed in meters per second 
        public void AddSpeedGraph(
            int chartRowOffset,
            string chartName,
            string chartTitle,
            DataPairList metrics)
        {
            (var chartWs, var lastRow) = Data.PrepareChartArea(GraphTabName, chartName, TardisTabName);
            if ((lastRow > 0) && (MaxDatumId > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(chartName, eScatterChartType.XYScatter);
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                Data.SetAxises(chart, "", "Speed", "0", "0.0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Mps", TardisModel.SpeedMpsSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);

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
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                Data.SetAxises(chart, "", "DYaw", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "DeltaYaw", TardisModel.DeltaYawDegSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);

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
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                Data.SetAxises(chart, "", "Pitch", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Pitch", TardisModel.PitchDegSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);

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
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows, ChartWidth);
                Data.SetAxises(chart, "", "Roll", "0", "0.00");
                chart.Legend.Remove();
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Roll", TardisModel.RollDegSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);

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
                Data.SetChart(chart, chartTitle, chartRowOffset, 0, StandardChartRows / 2, ChartWidth);
                Data.SetAxises(chart, "", "Leg", "0", "0");
                chart.Legend.Remove();
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = 1;
                chart.XAxis.MinValue = MinDatumId;
                chart.XAxis.MaxValue = MaxDatumId;

                Data.AddScatterSerie(chart, TardisTabName, "Leg", hasLegSetting, TardisModel.TardisIdSetting, DroneColors.InScopeDroneColor);
            }
        }
    }
}