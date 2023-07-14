using SkyCombGround.CommonSpace;
using System;


// Models are used in-memory and to persist/load data to/from the datastore
namespace SkyCombDrone.DroneModel
{
    // FlightStepSummaryStore summarises data related to a list of FlightSteps
    public class FlightStepSummaryModel : TardisSummaryModel
    {
        // First step of flight summarised
        public int MinStepId { get { return MinTardisId; } }
        // Last step of flight summarised
        public int MaxStepId { get { return MaxTardisId; } }


        // Average (median) speed of the drone in metres per second
        public float AvgSpeedMps { get; set; }


        // The minimum / maximum ground elevation (in meters above sealevel)
        // directly under the drone flight path
        public float MinDemM { get; set; }
        public float MaxDemM { get; set; }

        // The minimum / maximum surface (tree-top) elevation (in meters above sealevel)
        // directly under the drone flight path
        public float MinDsmM { get; set; }
        public float MaxDsmM { get; set; }


        public FlightStepSummaryModel() : base("Step")
        {
            ResetSteps();
        }


        public void ResetSteps()
        {
            ResetTardis();

            AvgSpeedMps = UnknownValue;
            MinDemM = UnknownValue;
            MaxDemM = UnknownValue;
            MinDsmM = UnknownValue;
            MaxDsmM = UnknownValue;
        }


        public void CopySteps(FlightStepSummaryModel other)
        {
            CopyTardis(other);

            AvgSpeedMps = other.AvgSpeedMps;
            MinDemM = other.MinDemM;
            MaxDemM = other.MaxDemM;
            MinDsmM = other.MinDsmM;
            MaxDsmM = other.MaxDsmM;
        }


        public void SummariseStep(FlightStepModel thisStep)
        {
            SummariseTardis(thisStep);

            (MinDemM, MaxDemM) = SummariseFloat(MinDemM, MaxDemM, thisStep.DemM);
            (MinDsmM, MaxDsmM) = SummariseFloat(MinDsmM, MaxDsmM, thisStep.DsmM);
        }


        // The summarised Steps values should not generate values outside the original envelope
        public void AssertGoodStepRevision(FlightStepSummaryModel other)
        {
            AssertGoodRevision(other);

            // Allow a 1% variation to cover rounding errors.
            Assert(AvgSpeedMps <= other.AvgSpeedMps * 1.01f, "AssertGoodStepRevision: Bad AvgSpeedMps");

            Assert(MinDemM >= other.MinDemM, "AssertGoodStepRevision: Bad MinDemM");
            Assert(MaxDemM <= other.MaxDemM, "AssertGoodStepRevision: Bad MaxDemM");

            Assert(MinDsmM >= other.MinDsmM, "AssertGoodStepRevision: Bad MinDsmM");
            Assert(MaxDsmM <= other.MaxDsmM, "AssertGoodStepRevision: Bad MaxDsmM");
        }


        // Calculate the Max/Min values to show on the vertical (altitude) axis.
        public (float, float) MinMaxVerticalAxisM()
        {
            float minAltitudeM = (float)Math.Floor(
                // Drone may start on a hillside, then flight horizontally, while land falls away.
                // So MinDemM may be lower than the drone's MinVertRaw
                Math.Min(MinDemM, MinAltitudeM));

            float maxAltitudeM = (float)Math.Ceiling(
                Math.Max(MaxDemM, MaxAltitudeM));

            return (minAltitudeM, maxAltitudeM);
        }


        // Return a string like "Ground: 42-47m, Surface: 44-49m, Drone: 42-84m" 
        public string DescribeElevation()
        {
            string answer = "";

            if (MinDemM != UnknownValue)
            {
                string minStr = MinDemM.ToString("0");
                string maxStr = MaxDemM.ToString("0");

                answer = "Ground " + minStr;

                if (minStr != maxStr)
                    answer += "-" + maxStr;

                answer += "m";

                if (MinDsmM != UnknownValue)
                {
                    string minStr2 = MinDsmM.ToString("0");
                    string maxStr2 = MaxDsmM.ToString("0");

                    answer += ", Surface " + minStr2;

                    if (minStr2 != maxStr2)
                        answer += "-" + maxStr2;

                    answer += "m";
                }
            }

            if (MinAltitudeM != UnknownValue)
            {
                if (answer != "")
                    answer += ", ";

                string minStr = MinAltitudeM.ToString("0");
                string maxStr = MaxAltitudeM.ToString("0");

                answer += "Drone " + minStr;

                if (minStr != maxStr)
                    answer += "-" + maxStr;

                answer += "m";
            }

            return answer;
        }


        // Describe the drone path lineal meters
        public string DescribeLinealM()
        {
            string answer = "";

            if (MaxSumLinealM > 0)
                answer += ", flew " + MaxSumLinealM.ToString("0") + "m";

            return answer;
        }


        // Describe the drone max/avg speed
        public string DescribeSpeed()
        {
            string answer = "Drone Speed";

            if (MaxSpeedMps != UnknownValue)
                answer += string.Format(": avg {0}m/s, max {1}m/s",
                    AvgSpeedMps.ToString("0.0"),
                    MaxSpeedMps.ToString("0.0"));

            return answer;
        }


        // Get object's settings related to altitude (e.g. for use in graph labeling)
        public override DataPairList GetSettings_Altitude()
        {
            var answer = base.GetSettings_Altitude();

            answer.Add("Max Surface M", MaxDsmM, ElevationNdp);
            answer.Add("Min Surface M", MinDsmM, ElevationNdp);
            answer.Add("Max Ground M", MaxDemM, ElevationNdp);
            answer.Add("Min Ground M", MinDemM, ElevationNdp);

            return answer;
        }


        // Get object's settings related to flight path & surface elevation (e.g. for use in graph labeling)
        public DataPairList GetSettings_FlightPath()
        {
            return new DataPairList
            {
                { "Max Surface M", MaxDsmM, ElevationNdp },
                { "Min Surface M", MinDsmM, ElevationNdp },
                { "Max Ground M", MaxDemM, ElevationNdp },
                { "Min Ground M", MinDemM, ElevationNdp },
            };
        }
    }
}
