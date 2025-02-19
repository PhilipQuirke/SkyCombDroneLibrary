// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundLogic;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    // Raw input data about a flight section
    // Contains all useful attributes provided in drone flight data logs e.g. when and where it took place
    public class FlightSectionModel : TardisModel
    {
        // Sometimes there is a gap in the flight log data & a Section has a duration of > 1900 Ms.
        // If flight section/step duration is too great don't try to smooth it
        public const int MaxSensibleSectionDurationMs = 500;


        public int SectionId { get { return TardisId; } }


        // Drone location in longitude and latitude
        public GlobalLocation GlobalLocation { get; set; }


        public FlightSectionModel(int sectionId) : base(sectionId)
        {
            GlobalLocation = new();
        }

        // One-based settings index values. Must align with GetSettings procedure below
        public const int LongitudeSetting = FirstFreeSetting;
        public const int LatitudeSetting = FirstFreeSetting + 1;



        // Get the object's settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public override DataPairList GetSettings()
        {
            var answer = base.GetSettings();
            answer[0].Key = "Section";

            answer.Add("Longitude", GlobalLocation.Longitude, BaseConstants.LatLongNdp);
            answer.Add("Latitude", GlobalLocation.Latitude, BaseConstants.LatLongNdp);

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            base.LoadSettings(settings);

            int i = FirstFreeSetting - 1;
            GlobalLocation.Longitude = double.Parse(settings[i++]);
            GlobalLocation.Latitude = double.Parse(settings[i++]);

            GlobalLocation.AssertNZ();
        }
    }



    // Raw input data about a flight 
    public abstract class FlightSectionsModel : TardisSummaryModel
    {
        // The file name containing the flight data
        public string FileName { get; set; } = "";


        // Minimum local (not UTC) date/time from the flight data
        public DateTime MinDateTime { get; set; } = DateTime.MinValue;
        // Maximum local (not UTC) date/time from the flight data
        public DateTime MaxDateTime { get; set; } = DateTime.MinValue;


        // The Min/MaxGlobalLocation values represent a box encompassing the locations the drone flew over.
        // Commonly the drone flight path is NOT a rectangular box with sides aligned North and East,
        // so the Min/MaxGlobalLocation box is commonly a larger area than the area the drone flew over.
        public GlobalLocation? MinGlobalLocation { get; set; } = null;
        public GlobalLocation? MaxGlobalLocation { get; set; } = null; // Value is always very similar to MinGlobalLocation


        // The Min/MaxCountryLocation values represent a box encompassing the locations the drone flew over.
        // Commonly the drone flight path is NOT a rectangular box with sides aligned North and East,
        // so the Min/MaxCountryLocation box is commonly a larger area than the area the drone flew over.
        // The location is in country coordinates. In NZ, using NZTM, example has Northing=5916626 Easting=1751330 
        public CountryLocation? MinCountryLocation
        {
            get
            {
                return (MinGlobalLocation == null ? null : NztmProjection.WgsToNztm(MinGlobalLocation));
            }
        }
        public CountryLocation? MaxCountryLocation
        {
            get
            {
                return (MaxGlobalLocation == null ? null : NztmProjection.WgsToNztm(MaxGlobalLocation));
            }
        }


        // The centre of flight in global coordinate system
        public GlobalLocation? GlobalCentroid
        {
            get
            {
                var min = MinGlobalLocation;
                var max = MaxGlobalLocation;
                if (min == null || max == null)
                    return null;
                return new GlobalLocation(
                    (min.Latitude + max.Latitude) / 2,
                    (min.Longitude + max.Longitude) / 2);
            }
        }
        // The range of flight in global coordinate system
        public GlobalLocation? GlobalRange
        {
            get
            {
                var min = MinGlobalLocation;
                var max = MaxGlobalLocation;
                if (min == null || max == null)
                    return null;
                return new GlobalLocation(
                    max.Latitude - min.Latitude,
                    max.Longitude - min.Longitude);
            }
        }


        public FlightSectionsModel(List<string>? settings = null) : base("Section")
        {
            ResetTardis();

            if (settings != null)
                LoadSettings(settings);
        }


        // Describe the drone min / max altitude (in meters above sealevel)
        public string DescribeAltitude()
        {
            string minStr = MinAltitudeM.ToString("0");
            string maxStr = MaxAltitudeM.ToString("0");

            string answer = "Drone Altitude: " + minStr;

            if (minStr != maxStr)
                answer += "-" + maxStr;

            answer += " m";

            return answer;
        }


        public string ShortFileName()
        {
            if (FileName == "")
                return "";

            var answer = FileName.Substring(FileName.LastIndexOf('\\') + 1);

            // Uppercase filename and lowercase suffix for consistency
            return
                answer.Substring(0, answer.LastIndexOf('.')).ToUpper() +
                answer.Substring(answer.LastIndexOf('.')).ToLower();
        }


        public string DescribePath
        {
            get
            {
                return string.Format("Drone Path: {0} x {1} m",
                    NorthingRangeM().ToString("0"),
                    EastingRangeM().ToString("0"));
            }
        }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        public override DataPairList GetSettings()
        {
            var answer = base.GetSettings();

            answer.Add("File Name", ShortFileName());
            answer.Add("Min Date Time", MinDateTime.ToString(DateFormat));
            answer.Add("Max Date Time", MaxDateTime.ToString(DateFormat));
            answer.Add("Min Global Location", (MinGlobalLocation != null ? MinGlobalLocation.ToString() : ""));
            answer.Add("Max Global Location", (MaxGlobalLocation != null ? MaxGlobalLocation.ToString() : ""));

            if (MinCountryLocation != null)
                answer.Add("Min Country M", MinCountryLocation.ToString());
            if (MaxCountryLocation != null)
                answer.Add("Max Country M", MaxCountryLocation.ToString());

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            int index = LoadSettingsOffset(settings);

            FileName = settings[index++];
            MinDateTime = DateTime.Parse(settings[index++]);
            MaxDateTime = DateTime.Parse(settings[index++]);
            MinGlobalLocation = new GlobalLocation(settings[index++]);
            MaxGlobalLocation = new GlobalLocation(settings[index++]);

            MinGlobalLocation.AssertNZ();
            MinGlobalLocation.AssertNZ();
        }
    }
}
