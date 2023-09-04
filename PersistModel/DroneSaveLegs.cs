using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using System.Drawing;


namespace SkyCombDrone.PersistModel
{
    // Save meta-data about drone legs
    public class DroneSaveLegs : DataStoreAccessor
    {
        public DroneSaveLegs(DroneDataStore data)
            : base(data)
        {
        }


        // Save calculated "leg" flight data
        public void SaveList(Drone drone)
        {
            if (Data.SelectWorksheet(Legs1TabName))
                Data.ClearWorksheet();

            if (!drone.HasFlightLegs)
                return;

            Data.SelectOrAddWorksheet(Legs1TabName);
            int row = 0;
            foreach (var leg in drone.FlightLegs.Legs)
                Data.SetDataListRowKeysAndValues(ref row, leg.GetSettings());

            Data.SetColumnWidth(FlightLegModel.WhyEndSetting, 35);

            Data.SetColumnColor(FlightLegModel.LegIdSetting, row, Color.Blue);
            Data.SetColumnColor(FlightLegModel.LegNameSetting, row, Color.Blue);

            Data.SetLastUpdateDateTime(Legs1TabName);
        }
    }
}