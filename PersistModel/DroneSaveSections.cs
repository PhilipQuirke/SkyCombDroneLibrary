using OfficeOpenXml.Drawing.Chart;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using System;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSaveSections : TardisSaveGraph
    {
        Drone Drone = null;
        FlightSections Sections = null;


        public DroneSaveSections(DataStore data, Drone drone)
            : base(data, Sections1TabName, Sections2TabName)
        {
            SetSections(drone);
        }


        public void SetSections(Drone drone)
        {
            Drone = drone;
            Sections = drone.FlightSections;
        }


        // Save the summary settings about the raw sections
        public void SaveSummary(int col, string title)
        {
            if (Sections != null)
                Data.SetTitleAndDataListColumn(title, Chapter2TitleRow, col, Sections.GetSettings());
        }


        // Save raw flight "section" data list
        public void SaveList()
        {
            if (Data.SelectWorksheet(Sections1TabName))
                Data.ClearWorksheet();

            if (Sections == null)
                return;

            if (Sections.Sections.Count > 0)
            {
                Data.SelectOrAddWorksheet(Sections1TabName);

                int sectionRow = 0;
                foreach (var section in Sections.Sections)
                    Data.SetDataListRowKeysAndValues(ref sectionRow, section.Value.GetSettings());

                Data.SetNumberColumnNdp(TardisModel.PitchDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.YawDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.DeltaYawDegSetting, DegreesNdp);

                Data.SetColumnWidth(FlightSectionModel.LongitudeSetting, 12);
                Data.SetColumnWidth(FlightSectionModel.LatitudeSetting, 12);

                // Highlight in red any section where the TimeMs exceeds FlightConfig.MaxLegGapDurationMs. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.TimeMsSetting, sectionRow, Drone.Config.MaxLegGapDurationMs);

                // Highlight in red any step where the PitchRad exceeds FlightConfig.MaxLegPitchDeg. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.PitchDegSetting, sectionRow, Drone.Config.MaxLegStepPitchDeg);

                // Highlight in red any cells where the DeltaYaw exceeds FlightConfig.MaxLegStepDeltaYawDeg. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.DeltaYawDegSetting, sectionRow, Drone.Config.MaxLegStepDeltaYawDeg);

                Data.SetLastUpdateDateTime(Sections1TabName);
            }
        }


        // Add a graph of the drone flight path using (unsmoothed) Section data
        public void AddLongLatPathGraph()
        {
            const string ChartName = "SectionLongLat";
            const string ChartTitle = "Raw drone flight path (Longitude / Latitude) - axis scales differ";

            (var chartWs, var lastRow) = Data.PrepareChartArea(Sections2TabName, ChartName, Sections1TabName);
            if ((lastRow > 0) && (Sections.Sections.Count > 0))
            {
                var chart = chartWs.Drawings.AddScatterChart(ChartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, ChartTitle, 0, 1, LargeChartRows);
                DataStore.SetAxises(chart, "Long", "Lat", "0.000000", "0.000000");
                chart.Legend.Remove();
                chart.XAxis.MinValue = Sections.MinGlobalLocation.Longitude;
                chart.XAxis.MaxValue = Sections.MaxGlobalLocation.Longitude;
                chart.YAxis.MinValue = Sections.MinGlobalLocation.Latitude;
                chart.YAxis.MaxValue = Sections.MaxGlobalLocation.Latitude;

                Data.AddScatterSerie(chart, Sections1TabName, "Section", FlightSection.LatitudeSetting, FlightSection.LongitudeSetting, Colors.InScopeDroneColor);
            }
        }


        // Add a graph of the drone flight path using unsmoothed Sections data
        public void AddNorthingEastingPathGraph()
        {
            const string ChartName = "SectionsNorthingEasting";
            const string ChartTitle = "Raw drone flight path (Northing / Easting)";

            (var chartWs, var lastRow) = Data.PrepareChartArea(Sections2TabName, ChartName, Sections1TabName);
            if ((lastRow > 0) && (Sections.Sections.Count > 0))
            {
                var axisLength = Math.Ceiling(Math.Max(
                    Sections.NorthingRangeM(),
                    Sections.EastingRangeM()));

                var chart = chartWs.Drawings.AddScatterChart(ChartName, eScatterChartType.XYScatter);
                DataStore.SetChart(chart, ChartTitle, 0, 0, LargeChartRows);
                DataStore.SetAxises(chart, "Easting", "Northing", "0.0", "0.0");
                chart.Legend.Remove();
                chart.XAxis.MinValue = 0;
                chart.XAxis.MaxValue = axisLength;
                chart.YAxis.MinValue = 0;
                chart.YAxis.MaxValue = axisLength;

                Data.AddScatterSerie(chart, Sections1TabName, "Flight path", TardisModel.NorthingMSetting, TardisModel.EastingMSetting, Colors.InScopeDroneColor);
            }
        }


        // Add a graph of the drone travel distance in meters per section using unsmoothed Sections data
        public void AddTravelDistGraph()
        {
            AddTravelDistGraph(
                2,
                "SectionsTravelDist",
                "Raw drone travel distance (in lineal M) vs Section",
                Sections.GetSettings_Lineal());
        }


        // Add a graph of the drone flight speed using unsmoothed Sections data
        public void AddSpeedGraph()
        {
            AddSpeedGraph(
                3,
                "SectionsSpeed",
                "Raw drone flight travel speed (meters / second) vs section",
                Sections.GetSettings_Speed());
        }


        // Add a graph of the drone delta yaw (change of direction) using raw Sections data
        public void AddDeltaYawGraph()
        {
            AddDeltaYawGraph(
                4,
                "SectionsDeltaYaw",
                "Raw drone change in direction (aka Delta Yaw) in Degrees vs Section",
                Sections.GetSettings_DeltaYaw());
        }


        // Add a pitch graph  
        public void AddPitchGraph()
        {
            AddPitchGraph(
                5,
                "SectionPitch",
                "Drone Pitch (in degrees) vs Section",
                Sections.GetSettings_Pitch());
        }


        // Add a roll graph  
        public void AddRollGraph()
        {
            AddRollGraph(
                6,
                "SectionRoll",
                "Drone Roll (in degrees) vs Section",
                Sections.GetSettings_Roll());
        }


        public void SaveCharts()
        {
            if (Data.SelectWorksheet(Sections2TabName))
                Data.ClearWorksheet();

            if (Sections == null)
                return;

            Data.SelectOrAddWorksheet(Sections2TabName);

            MinDatumId = 0;
            MaxDatumId = Sections.Sections.Count;

            AddLongLatPathGraph();
            AddNorthingEastingPathGraph();
            AddTravelDistGraph();
            AddSpeedGraph();
            AddDeltaYawGraph();
            AddPitchGraph();
            AddRollGraph();

            Data.SetLastUpdateDateTime(Sections2TabName);
        }
    }
}