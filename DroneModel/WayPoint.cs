// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombDrone.DroneLogic;
using SkyCombGround.CommonSpace;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{

    // A WayPoint is a point specified by the user manually during a drone flight.
    // It signifies something of interest to the drone operator e.g. a possum
    public class WayPoint : ConfigBase
    {
        // Unique Id for this WayPoint
        public int WayPointId { get; set; } = 0;

        // Location of the waypoint in the global coordinate system
        public GlobalLocation GlobalLocation { get; set; } = new GlobalLocation();

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string Description { get; set; } = "";


        public WayPoint(List<string>? settings = null)
        {
            if (settings != null)
                LoadSettings(settings);
        }


        // One-based settings index values. Must align with GetSettings procedure below
        public const int IdSetting = 1;
        public const int GlobalLocationSetting = 2;
        public const int CreatedAtSetting = 3;
        public const int DescriptionSetting = 4;


        // Get this object's settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "WayPointId", WayPointId },
                { "GlobalLocation", GlobalLocation.ToString() },
                { "CreatedAt", CreatedAt.ToString(ShortDateFormat)  },
                { "Description", Description },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            WayPointId = StringToNonNegInt(settings[0]);
            GlobalLocation = new GlobalLocation(settings[1].ToString());
            CreatedAt = DateTime.Parse(settings[2].ToString());
            if (settings.Count >= DescriptionSetting)
                Description = settings[DescriptionSetting - 1];
        }
    }
}
