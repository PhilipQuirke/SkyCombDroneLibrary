// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;


// Contains calculated data about a drone flight, derived from raw flight data and ground elevation data
namespace SkyCombDrone.DroneLogic
{

    // A FlightLeg is a section of a drone flight path that is
    // in a mostly constant direction for a significant duration and travels a significant distance.
    // Main use for a FlightLeg is to limit the scope of CombProcessModel processing.
    // Drone Pitch, Roll and Speed are deliberately ignored (not considered) and may NOT be mostly constant. 
    // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md for more details.
    //
    // FlightLeg used to require constant Altitude, but the Cornerstone Conservation (Lennard Sparks)
    // 'search grid' flight data from March 2024 has useful legs with varying altitude, so remove that requirement.
    public class FlightLeg : FlightLegModel
    {
        public FlightLeg(List<string>? settings = null) : base(settings)
        {
        }


        // Return the percentage overlap of this leg with the RunVideoFromS / RunVideoToS range
        public static int PercentOverlapWithRunFromTo(Drone drone, int minStepId, int maxStepid)
        {
            try
            {
                if ((!drone.HasFlightSteps) || (minStepId < 0) || (maxStepid < 0))
                    return 0;

                var runFromS = drone.DroneConfig.RunVideoFromS;
                var runToS = drone.DroneConfig.RunVideoToS;

                var legFromS = drone.FlightSteps.Steps[minStepId].FlightSection.StartTime.TotalSeconds;
                var legToS = drone.FlightSteps.Steps[maxStepid].FlightSection.StartTime.TotalSeconds;

                // What percentage of this leg overlaps the drone Run From / To 
                var maxMin = Math.Max(runFromS, legFromS);
                var minMax = (runToS < 0.01 ? legToS : Math.Min(runToS, legToS));

                // If more than 2/3 rds of the leg overlaps the drone Run From / To highlight the button  
                double legDuration = legToS - legFromS;
                double overlap = 100.0 * (minMax - maxMin) / legDuration;

                return (int)overlap;
            }
            catch (Exception ex)
            {
                throw ThrowException("FlightLeg.PercentOverlapWithRunFromTo", ex);
            }
        }
        public int PercentOverlapWithRunFromTo(Drone drone)
        {
            return PercentOverlapWithRunFromTo(drone, MinStepId, MaxStepId);
        }
        public bool OverlapsRunFromTo(Drone drone)
        {
            return PercentOverlapWithRunFromTo(drone) >= MinOverlapPercent;
        }


        // Return the FlightStep in this leg that is closest to the specified flightMS
        public FlightStep? MsToNearestFlightStep(int flightMs, FlightStepList steps)
        {
            Assert(flightMs >= MinSumTimeMs, "FlightLeg.FlightMsToNearestFlightStep: Bad logic 1");
            Assert(flightMs <= MaxSumTimeMs, "FlightLeg.FlightMsToNearestFlightStep: Bad logic 2"); ;

            FlightStep nearestStep = null;
            int nearestDelta = int.MaxValue;

            // PQR ToDo Increase speed of this loop by bisection of the sorted list of steps
            for (int stepId = MinStepId; stepId <= MaxStepId; stepId++)
            {
                FlightStep? thisStep;
                if (steps.TryGetValue(stepId, out thisStep))
                {
                    var thisDelta = Math.Abs(thisStep.SumTimeMs - flightMs);
                    if (thisDelta < nearestDelta)
                    {
                        nearestStep = thisStep;
                        nearestDelta = thisDelta;
                    }
                    else if (thisDelta > nearestDelta)
                        break;
                }
            }

            return nearestStep;
        }
    };


    // FlightLegs is a time-ordered list of (zero to many) FlightLeg objects
    public class FlightLegs : ConfigBase
    {
        // The list of flight legs in time order.
        public List<FlightLeg> Legs = new();


        public FlightLegs()
        {
        }


        // What lineal distance is transversed in legs?
        public float SumLinealM()
        {
            float linealM = 0;
            foreach (var leg in Legs)
                if ((leg.MaxSumLinealM >= 0) && (leg.MinSumLinealM >= 0))
                    linealM += leg.MaxSumLinealM - leg.MinSumLinealM;
            return linealM;
        }


        // If this leg violate the duration or distance rule, clean up the steps
        private void RemoveLeg(FlightSteps steps, int legStartKey, int legEndKey)
        {
            foreach (var theStep in steps.Steps)
            {
                if (theStep.Key >= legStartKey)
                    theStep.Value.FlightLegId = 0;
                if (theStep.Key == legEndKey)
                    break;
            }
        }


        // Remove legs that are too short
        private void RemoveSmallLinealLegs(int min_lineal_length_m)
        {
            bool removed = false;
            for (int i = Legs.Count - 1; i >= 0; i--)
            {
                var leg = Legs[i];
                if (leg.MaxSumLinealM - leg.MinSumLinealM < min_lineal_length_m)
                {
                    removed = true;
                    Legs.RemoveAt(i);
                }
            }

            if (removed)
                // Rename the remaining legs into a continuous sequence
                for (int i = 0; i < Legs.Count; i++)
                    Legs[i].FlightLegId = i + 1;
        }


        // Calculate the Step.LegId values
        public List<string>? Calculate_Steps(FlightSections sections, FlightSteps steps, DroneConfigModel config)
        {
            try
            {
                // To evaluate a leg we must have Yaw and Pitch data. Some drones do not provide this data.
                if ((sections == null) || (steps == null) || (!sections.HasYawData()) || (!sections.HasPitchData()))
                    return null;

                // The unique name of each leg
                int maxLegId = 0;

                List<string> whyEnd = new();

                FlightStep? startStep = null;
                FlightStep? prevStep = null;
                FlightStep? endStep = null;
                int startKey = UnknownValue;
                int endKey = UnknownValue;
                int legDurationMs = 0;
                foreach (var thisStepPair in steps.Steps)
                {
                    var thisStep = thisStepPair.Value;
                    if (startKey >= 0)
                    {
                        // If this step violates a rule, then we should finish this leg
                        //bool badSumAltitude = (Math.Abs(startStep.AltitudeM - thisStep.AltitudeM) > config.MaxLegSumAltitudeDeltaM);
                        //bool badStepAltitude = (prevStep != null) && (Math.Abs(prevStep.AltitudeM - thisStep.AltitudeM) > config.MaxLegStepAltitudeDeltaM);
                        bool badYaw = (Math.Abs(thisStep.YawDegsDelta(startStep)) > config.MaxLegSumDeltaYawDeg);
                        bool badSumPitch = (Math.Abs(startStep.PitchDeg - thisStep.PitchDeg) >= config.MaxLegSumPitchDeg);
                        bool badDuration = (thisStep.FlightSection.TimeMs > config.MaxLegGapDurationMs);

                        bool badStepPitch = (!config.UseGimbalData) &&
                            (Math.Abs(thisStep.PitchDeg) >= config.MaxLegStepPitchDeg);
                        bool badCameraDown = config.UseGimbalData &&
                            (Math.Abs(thisStep.PitchDeg) < config.MinCameraDownDeg);


                        if (badYaw || badSumPitch || badDuration || badStepPitch || badCameraDown) // badStepAltitude || badSumAltitude || 
                        {
                            if ((legDurationMs < config.MinLegDurationMs) ||
                                (RelativeLocation.DistanceM(startStep.DroneLocnM, thisStep.DroneLocnM) < config.MinLegDistanceM))
                            {
                                // This leg violate the duration or distance rule,
                                // remove the leg and clean up the steps.
                                RemoveLeg(steps, startKey, thisStepPair.Key);
                                maxLegId--;
                            }
                            else
                            {
                                // End this (good) leg.
                                // if (badSumAltitude)
                                //    whyEnd.Add($"Sum altitude change too large: {startStep.AltitudeM} to {thisStep.AltitudeM}");
                                //if (badStepAltitude)
                                //    whyEnd.Add($"Step altitude change too large: {prevStep.AltitudeM} to {thisStep.AltitudeM}");
                                if (badYaw)
                                    whyEnd.Add($"Yaw change too large: {startStep.YawDegsDelta(startStep)} to {thisStep.YawDegsDelta(startStep)}");
                                else if (badSumPitch)
                                    whyEnd.Add($"Sum pitch too large: {startStep.PitchDeg} to {thisStep.PitchDeg}");
                                else if (badDuration)
                                    whyEnd.Add($"Gap too large: {thisStep.FlightSection.TimeMs}");
                                else if (badStepPitch)
                                    whyEnd.Add($"Step pitch too large: {thisStep.PitchDeg}");
                                else if (badCameraDown)
                                    whyEnd.Add($"Step camera down too small: {thisStep.PitchDeg}");
                            }

                            // Do not include thisStep in this leg. Reset for next leg calcs.
                            prevStep = null;
                            startStep = null;
                            endStep = null;
                            startKey = UnknownValue;
                            endKey = UnknownValue;
                            legDurationMs = 0;
                        }
                        else
                        {
                            // Include this step in the leg.
                            legDurationMs += thisStep.FlightSection.TimeMs;
                            thisStep.FlightLegId = maxLegId;
                            endStep = thisStep;
                            endKey = thisStepPair.Key;
                        }
                    }
                    else
                    {
                        // Should we start a new leg?

                        bool badStepPitch = (!config.UseGimbalData) &&
                            (Math.Abs(thisStep.PitchDeg) >= config.MaxLegStepPitchDeg);

                        if ((Math.Abs(thisStep.DeltaYawDeg) < config.MaxLegStepDeltaYawDeg) &&
                           (!badStepPitch))
                        {
                            maxLegId++;
                            thisStep.FlightLegId = maxLegId;

                            startStep = thisStep;
                            endStep = thisStep;
                            startKey = thisStepPair.Key;
                            endKey = thisStepPair.Key;
                            legDurationMs = thisStep.FlightSection.TimeMs;
                        }
                    }

                    prevStep = thisStep;
                }

                if (startKey > 0)
                {
                    if ((legDurationMs < config.MinLegDurationMs) ||
                        (RelativeLocation.DistanceM(startStep.DroneLocnM, endStep.DroneLocnM) < config.MinLegDistanceM))
                        // This leg violate the duration or distance rule,
                        // remove the leg and clean up the steps.                        
                        RemoveLeg(steps, startKey, endKey);
                    else
                        whyEnd.Add("No more steps");
                }

                return whyEnd;
            }
            catch (Exception ex)
            {
                throw ThrowException("FlightLegs.Calculate_Steps", ex);
            }
        }


        // Create a leg object when we have no Flight Log data
        public void Calculate_NoFlightData(DroneConfigModel config)
        {
            FlightLeg answer = new();

            answer.FlightLegId = 1;
            answer.WhyLegEnded = "N/A";
            answer.MinSumTimeMs = (int)(config.RunVideoFromS * 1000.0f);
            answer.MaxSumTimeMs = (int)(config.RunVideoToS * 1000.0f);
            answer.MinTardisId = UnknownValue;
            answer.MaxTardisId = UnknownValue;

            Legs.Add(answer);
        }


        // Create the leg objects
        public void Calculate_Pass1(FlightSections sections, FlightSteps steps, DroneConfigModel config)
        {
            try
            {
                // Calculate the Step.LegId values
                var whyEnd = Calculate_Steps(sections, steps, config);

                Legs.Clear();

                FlightLeg? thisLeg = null;
                foreach (var thisStep in steps.Steps)
                {
                    if (thisStep.Value.FlightLegId > 0)
                    {
                        int thisLegId = thisStep.Value.FlightLegId;

                        if ((thisLeg != null) && (thisLeg.FlightLegId != thisLegId))
                            thisLeg = null;

                        if (thisLeg == null)
                        {
                            thisLeg = new();
                            thisLeg.FlightLegId = thisStep.Value.FlightLegId;
                            thisLeg.MinTardisId = thisStep.Key;
                            thisLeg.MaxTardisId = thisStep.Key;
                            thisLeg.MinSumLinealM = thisStep.Value.SumLinealM;
                            thisLeg.MaxSumLinealM = thisStep.Value.SumLinealM;
                            thisLeg.WhyLegEnded = (whyEnd.Count > thisLegId - 1 ? whyEnd[thisLegId - 1] : "");

                            Legs.Add(thisLeg);
                        }
                        else
                        {
                            thisLeg.MaxTardisId = thisStep.Key;
                            thisLeg.MaxSumLinealM = thisStep.Value.SumLinealM;
                        }
                    }
                    else
                        thisLeg = null;
                }

                // In DJI_0120 ~3 objects are detected very early in leg 3.
                // While the DeltaYawDeg at the start of the leg is near 0,
                // the video clearly shows a 1/2 second of yaw in the leg.
                // This is likely the gimbal recentering to catch up just after drone finishes yawing.
                // The gimbal is an independent drone subsystem. The drone does not log gimbal-specific yaw data.
                // Each leg is at least config.MinLegDurationMs long (defaults to 2 seconds)
                // Sacrifice (remove) the first 1 second of each leg.
                // PQR TODO: For short legs this may delete the leg!
                foreach (var leg in Legs)
                {
                    var minStepId = leg.MinTardisId;
                    var minStep = steps.Steps[minStepId];

                    var minStepTimeNew = minStep.FlightSection.SumTimeMs + 1;
                    var minStepIdNew = minStepId;

                    while (true)
                    {
                        if (steps.Steps.TryGetValue(minStepIdNew, out FlightStep? theStep))
                        {
                            if (theStep.FlightSection.SumTimeMs >= minStepTimeNew)
                            {
                                // Reset the leg to start with this step
                                leg.MinTardisId = minStepIdNew;
                                break;
                            }
                            else
                                // Remove the step from the leg
                                theStep.FlightLegId = 0;
                        }

                        minStepIdNew++;
                    }
                }

            }
            catch (Exception ex)
            {
                throw ThrowException("FlightLegs.Calculate_Pass1", ex);
            }
        }


        // If we have many legs we may be running a search grid with long legs and short legs.
        // Remove short legs as they are less interesting.
        public void Calculate_Pass2()
        {
            int optimal_num_legs = 26; // Based on UI constraints

            for (int lineal_length_m = 5; lineal_length_m <= 50; lineal_length_m += 5)
            {
                if (Legs.Count > optimal_num_legs)
                    RemoveSmallLinealLegs(lineal_length_m);

                if (Legs.Count <= optimal_num_legs)
                    return;
            }
        }


        // Update the leg objects
        public void Calculate_Pass3(FlightSteps steps)
        {
            foreach (var leg in Legs)
            {
                leg.ResetTardis();
                foreach (var step in steps.Steps)
                    if (step.Value.FlightLegId == leg.FlightLegId)
                    {
                        leg.SummariseTardis(step.Value);
                        step.Value.FlightLeg = leg;
                    }

                Assert(leg.MinTardisId > 0, "FlightLeg.Calculate_Pass2: Bad MinTardisId");
                Assert(leg.MaxTardisId > 0, "FlightLeg.Calculate_Pass2: Bad MaxTardisId");
            }
        }


        // Update the FlightStep pointers to FlightLeg
        public void Set_FlightStep_FlightLeg(FlightSteps steps)
        {
            foreach (var step in steps.Steps)
            {
                if (step.Value.FlightLegId <= 0)
                    continue;

                foreach (var leg in Legs)
                    if (step.Value.FlightLegId == leg.FlightLegId)
                    {
                        step.Value.FlightLeg = leg;
                        break;
                    }
            }
        }


        // Return the index of the first and last leg overlap of this leg with the RunVideoFromS / RunVideoToS range
        public (int, int) OverlappingLegsRange(Drone drone)
        {
            int firstLegId = UnknownValue;
            int lastLegId = UnknownValue;

            foreach (var leg in Legs)
            {
                if (leg.OverlapsRunFromTo(drone))
                {
                    if (firstLegId == UnknownValue)
                        firstLegId = leg.FlightLegId;
                    lastLegId = leg.FlightLegId;
                }
                else
                {
                    if (firstLegId != UnknownValue)
                        break;
                }
            }

            return (firstLegId, lastLegId);
        }


        public string DescribeLegs
        {
            get
            {
                if (Legs.Count == 0)
                    return "";

                return string.Format(", {0} legs", Legs.Count);
            }
        }


        public void AssertGood(bool hasFlightSteps)
        {
            foreach (var thisLeg in Legs)
            {
                if (hasFlightSteps)
                {
                    Assert(thisLeg.MinStepId >= 0, "FlightLegs.AssertGood: Bad MinStepId");
                    Assert(thisLeg.MaxStepId > 0, "FlightLegs.AssertGood: Bad MaxStepId");
                    Assert(thisLeg.RangeSumTimeMs > 0, "FlightLegs.AssertGood: Bad RangeSumTimeMs");
                    Assert(thisLeg.MinAltitudeM > 0, "FlightLegs.AssertGood: Bad MinAltitudeM");
                    Assert(thisLeg.MaxAltitudeM > 0, "FlightLegs.AssertGood: Bad MaxAltitudeM");
                }
            }
        }
    }
}


