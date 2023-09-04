// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombGround.CommonSpace;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{

    // A FlightLeg is a section of a drone flight path that is at a mostly constant altitude,
    // in a mostly constant direction for a significant duration and travels a significant distance.
    // Main use for a FlightLeg is to limit the scope of CombProcessModel processing.
    // Pitch, Roll and Speed are deliberately ignored (not considered) and may NOT be mostly constant. 
    // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md for more details.
    public abstract class FlightLegModel : TardisSummaryModel
    {
        // The minimum percentage overlap between a leg and the RunFrom/To for leg
        // to be consider in-scope for highlighting in UI
        public const int MinOverlapPercent = 20; // 20 percent


        public int LegId { get; set; } = 0;
        public string LegName { get { return LegIdToName(LegId); } }
        public string WhyLegEnded { get; set; } = "";

        // The amount to correct the altitude reported by the drone in this leg.
        // Calculated by CombLeg.CalculateSettings to best reduce the CombObject location errors.
        // ApplyFixAltitudeM impacts FlightSteps, CombFeatures & CombObjects member data.
        public float FixAltitudeM { get; set; } = 0;

        public int MinStepId { get { return MinTardisId; } }
        public int MaxStepId { get { return MaxTardisId; } }



        public FlightLegModel(List<string>? settings = null) : base("Step")
        {
            if (settings != null)
                LoadSettings(settings);
        }


        public void AssertGood(bool hasFlightSteps)
        {
            Assert(LegId > 0, "FlightLeg.AssertGood: Bad LegId");
            if (hasFlightSteps)
            {
                Assert(MinStepId >= 0, "FlightLeg.AssertGood: Bad MinStepId");
                Assert(MaxStepId >= MinStepId, "FlightLeg.AssertGood: Bad MaxStepId");
                Assert(RangeSumTimeMs > 0, "FlightLeg.AssertGood: Bad TimeMs");
            }
        }



        // One-based settings index values. Must align with GetSettings procedure below
        public const int LegIdSetting = 1;
        public const int LegNameSetting = 2;
        public const int WhyEndSetting = 3;
        public const int FixAltitudeMSetting = 4;


        // Get this object's settings as datapairs (e.g. for saving to a datastore). Must align with above index values.
        public override DataPairList GetSettings()
        {
            var answer = new DataPairList
            {
                { "Leg Id", LegId },
                { "Leg Name", LegName },
                { "Why Leg Ended", WhyLegEnded },
                { "Fix Altitude M", FixAltitudeM, HeightNdp }
            };

            answer.AddRange(base.GetSettings());

            return answer;
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public override void LoadSettings(List<string> settings)
        {
            LegId = StringToNonNegInt(settings[0]);
            // Name = settings[1];
            WhyLegEnded = settings[2];
            FixAltitudeM = StringToFloat(settings[3]);

            LoadSettingsOffset(settings, 4);
        }
    };
}
