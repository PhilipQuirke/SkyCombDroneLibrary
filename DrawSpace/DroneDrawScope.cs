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


        // Drone encompassing box size in local coordinate system - NorthingM/EastingM
        public virtual DroneLocation MinDroneLocnM { get { return TardisSummary.MinDroneLocnM; } }
        public virtual DroneLocation MaxDroneLocnM { get { return TardisSummary.MaxDroneLocnM; } }


        // First millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int FirstDrawMs { get { return 0; } }
        // Last millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int LastDrawMs { get { return TardisSummary.MaxTimeMs; } }


        // First step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int FirstDrawStepId { get { return 0; } }
        // Last step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int LastDrawStepId { get { return TardisSummary.MaxTardisId; } }


        // First step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int FirstRunStepId { get { return 0; } }
        // Last step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int LastRunStepId { get { return TardisSummary.MaxTardisId; } }


        public virtual FlightStep CurrRunFlightStep { get { return null; } }
        public virtual int CurrRunStepId { get { return 1; } }


        public virtual float FloorMinSumLinealM { get { return TardisSummary.FloorMinSumLinealM; } }
        public virtual float CeilingMaxSumLinealM { get { return TardisSummary.CeilingMaxSumLinealM; } }
        public virtual string DescribePath { get { return ""; } }
        public virtual DataPairList GetSettings_FlightPath { get { return null; } }


        public virtual float FloorMinPitchDeg { get { return TardisSummary.FloorMinPitchDeg; } }
        public virtual float CeilingMaxPitchDeg { get { return TardisSummary.CeilingMaxPitchDeg; } }
        public virtual string DescribePitch { get { return ""; } }
        public virtual DataPairList GetSettings_Pitch { get { return TardisSummary.GetSettings_Pitch(); } }


        public virtual float FloorMinDeltaYawDeg { get { return TardisSummary.FloorMinDeltaYawDeg; } }
        public virtual float CeilingMaxDeltaYawDeg { get { return TardisSummary.CeilingMaxDeltaYawDeg; } }
        public virtual string DescribeDeltaYaw { get { return "";  } }
        public virtual DataPairList GetSettings_DeltaYaw { get { return TardisSummary.GetSettings_DeltaYaw(); } }


        public virtual float FloorMinRollDeg { get { return TardisSummary.FloorMinRollDeg; } }
        public virtual float CeilingMaxRollDeg { get { return TardisSummary.CeilingMaxRollDeg; } }
        public virtual string DescribeRoll { get { return ""; } }
        public virtual DataPairList GetSettings_Roll { get { return TardisSummary.GetSettings_Roll(); } }


        public virtual (float, float) MinMaxVerticalAxisM { get { return (0, 0 ); } }
        public virtual string DescribeElevation { get { return ""; } }
        public virtual DataPairList GetSettings_Altitude { get { return TardisSummary.GetSettings_Altitude(); } }


        public virtual string DescribeSpeed { get { return ""; } }
        public virtual DataPairList GetSettings_Speed { get { return TardisSummary.GetSettings_Speed(); } }
        public virtual float MaxSpeedMps { get { return TardisSummary.MaxSpeedMps; } }


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
    }


    // Code to draw images related to drone & process data in charts, graphs,etc.
    public class DroneDrawScope : TardisDrawScope
    {
        // The drone data (if any) to draw
        public Drone? Drone = null;


        public override string DescribePath { get { return ( Drone == null ? "" : Drone.DescribeFlightPath ); } }

        public override string DescribePitch { get { return (Drone == null ? "" : Drone.FlightSteps.DescribePitch(Drone.Config) ); } }

        public override string DescribeDeltaYaw { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeDeltaYaw(Drone.Config) ); } }

        public override string DescribeRoll { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeRoll(Drone.Config) ); } }

        public override (float, float) MinMaxVerticalAxisM { get { return Drone == null ? (0,0) : Drone.FlightSteps.MinMaxVerticalAxisM; } }

        public override string DescribeElevation { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeElevation ); } }

        public override string DescribeSpeed { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeSpeed ); } }


        public DroneDrawScope(Drone drone) : base (drone == null ? null : drone.FlightSteps)
        {
            Drone = drone;
        }

        public DroneDrawScope(TardisSummaryModel tardisModel) : base(tardisModel)
        {
            Drone = null;
        }
    }
}
