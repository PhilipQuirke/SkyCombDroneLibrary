using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using System.Drawing;


namespace SkyCombDrone.PersistModel
{
    // Save waypoint data 
    public class DroneSaveWayPoints : DataStoreAccessor
    {
        public DroneSaveWayPoints(DroneDataStore data)
            : base(data)
        {
        }


        public void SaveList(Drone drone)
        {
            if (Data.SelectWorksheet(WayPointsTabName))
                Data.ClearWorksheet();

            if (!drone.HasWayPoints)
                return;

            Data.SelectOrAddWorksheet(WayPointsTabName);
            int row = 0;
            foreach (var point in drone.WayPoints.Points)
                Data.SetDataListRowKeysAndValues(ref row, point.GetSettings());

            Data.SetColumnWidth(WayPoint.IdSetting, 15);
            Data.SetColumnWidth(WayPoint.GlobalLocationSetting, 20);
            Data.SetColumnWidth(WayPoint.CreatedAtSetting, 15);
            Data.SetColumnWidth(WayPoint.DescriptionSetting, 35);

            Data.SetLastUpdateDateTime(LegsTabName);
        }
    }
}