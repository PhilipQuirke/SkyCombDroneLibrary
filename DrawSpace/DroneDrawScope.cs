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
        public virtual RelativeLocation MinLocationM { get { return Drone.FlightSections.MinLocationM; } }
        public virtual RelativeLocation MaxLocationM { get { return Drone.FlightSections.MaxLocationM; } }


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
