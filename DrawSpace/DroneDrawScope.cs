// Copyright SkyComb Limited 2023. All rights reserved.
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.DrawSpace
{
    // Code to draw images related to drone & process data in charts, graphs,etc.
    public class TardisDrawScope : BaseConstants
    {
        // The tardis summary (if any) to draw
        public TardisSummaryModel? TardisSummary = null;


        // First millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int FirstDrawMs { get { return 0; } }
        // Last millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int LastDrawMs { get { return TardisSummary == null ? 0 : TardisSummary.MaxTimeMs; } }


        // First step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int FirstDrawStepId { get { return 0; } }
        // Last step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int LastDrawStepId { get { return TardisSummary == null ? 0 : TardisSummary.MaxTardisId; } }


        // First step of flight data to run. A StepId axis approximates a time axis.
        public virtual int FirstRunStepId { get { return 0; } }
        // Last step of flight data to run. A StepId axis approximates a time axis.
        public virtual int LastRunStepId { get { return TardisSummary == null ? 0 : TardisSummary.MaxTardisId; } }


        public virtual FlightStep? CurrRunFlightStep { get { return null; } }
        public virtual int CurrRunStepId { get { return 1; } }


        public virtual float FloorMinSumLinealM { get { return TardisSummary == null ? 0 : TardisSummary.FloorMinSumLinealM; } }
        public virtual float CeilingMaxSumLinealM { get { return TardisSummary == null ? 0 : TardisSummary.CeilingMaxSumLinealM; } }
        public virtual string DescribePath { get { return ""; } }
        public virtual DataPairList GetSettings_FlightPath { get { return null; } }


        public virtual float FloorMinPitchDeg { get { return TardisSummary == null ? 0 : TardisSummary.FloorMinPitchDeg; } }
        public virtual float CeilingMaxPitchDeg { get { return TardisSummary == null ? 0 : TardisSummary.CeilingMaxPitchDeg; } }
        public virtual string DescribePitch { get { return ""; } }
        public virtual DataPairList GetSettings_Pitch { get { return TardisSummary == null ? null : TardisSummary.GetSettings_Pitch(); } }


        public virtual float FloorMinDeltaYawDeg { get { return TardisSummary == null ? 0 : TardisSummary.FloorMinDeltaYawDeg; } }
        public virtual float CeilingMaxDeltaYawDeg { get { return TardisSummary == null ? 0 : TardisSummary.CeilingMaxDeltaYawDeg; } }
        public virtual string DescribeDeltaYaw { get { return ""; } }
        public virtual DataPairList GetSettings_DeltaYaw { get { return TardisSummary == null ? null : TardisSummary.GetSettings_DeltaYaw(); } }


        public virtual float FloorMinRollDeg { get { return TardisSummary == null ? 0 : TardisSummary.FloorMinRollDeg; } }
        public virtual float CeilingMaxRollDeg { get { return TardisSummary == null ? 0 : TardisSummary.CeilingMaxRollDeg; } }
        public virtual string DescribeRoll { get { return ""; } }
        public virtual DataPairList GetSettings_Roll { get { return TardisSummary == null ? null : TardisSummary.GetSettings_Roll(); } }


        public virtual (float, float) MinMaxVerticalAxisM { get { return (0, 0); } }
        public virtual string DescribeElevation { get { return ""; } }
        public virtual DataPairList GetSettings_Altitude { get { return TardisSummary == null ? null : TardisSummary.GetSettings_Altitude(); } }


        public virtual string DescribeSpeed { get { return ""; } }
        public virtual DataPairList GetSettings_Speed { get { return TardisSummary == null ? null : TardisSummary.GetSettings_Speed(); } }
        public virtual float MaxSpeedMps { get { return TardisSummary == null ? 0 : TardisSummary.MaxSpeedMps; } }


        // The ObjectForm only wants to draw features up to a certain BlockId
        public int MaxFeatureBlockIdToDraw = 999999;


        public TardisDrawScope(TardisSummaryModel? tardisSummary)
        {
            TardisSummary = tardisSummary;
        }


        // Are we drawing the full flight? Or just a subset of it?
        public bool DrawFullFlight()
        {
            return
                (TardisSummary != null) &&
                (FirstDrawStepId == 0) &&
                (LastDrawStepId == TardisSummary.MaxTardisId);
        }


        // Sometimes the CurrRunFlightStep can go one step outside of range
        // and the "out of range" step may have "out of range" attribute values,
        // triggering the Asserts in DroneDatumToHeight.
        public virtual bool CurrRunFlightStepValid()
        {
            return true;
        }


        // Always draw the first/last frames, first/last frames processed
        public virtual bool DrawStepId(int thisStepId)
        {
            return
                thisStepId == FirstDrawStepId ||
                thisStepId == LastDrawStepId;
        }


        public bool RunStepIdInScope(int firstStepId, int lastStepId)
        {
            return
                (FirstRunStepId != UnknownValue) &&
                (LastRunStepId != UnknownValue) &&
                (firstStepId >= FirstRunStepId) &&
                (lastStepId <= LastRunStepId);
        }
        public bool RunStepIdInScope(int thisStepId)
        {
            return RunStepIdInScope(thisStepId, thisStepId);    
        }
    }


    // Code to draw images related to drone & process data in charts, graphs,etc.
    public class DroneDrawScope : TardisDrawScope
    {
        // The drone data (if any) to draw
        public Drone Drone;


        public override string DescribePath { get { return ( Drone == null ? "" : Drone.DescribeFlightPath ); } }

        public override string DescribePitch { get { return (Drone == null ? "" : Drone.FlightSteps.DescribePitch(Drone.Config) ); } }

        public override string DescribeDeltaYaw { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeDeltaYaw(Drone.Config) ); } }

        public override string DescribeRoll { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeRoll(Drone.Config) ); } }

        public override (float, float) MinMaxVerticalAxisM { get { return Drone == null ? (0,0) : Drone.FlightSteps.MinMaxVerticalAxisM; } }

        public override string DescribeElevation { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeElevation ); } }

        public override string DescribeSpeed { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeSpeed ); } }


        public DroneDrawScope(Drone drone) : base(drone == null ? null : drone.FlightSteps)
        {
            Drone = drone;
        }

        public DroneDrawScope(TardisSummaryModel? tardisModel) : base(tardisModel)
        {
            Drone = null;
        }


        public bool RunStepInScope(FlightStep flightStep)
        {
            return
                RunStepIdInScope(flightStep.StepId, flightStep.StepId) &&
                Drone.FlightStepInRunScope(flightStep);
        }
    }
}
