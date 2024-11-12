using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations to a datastore, including graphs
    public class DroneSaveSections : TardisSaveGraph
    {
        Drone Drone;
        FlightSections Sections;


        public DroneSaveSections(DroneDataStore data, Drone drone)
            : base(data, SectionDataTabName, "")
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
            if (Data.SelectWorksheet(SectionDataTabName))
                Data.ClearWorksheet();

            if (Sections == null)
                return;

            if (Sections.Sections.Count > 0)
            {
                Data.SelectOrAddWorksheet(SectionDataTabName);

                int sectionRow = 0;
                foreach (var section in Sections.Sections)
                    Data.SetDataListRowKeysAndValues(ref sectionRow, section.Value.GetSettings());

                Data.SetNumberColumnNdp(TardisModel.PitchDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.YawDegSetting, DegreesNdp);
                Data.SetNumberColumnNdp(TardisModel.DeltaYawDegSetting, DegreesNdp);

                Data.SetColumnWidth(FlightSectionModel.LongitudeSetting, 12);
                Data.SetColumnWidth(FlightSectionModel.LatitudeSetting, 12);

                // Highlight in red any section where the TimeMs exceeds FlightConfig.MaxLegGapDurationMs. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.TimeMsSetting, sectionRow, Drone.DroneConfig.MaxLegGapDurationMs);

                if (Drone.DroneConfig.GimbalDataAvail == GimbalDataEnum.ManualNo)
                    // Highlight in red any step where the PitchRad exceeds FlightConfig.MaxLegPitchDeg. This implies not part of a leg.
                    Data.AddConditionalRuleBad(TardisModel.PitchDegSetting, sectionRow, Drone.DroneConfig.MaxLegStepPitchDeg);

                // Highlight in red any cells where the DeltaYaw exceeds FlightConfig.MaxLegStepDeltaYawDeg. This implies not part of a leg.
                Data.AddConditionalRuleBad(TardisModel.DeltaYawDegSetting, sectionRow, Drone.DroneConfig.MaxLegStepDeltaYawDeg);
            }
        }
    }
}