using SkyCombGround.CommonSpace;
using System;
using System.Collections.Generic;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    // Summarise a sequence of Tardis objects
    public class TardisSummaryModel : ConfigBase
    {
        // Name of type of data stored in Min/MaxTardisId
        public string TardisType { get; }
        // Minimum summarised TardisId
        public int MinTardisId { get; set; }
        // Maximum summarised TardisId
        public int MaxTardisId { get; set; }


        // Drone encompassing box size in local coordinate system - NorthingM/EastingM
        // Northing/Easting is a symmetrical local coordinate system
        // That is 1 unit Northing is the same distance as 1 unit Easting.
        // Origin is bottom left of box 
        public RelativeLocation? MinLocationM { get; set; }
        public RelativeLocation? MaxLocationM { get; set; }


        // The minimum / maximum TimeMS (aka duration) of any one flight step
        public int MinTimeMs { get; set; }
        public int MaxTimeMs { get; set; }
        // The minimum / maximum SumTimeMS.
        // If all steps/sections are included values are zero and the duration of the flight
        public int MinSumTimeMs { get; set; }
        public int MaxSumTimeMs { get; set; }
        public int RangeSumTimeMs { get { return MaxSumTimeMs - MinSumTimeMs; } }


        // The minimum / maximum distance travelled by a drone in one flight step
        public float MinLinealM { get; set; }
        public float MaxLinealM { get; set; }
        // The minimum / maximum SumLinealM
        // If all steps/sections are included values are zero and the linear distance travelled over the flight
        public float MinSumLinealM { get; set; }
        public float MaxSumLinealM { get; set; }
        // The minimum / maximum SumLinealM of the drone per step in meters, rounded for use on horizontal axises.
        public float FloorMinSumLinealM { get { return MinSumLinealM == UnknownValue ? 0 : (float)Math.Floor(MinSumLinealM); } }
        public float CeilingMaxSumLinealM { get { return MaxSumLinealM == UnknownValue ? 1 : (float)Math.Max(1, Math.Ceiling(MaxSumLinealM)); } }


        // Speed of the drone in metres per second
        public float MinSpeedMps { get; set; }
        public float MaxSpeedMps { get; set; }


        // The minimum / maximum altitude of drone above sealevel in metres. More accurate version of FlightInputList.Min/MaxAltitude
        public float MinAltitudeM { get; set; }
        public float MaxAltitudeM { get; set; }


        // The minimum delta yaw of the drone per step  
        public float MinDeltaYawDeg { get; set; }
        // The maximum delta yaw of the drone per step 
        public float MaxDeltaYawDeg { get; set; }


        // The minimum / maximum delta yaw of the drone per step in degrees, rounded for use on vertical axises.
        public float FloorMinDeltaYawDeg()
        {
            return
                MinDeltaYawDeg == UnknownValue ? UnknownValue :
                    MinDeltaYawDeg > 0 ? 0 :
                        MinDeltaYawDeg > -0.18f ? -0.2f :
                            MinDeltaYawDeg > -0.4f ? -0.5f : (float)Math.Floor(MinDeltaYawDeg);
        }
        public float CeilingMaxDeltaYawDeg()
        {
            return
                MaxDeltaYawDeg == UnknownValue ? UnknownValue :
                    MaxDeltaYawDeg < 0.18f ? 0.2f :
                        MaxDeltaYawDeg < 0.4f ? 0.5f : (float)Math.Max(1, Math.Ceiling(MaxDeltaYawDeg));
        }


        // The minimum pitch of the drone per step  
        public float MinPitchDeg { get; set; }
        // The maximum pitch of the drone per step  
        public float MaxPitchDeg { get; set; }
        // The minimum / maximum pitch of the drone per step in degrees, rounded for use on vertical axises.
        public float FloorMinPitchDeg { get { return MinPitchDeg == UnknownValue ? UnknownValue : MinPitchDeg > 0 ? 0 : (float)Math.Floor(MinPitchDeg); } }
        public float CeilingMaxPitchDeg { get { return MaxPitchDeg == UnknownValue ? UnknownValue : MaxPitchDeg < 0 ? 5 : (float)Math.Max(1, Math.Ceiling(MaxPitchDeg)); } }


        // The minimum roll of the drone per step  
        public float MinRollDeg { get; set; }
        // The maximum roll of the drone per step  
        public float MaxRollDeg { get; set; }
        // The minimum / maximum roll of the drone per step in degrees, rounded for use on vertical axises.
        public float FloorMinRollDeg { get { return MinRollDeg == UnknownValue ? UnknownValue : MinRollDeg > 0 ? 0 : (float)Math.Floor(MinRollDeg); } }
        public float CeilingMaxRollDeg { get { return MaxRollDeg == UnknownValue ? UnknownValue : MaxRollDeg < 0 ? 5 : (float)Math.Max(1, Math.Ceiling(MaxRollDeg)); } }


        public TardisSummaryModel(string tardisIdName)
        {
            TardisType = tardisIdName;
            ResetTardis();
        }


        public void ResetTardis()
        {
            MinTardisId = UnknownValue;
            MaxTardisId = UnknownValue;
            MinLocationM = null;
            MaxLocationM = null;
            MinTimeMs = UnknownValue;
            MaxTimeMs = UnknownValue;
            MinSumTimeMs = UnknownValue;
            MaxSumTimeMs = UnknownValue;
            MinLinealM = UnknownValue;
            MaxLinealM = UnknownValue;
            MinSumLinealM = UnknownValue;
            MaxSumLinealM = UnknownValue;
            MinSpeedMps = UnknownValue;
            MaxSpeedMps = UnknownValue;
            MinAltitudeM = UnknownValue;
            MaxAltitudeM = UnknownValue;
            MinDeltaYawDeg = UnknownValue;
            MaxDeltaYawDeg = UnknownValue;
            MinPitchDeg = UnknownValue;
            MaxPitchDeg = UnknownValue;
            MinRollDeg = UnknownValue;
            MaxRollDeg = UnknownValue;
        }


        public void CopyTardis(TardisSummaryModel other)
        {
            MinTardisId = other.MinTardisId;
            MaxTardisId = other.MaxTardisId;
            MinLocationM = (other.MinLocationM == null ? null : other.MinLocationM.Clone());
            MaxLocationM = (other.MaxLocationM == null ? null : other.MaxLocationM.Clone());
            MinTimeMs = other.MinTimeMs;
            MaxTimeMs = other.MaxTimeMs;
            MinSumTimeMs = other.MinSumTimeMs;
            MaxSumTimeMs = other.MaxSumTimeMs;
            MinLinealM = other.MinLinealM;
            MaxLinealM = other.MaxLinealM;
            MinSumLinealM = other.MinSumLinealM;
            MaxSumLinealM = other.MaxSumLinealM;
            MinSpeedMps = other.MinSpeedMps;
            MaxSpeedMps = other.MaxSpeedMps;
            MinAltitudeM = other.MinAltitudeM;
            MaxAltitudeM = other.MaxAltitudeM;
            MinDeltaYawDeg = other.MinDeltaYawDeg;
            MaxDeltaYawDeg = other.MaxDeltaYawDeg;
            MinPitchDeg = other.MinPitchDeg;
            MaxPitchDeg = other.MaxPitchDeg;
            MinRollDeg = other.MinRollDeg;
            MaxRollDeg = other.MaxRollDeg;
        }


        // Assert that this object is a good subset of the original summary.
        // This object must not exceed the envelope of the original summary.
        public void AssertGoodSubset(TardisSummaryModel original, bool checkTradisId = true)
        {
            if (checkTradisId)
            {
                Assert(MinTardisId >= original.MinTardisId, "AssertGoodRevision: Bad MinTardisId");
                Assert(MaxTardisId <= original.MaxTardisId, "AssertGoodRevision: Bad MaxTardisId");
            }

            // Ignore small rounding errors.
            float rounding = 0.001f;

            Assert(MinLocationM.NorthingM + rounding >= original.MinLocationM.NorthingM, "AssertGoodRevision: Bad MinLocationM.NorthingM");
            Assert(MinLocationM.EastingM + rounding >= original.MinLocationM.EastingM, "AssertGoodRevision: Bad MinLocationM.EastingM");
            Assert(MaxLocationM.NorthingM - rounding <= original.MaxLocationM.NorthingM, "AssertGoodRevision: Bad MaxLocationM.NorthingM");
            Assert(MaxLocationM.EastingM - rounding <= original.MaxLocationM.EastingM, "AssertGoodRevision: Bad MaxLocationM.EastingM");

            // We dont check MinVertRaw as OnGroundAt can cause values outside the original envelope
            // We dont check MaxVertRaw as OnGroundAt can cause values outside the original envelope

            Assert(MinPitchDeg == UnknownValue || MinPitchDeg + rounding >= original.MinPitchDeg, "AssertGoodRevision: Bad MinPitchDeg");
            Assert(MaxPitchDeg == UnknownValue || MaxPitchDeg - rounding <= original.MaxPitchDeg, "AssertGoodRevision: Bad MaxPitchDeg");

            Assert(MinRollDeg == UnknownValue || MinRollDeg + rounding >= original.MinRollDeg, "AssertGoodRevision: Bad MinRollDeg");
            Assert(MaxRollDeg == UnknownValue || MaxRollDeg - rounding <= original.MaxRollDeg, "AssertGoodRevision: Bad MaxRollDeg");
        }


        // Assert that this object is a good revision of the original summary.
        // Used to compare a revised (e.g. smoothed) copy of data with the original.
        // This object must not exceed the envelope of the original summary.
        public void AssertGoodRevision(TardisSummaryModel original)
        {
            // A revision must cover the same Tardis objects and time range
            Assert(MinTardisId == original.MinTardisId, "AssertGoodRevision: Bad MinTardisId");
            Assert(MaxTardisId == original.MaxTardisId, "AssertGoodRevision: Bad MaxTardisId");
            Assert(MinTimeMs == original.MinTimeMs, "AssertGoodRevision: Bad MinTimeMs");
            Assert(MaxTimeMs == original.MaxTimeMs, "AssertGoodRevision: Bad MaxTimeMs");

            AssertGoodSubset(original);

            Assert(MinSumLinealM >= original.MinSumLinealM, "AssertGoodRevision: Bad MinSumLinealM");
            Assert(MaxSumLinealM <= original.MaxSumLinealM, "AssertGoodRevision: Bad MaxSumLinealM");

            Assert(MinSpeedMps >= original.MinSpeedMps, "AssertGoodRevision: Bad MinSpeedMps");
            Assert(MaxSpeedMps <= original.MaxSpeedMps, "AssertGoodRevision: Bad MaxSpeedMps");

            Assert(MinDeltaYawDeg >= original.MinDeltaYawDeg, "AssertGoodRevision: Bad MinDeltaYawDeg");
            Assert(MaxDeltaYawDeg <= original.MaxDeltaYawDeg, "AssertGoodRevision: Bad MaxDeltaYawDeg");
        }


        // Update the min and max values with the new "value" is appropriate.
        public static (float min, float max) SummariseFloat(float min, float max, float value)
        {
            if (value != UnknownValue)
            {
                if (max == UnknownValue)
                {
                    min = value;
                    max = value;
                }
                else
                {
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }
            }
            return (min, max);
        }
        public static (int min, int max) SummariseInt(int min, int max, int value)
        {
            if (value != UnknownValue)
            {
                if (max == UnknownValue)
                {
                    min = value;
                    max = value;
                }
                else
                {
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }
            }
            return (min, max);
        }


        public void SummariseTardis(TardisModel tardis)
        {
            (MinTardisId, MaxTardisId) = SummariseInt(MinTardisId, MaxTardisId, tardis.TardisId);

            if (tardis.LocationM != null)
            {
                if (MinLocationM == null)
                {
                    MinLocationM = new RelativeLocation(tardis.LocationM);
                    MaxLocationM = new RelativeLocation(tardis.LocationM);
                }
                else
                {
                    MinLocationM.NorthingM = Math.Min(MinLocationM.NorthingM, tardis.LocationM.NorthingM);
                    MinLocationM.EastingM = Math.Min(MinLocationM.EastingM, tardis.LocationM.EastingM);
                    MaxLocationM.NorthingM = Math.Max(MaxLocationM.NorthingM, tardis.LocationM.NorthingM);
                    MaxLocationM.EastingM = Math.Max(MaxLocationM.EastingM, tardis.LocationM.EastingM);
                }
            }

            (MinTimeMs, MaxTimeMs) = SummariseInt(MinTimeMs, MaxTimeMs, tardis.TimeMs);
            (MinSumTimeMs, MaxSumTimeMs) = SummariseInt(MinSumTimeMs, MaxSumTimeMs, tardis.SumTimeMs);

            (MinLinealM, MaxLinealM) = SummariseFloat(MinLinealM, MaxLinealM, tardis.LinealM);
            (MinSumLinealM, MaxSumLinealM) = SummariseFloat(MinSumLinealM, MaxSumLinealM, tardis.SumLinealM);

            (MinSpeedMps, MaxSpeedMps) = SummariseFloat(MinSpeedMps, MaxSpeedMps, tardis.SpeedMps());

            (MinAltitudeM, MaxAltitudeM) = SummariseFloat(MinAltitudeM, MaxAltitudeM, tardis.AltitudeM);

            (MinDeltaYawDeg, MaxDeltaYawDeg) = SummariseFloat(MinDeltaYawDeg, MaxDeltaYawDeg, tardis.DeltaYawDeg);

            (MinPitchDeg, MaxPitchDeg) = SummariseFloat(MinPitchDeg, MaxPitchDeg, tardis.PitchDeg);

            (MinRollDeg, MaxRollDeg) = SummariseFloat(MinRollDeg, MaxRollDeg, tardis.RollDeg);
        }


        // Return a string like "Drone Delta Yaw: -0.2 to +6.4 degrees" 
        public string DescribeDeltaYaw(DroneConfigModel config)
        {
            string answer = config.PitchYawRollPrefix + " Delta Yaw";

            if (MinDeltaYawDeg != UnknownValue)
            {
                var format = "0.0";
                if (Math.Abs(MinDeltaYawDeg) < 1 &&
                    Math.Abs(MaxDeltaYawDeg) < 1)
                    format = "0.00";

                answer += string.Format(": {0} to {1} degrees.",
                    MinDeltaYawDeg.ToString(format),
                    MaxDeltaYawDeg.ToString(format));
            }

            return answer;
        }


        // Return a string like "Drone Pitch: -0.2 to +1.4 degrees" 
        public string DescribePitch(DroneConfigModel config)
        {
            string answer = config.PitchYawRollPrefix + " Pitch";

            if (MinPitchDeg != UnknownValue)
                answer += string.Format(": {0} to {1} degrees.",
                    MinPitchDeg.ToString("0.0"),
                    MaxPitchDeg.ToString("0.0"));

            return answer;
        }


        // Return a string like "Drone Roll: -0.2 to +1.4 degrees" 
        public string DescribeRoll(DroneConfigModel config)
        {
            string answer = config.PitchYawRollPrefix + " Roll";

            if (MinRollDeg != UnknownValue)
                answer += string.Format(": {0} to {1} degrees.",
                    MinRollDeg.ToString("0.0"),
                    MaxRollDeg.ToString("0.0"));

            return answer;
        }


        public float NorthingRangeM()
        {
            if (MinLocationM == null || MaxLocationM == null || MaxLocationM.NorthingM == UnknownValue)
                return UnknownValue;

            return MaxLocationM.NorthingM - MinLocationM.NorthingM;
        }
        public float EastingRangeM()
        {
            if (MinLocationM == null || MaxLocationM == null || MaxLocationM.EastingM == UnknownValue)
                return UnknownValue;

            return MaxLocationM.EastingM - MinLocationM.EastingM;
        }
        public float AreaM2()
        {
            if (MinLocationM == null || MaxLocationM == null || MaxLocationM.NorthingM == UnknownValue)
                return UnknownValue;

            return (MaxLocationM.NorthingM - MinLocationM.NorthingM) * (MaxLocationM.EastingM - MinLocationM.EastingM);
        }

        public void AssertGood_SizeM()
        {
            Assert(NorthingRangeM() >= 0, "TardisSummary.AssertGood: Negative NorthingM");
            Assert(EastingRangeM() >= 0, "TardisSummary.AssertGood: Negative EastingM");
            Assert(AreaM2() >= 0, "TardisSummary.AssertGood: Negative AreaM2");
        }


        // Get object's settings related to distance travelled (e.g. for use in graph labeling)
        public DataPairList GetSettings_Lineal()
        {
            return new DataPairList
            {
                { "Max Horiz M", MaxLinealM, LocationNdp },
                { "Min Horiz M", MinLinealM, LocationNdp },
                { "Max Sum Lineal M", MaxSumLinealM, LocationNdp },
                { "Min Sum Lineal M", MinSumLinealM, LocationNdp },
                { "Range Lineal M", MaxSumLinealM - MinSumLinealM, LocationNdp },
            };
        }


        // Get object's settings related to speed (e.g. for use in graph labeling)
        public DataPairList GetSettings_Speed()
        {
            return new DataPairList
            {
                { "Max Time Ms", MaxTimeMs },
                { "Min Time Ms", MinTimeMs },
                { "Max Sum Time Ms", MaxSumTimeMs },
                { "Min Sum Time Ms", MinSumTimeMs },
                { "Range Sum Time Ms", MaxSumTimeMs - MinSumTimeMs },
                { "Max Speed Mps", MaxSpeedMps, LocationNdp  },
                { "Min Speed Mps", MinSpeedMps, LocationNdp  },
            };
        }


        // Get object's settings related to altitude (e.g. for use in graph labeling)
        public virtual DataPairList GetSettings_Altitude()
        {
            return new DataPairList
            {
                { "Max Drone Alt M", MaxAltitudeM, HeightNdp },
                { "Min Drone Alt M", MinAltitudeM, HeightNdp },
                { "Range Drone Alt M", MaxAltitudeM - MinAltitudeM, HeightNdp },
            };
        }


        // Get object's settings related to delta yaw (e.g. for use in graph labeling)

        public DataPairList GetSettings_DeltaYaw()
        {
            return new DataPairList
            {
                { "Max Delta Yaw Deg", MaxDeltaYawDeg, DegreesNdp },
                { "Min Delta Yaw Deg", MinDeltaYawDeg, DegreesNdp },
                { "Range Delta Yaw Deg", MaxDeltaYawDeg - MinDeltaYawDeg, DegreesNdp },
            };
        }


        // Get object's settings related to pitch (e.g. for use in graph labeling)

        public DataPairList GetSettings_Pitch()
        {
            return new DataPairList
            {
                { "Max Pitch Deg", MaxPitchDeg, DegreesNdp },
                { "Min Pitch Deg", MinPitchDeg, DegreesNdp },
                { "Range Pitch Deg", MaxPitchDeg - MinPitchDeg, DegreesNdp },
            };
        }


        // Get object's settings related to roll (e.g. for use in graph labeling)

        public DataPairList GetSettings_Roll()
        {
            return new DataPairList
            {
                { "Max Roll Deg", MaxRollDeg, DegreesNdp },
                { "Min Roll Deg", MinRollDeg, DegreesNdp },
                { "Range Roll Deg", MaxRollDeg - MinRollDeg, DegreesNdp },
            };
        }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        public virtual DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Min " + TardisType + " Id", MinTardisId },
                { "Max " + TardisType + " Id", MaxTardisId },
                { "Min Locn M", MinLocationM==null ? UnknownString : MinLocationM.ToString() },
                { "Max Locn M", MaxLocationM==null ? UnknownString : MaxLocationM.ToString() },
                { "Area M2", AreaM2(), LocationNdp  },
                { "Min Time Ms", MinTimeMs },
                { "Max Time Ms", MaxTimeMs },
                { "Min Sum Time Ms", MinSumTimeMs },
                { "Max Sum Time Ms", MaxSumTimeMs },
                { "Min Lineal M", MinLinealM, LocationNdp },
                { "Max Lineal M", MaxLinealM, LocationNdp },
                { "Min Sum Lineal M", MinSumLinealM, LocationNdp },
                { "Max Sum Lineal M", MaxSumLinealM, LocationNdp },
                { "Min Speed Mps", MinSpeedMps, LocationNdp  },
                { "Max Speed Mps", MaxSpeedMps, LocationNdp  },
                { "Min Altitude M", MinAltitudeM, HeightNdp },
                { "Max Altitude M", MaxAltitudeM, HeightNdp },
                { "Min Delta Yaw Deg", MinDeltaYawDeg, DegreesNdp },
                { "Max Delta Yaw Deg", MaxDeltaYawDeg, DegreesNdp },
                { "Min Pitch Deg", MinPitchDeg, DegreesNdp },
                { "Max Pitch Deg", MaxPitchDeg, DegreesNdp },
                { "Min Roll Deg", MinRollDeg, DegreesNdp },
                { "Max Roll Deg", MaxRollDeg, DegreesNdp },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public void LoadSettingsOffset(List<string> settings, int offset = 0)
        {
            MinTardisId = StringToInt(settings[offset++]);
            MaxTardisId = StringToInt(settings[offset++]);
            MinLocationM = (settings[offset] == UnknownString ? null : new RelativeLocation(settings[offset])); offset++;
            MaxLocationM = (settings[offset] == UnknownString ? null : new RelativeLocation(settings[offset])); offset++;
            offset++;// AreaM2 = settings[offset++];
            MinTimeMs = StringToInt(settings[offset++]);
            MaxTimeMs = StringToInt(settings[offset++]);
            MinSumTimeMs = StringToInt(settings[offset++]);
            MaxSumTimeMs = StringToInt(settings[offset++]);
            MinLinealM = StringToFloat(settings[offset++]);
            MaxLinealM = StringToFloat(settings[offset++]);
            MinSumLinealM = StringToFloat(settings[offset++]);
            MaxSumLinealM = StringToFloat(settings[offset++]);
            MinSpeedMps = StringToFloat(settings[offset++]);
            MaxSpeedMps = StringToFloat(settings[offset++]);
            MinAltitudeM = StringToFloat(settings[offset++]);
            MaxAltitudeM = StringToFloat(settings[offset++]);
            MinDeltaYawDeg = StringToFloat(settings[offset++]);
            MaxDeltaYawDeg = StringToFloat(settings[offset++]);
            MinPitchDeg = StringToFloat(settings[offset++]);
            MaxPitchDeg = StringToFloat(settings[offset++]);
            MinRollDeg = StringToFloat(settings[offset++]);
            MaxRollDeg = StringToFloat(settings[offset++]);
        }


        public virtual void LoadSettings(List<string> settings)
        {
            LoadSettingsOffset(settings);
        }
    }
}