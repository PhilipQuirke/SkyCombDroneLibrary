// Copyright SkyComb Limited 2023. All rights reserved.
using SkyCombDrone.DroneLogic;
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.DrawSpace
{
    // Code to draw images related to drone & process data in charts, graphs,etc.
    public class DroneDrawScope : BaseConstants
    {
        // The drone data (if any) to draw
        public Drone? Drone = null;


        // Drone encompassing box size in local coordinate system - NorthingM/EastingM
        public virtual DroneLocation MinDroneLocnM { get { return Drone.FlightSections.MinDroneLocnM; } }
        public virtual DroneLocation MaxDroneLocnM { get { return Drone.FlightSections.MaxDroneLocnM; } }


        // First millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int FirstDrawMs { get { return 0; } }
        // Last millisecond of flight data drawn. Used on graphs with a time axis
        public virtual int LastDrawMs { get { return Drone.FlightSections.MaxTimeMs; } }


        // First step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int FirstDrawStepId { get { return 0; } }
        // Last step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int LastDrawStepId { get { return Drone.FlightSections.MaxTardisId; } }


        // First step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int FirstRunStepId { get { return 0; } }
        // Last step of flight data to draw. A StepId axis approximates a time axis.
        public virtual int LastRunStepId { get { return Drone.FlightSections.MaxTardisId; } }


        public virtual FlightStep CurrRunFlightStep { get { return null; } }
        public virtual int CurrRunStepId { get { return 1; } }


        public virtual float FloorMinSumLinealM { get { return Drone.FlightSteps.FloorMinSumLinealM; } }
        public virtual float CeilingMaxSumLinealM { get { return Drone.FlightSteps.CeilingMaxSumLinealM; } }
        public virtual string DescribePath { get { return ( Drone == null ? "" : Drone.DescribeFlightPath ); } }
        public virtual DataPairList GetSettings_FlightPath { get { return Drone.FlightSteps.GetSettings_FlightPath(); } }


        public virtual float FloorMinPitchDeg { get { return Drone.FlightSteps.FloorMinPitchDeg; } }
        public virtual float CeilingMaxPitchDeg { get { return Drone.FlightSteps.CeilingMaxPitchDeg; } }
        public virtual string DescribePitch { get { return (Drone == null ? "" : Drone.FlightSteps.DescribePitch(Drone.Config) ); } }
        public virtual DataPairList GetSettings_Pitch { get { return Drone.FlightSteps.GetSettings_Pitch(); } }


        public virtual float FloorMinDeltaYawDeg { get { return Drone.FlightSteps.FloorMinDeltaYawDeg; } }
        public virtual float CeilingMaxDeltaYawDeg { get { return Drone.FlightSteps.CeilingMaxDeltaYawDeg; } }
        public virtual string DescribeDeltaYaw { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeDeltaYaw(Drone.Config) ); } }
        public virtual DataPairList GetSettings_DeltaYaw { get { return Drone.FlightSteps.GetSettings_DeltaYaw(); } }


        public virtual float FloorMinRollDeg { get { return Drone.FlightSteps.FloorMinRollDeg; } }
        public virtual float CeilingMaxRollDeg { get { return Drone.FlightSteps.CeilingMaxRollDeg; } }
        public virtual string DescribeRoll { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeRoll(Drone.Config) ); } }
        public virtual DataPairList GetSettings_Roll { get { return Drone.FlightSteps.GetSettings_Roll(); } }


        public virtual (float, float) MinMaxVerticalAxisM { get { return Drone.FlightSteps.MinMaxVerticalAxisM; } }
        public virtual string DescribeElevation { get { return (Drone == null ? "" : Drone.FlightSteps.DescribeElevation ); } }
        public virtual DataPairList GetSettings_Altitude { get { return Drone.FlightSteps.GetSettings_Altitude(); } }


        public virtual string DescribeSpeed { get { return Drone.FlightSteps.DescribeSpeed; } }
        public virtual DataPairList GetSettings_Speed { get { return Drone.FlightSteps.GetSettings_Speed(); } }
        public virtual float MaxSpeedMps { get { return Drone.FlightSteps.MaxSpeedMps; } }


        // The ObjectForm only wants to draw features up to a certain BlockId
        public int MaxFeatureBlockIdToDraw = 999999;


        public DroneDrawScope(Drone drone)
        {
            Drone = drone;
        }


        // Are we drawing the full flight? Or just a subset of it?
        public bool DrawFullFlight()
        {
            return
                (Drone != null) &&
                (FirstDrawStepId == 0) &&
                (LastDrawStepId == Drone.FlightSteps.MaxStepId);
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
}
