// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombGround.CommonSpace;
using System.Drawing;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    // Time and relative distance in space - holds one location, time, yaw, pitch, roll & altitude. 
    public class TardisModel : ConfigBase
    {
        // TardisId. Unique identifier. Zero-based.
        // Frequently there will be a single gap in the TardisId sequence.
        // In rare cases, there are multiple steps gap in the TardisId sequence. A gap of 7 sections has been seen in real-world data.
        public int TardisId { get; set; }


        // Time (from the start of the video) that this section started
        public TimeSpan StartTime { get; set; }
        // Time between this and previous Tardis in milliseconds. May be > 1000 in rare cases
        public int TimeMs { get; set; } = UnknownValue;
        // Time in milliseconds from the start of the video.
        // Rounding errors in TimeMs means this best derived from StartTime (and NOT the sum of TimeMs)
        public int SumTimeMs { get { return (int)StartTime.TotalMilliseconds; } }


        // Drone location relative to drone encompassing box (in meters).
        // NorthingM and EastingM are normally in the range 0 to 5000 (5km).
        public DroneLocation? DroneLocnM { get; set; }
        // Lineal (straight line) distance drone travelled since previous Tardis 
        public float LinealM { get; set; } = UnknownValue;
        // Sum of lineal distance for this and all previous Tardis
        // This metric "unwinds" the drone flight path into a straight path & measures total distance travelled. 
        // Used as a graph horizontal axis (as an alternative to elapsed flight time).
        public float SumLinealM { get; set; } = UnknownValue;


        // Drone orientation - Yaw from -180 degrees to +180 degrees e.g. 36.3
        // If yaw = 0° and the camera is looking to the ground(i.e.nadir), then the top of the image points to the north.
        public float YawDeg { get; set; } = UnknownValue;
        public float YawRad { get { return DegToRad(YawDeg); } }

        // The change in direction of the drone over the previous step 
        public float DeltaYawDeg { get; set; } = UnknownValue;
        public float DeltaYawRad { get { return DegToRad(DeltaYawDeg); } }


        // Drone orientation - Pitch in degrees e.g. 2.5
        public float PitchDeg { get; set; } = UnknownValue;


        // Drone orientation - Roll e.g. 0.7
        public float RollDeg { get; set; } = UnknownValue;


        // Raw drone altitude (height) above sea level in metres e.g. 61.241 m. Not very accurate
        // Aka absolute altitude.
        public float AltitudeM { get; set; } = UnknownValue;


        // Drone camera focal length e.g. 40 to 242 in a single Lennard Sparks thermal video SRT
        // A FocalLength of 280 is same as f2.8 lens
        public float FocalLength { get; set; } = UnknownValue;
        // Drone zoom value
        public float Zoom { get; set; } = UnknownValue;


        public TardisModel(int tardisId)
        {
            TardisId = tardisId;
        }


        public TardisModel(TardisModel other)
        {
            TardisId = other.TardisId;
            StartTime = other.StartTime;
            TimeMs = other.TimeMs;
            DroneLocnM = (other.DroneLocnM != null ? other.DroneLocnM.Clone() : null);
            LinealM = other.LinealM;
            SumLinealM = other.SumLinealM;
            YawDeg = other.YawDeg;
            DeltaYawDeg = other.DeltaYawDeg;
            PitchDeg = other.PitchDeg;
            RollDeg = other.RollDeg;
            AltitudeM = other.AltitudeM;
            FocalLength = other.FocalLength;
            Zoom = other.Zoom;
        }


        // Calculates TimeMs and SumTimeMs
        public void CalculateSettings_TimeMs(TardisModel? prevTardis)
        {
            if (prevTardis == null)
                TimeMs = (int)StartTime.TotalMilliseconds;
            else
            {
                var timeDelta = StartTime - prevTardis.StartTime;
                // Duration in milliseconds. May be > 1000 in rare cases. 
                TimeMs = timeDelta.Seconds * 1000 + timeDelta.Milliseconds;
            }
        }


        // Calculate lineal distance drone travelled since last Tardis (in meters). Impacts SpeedMps value
        public void CalculateSettings_LinealM(TardisModel prevTardis)
        {
            LinealM = 0;
            SumLinealM = 0;

            if (DroneLocnM != null && prevTardis != null && prevTardis.DroneLocnM != null)
            {
                LinealM = new RelativeLocation(
                    DroneLocnM.NorthingM - prevTardis.DroneLocnM.NorthingM,
                    DroneLocnM.EastingM - prevTardis.DroneLocnM.EastingM)
                        .DiagonalM;
                Assert(LinealM < 1000, "CalculateSettings_LinealM: 1km step");
                SumLinealM = prevTardis.SumLinealM + LinealM;
            }
        }


        // Calculate the speed of this Tardis in meters per second
        public float SpeedMps
        {
            get
            {
                if (TimeMs <= 0 || LinealM <= 0)
                    return 0;

                return 1000.0f * LinealM / TimeMs;
            }
        }


        // Return the difference in Yaw between two steps (with minimal Yaw value)
        public float YawDegsDelta(TardisModel fromTardis)
        {
            if (fromTardis == null)
                return 0;

            float fromYawDegs = fromTardis.YawDeg;
            float thisYawDegs = YawDeg;

            if (fromYawDegs == UnknownValue ||
               thisYawDegs == UnknownValue)
                return 0;

            // Combining positive (+174) degrees with negative (-166) degrees
            // from a -166 to +174 degree jump between sections gives a bad (near zero) answer
            var deltaYawDeg = fromYawDegs - thisYawDegs;

            // Adding/subtracting 360 degrees to deltaYawDeg has no impact on these calculations 
            // But this reduces the absolute size of deltaYawDeg for better graphing / spotting exceptions.
            if (deltaYawDeg > 180)
                deltaYawDeg -= 360;
            else if (deltaYawDeg < -180)
                deltaYawDeg += 360;

            return deltaYawDeg;
        }


        public void CalculateSettings_DeltaYawDeg(TardisModel fromTardis)
        {
            DeltaYawDeg = YawDegsDelta(fromTardis);
        }


        // Load this object's settings from another object
        public void CopyTardis(TardisModel other)
        {
            StartTime = other.StartTime;
            TimeMs = other.TimeMs;

            if (other.DroneLocnM != null)
                DroneLocnM = new(other.DroneLocnM);
            else
                DroneLocnM = null;

            LinealM = other.LinealM;
            SumLinealM = other.SumLinealM;
            YawDeg = other.YawDeg;
            DeltaYawDeg = other.DeltaYawDeg;
            PitchDeg = other.PitchDeg;
            RollDeg = other.RollDeg;
            AltitudeM = other.AltitudeM;
            FocalLength = other.FocalLength;
            Zoom = other.Zoom;
        }


        // DirectionChevron
        // Arrow head showing direction of drone's flight
        public (PointF, PointF, PointF) DirectionChevron()
        {
            int width = 8;

            PointF bottomLeft = new(-width / 2, width / 2);
            PointF bottomRight = new(width / 2, width / 2);

            return (
                DroneLocation.RotatePoint(bottomLeft, YawRad),
                new(0, 0),
                DroneLocation.RotatePoint(bottomRight, YawRad)
            );
        }


        // One-based settings index values. Must align with GetSettings procedure below
        public const int TardisIdSetting = 1;
        public const int StartTimeSetting = 2;
        public const int TimeMsSetting = 3;
        public const int SumTimeMsSetting = 4;
        public const int NorthingMSetting = 5;
        public const int EastingMSetting = 6;
        public const int LinealMSetting = 7;
        public const int SumLinealMSetting = 8;
        public const int SpeedMpsSetting = 9;
        public const int YawDegSetting = 10;
        public const int DeltaYawDegSetting = 11;
        public const int PitchDegSetting = 12;
        public const int RollDegSetting = 13;
        public const int AltitudeMSetting = 14;
        public const int FocalLengthSetting = 15;
        public const int ZoomSetting = 16;
        public const int FirstFreeSetting = 17;


        // As save/load settings, convert UnknownValue to improve graphing. 
        public const float UnknownLinealValue = -0.1f;
        public const float UnknownDegreeValue = -200;


        // Get the object's settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public virtual DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Tardis Id", TardisId },
                { "Start Time", TimeSpanToString(StartTime) },
                { "Time Ms", TimeMs, MillisecondsNdp },
                { "Sum Time Ms", SumTimeMs, MillisecondsNdp },
                { "Northing M", DroneLocnM!=null ? DroneLocnM.NorthingM : UnknownValue , LocationNdp },
                { "Easting M", DroneLocnM!=null ? DroneLocnM.EastingM : UnknownValue , LocationNdp },
                { "Lineal CM", LinealM == UnknownValue ? UnknownLinealValue : (int)(LinealM * 100), LocationNdp }, // Improve graphing
                { "Sum Lineal M", SumLinealM == UnknownValue ? UnknownLinealValue : SumLinealM, LocationNdp }, // Improve graphing
                { "Speed Mps", SpeedMps, LocationNdp },
                { "Yaw", YawDeg == UnknownValue ? UnknownLinealValue : YawDeg, DegreesNdp },
                { "Delta Yaw", DeltaYawDeg == UnknownValue ? UnknownLinealValue : DeltaYawDeg, DegreesNdp },
                { "Pitch", PitchDeg == UnknownValue ? UnknownLinealValue : PitchDeg, DegreesNdp },
                { "Roll", RollDeg == UnknownValue ? UnknownLinealValue : RollDeg, DegreesNdp },
                { "Altitude M", AltitudeM, ElevationNdp },
                { "Focal Len", FocalLength, 2 },
                { "Zoom", Zoom, 2 },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public virtual void LoadSettings(List<string> settings)
        {
            // TardisId = settings[0]
            StartTime = StringToTimeSpan(settings[1]);
            TimeMs = StringToNonNegInt(settings[2]);
            // SumTimeMs = StringToNonNegInt(settings[3]);
            DroneLocnM = new DroneLocation(settings[4], settings[5]);
            LinealM = StringToFloat(settings[6]) / 100;
            SumLinealM = StringToFloat(settings[7]);
            // SpeedMps = settings[8]
            YawDeg = StringToFloat(settings[9]);
            DeltaYawDeg = StringToFloat(settings[10]);
            PitchDeg = StringToFloat(settings[11]);
            RollDeg = StringToFloat(settings[12]);
            AltitudeM = StringToFloat(settings[13]);
            FocalLength = StringToFloat(settings[14]);
            Zoom = StringToFloat(settings[15]);


            if (LinealM == UnknownLinealValue)
                LinealM = UnknownValue;
            if (SumLinealM == UnknownLinealValue)
                SumLinealM = UnknownValue;
            if (YawDeg == UnknownLinealValue)
                YawDeg = UnknownValue;
            if (DeltaYawDeg == UnknownLinealValue)
                DeltaYawDeg = UnknownValue;
            if (PitchDeg == UnknownLinealValue)
                PitchDeg = UnknownValue;
            if (RollDeg == UnknownLinealValue)
                RollDeg = UnknownValue;
        }
    };
}