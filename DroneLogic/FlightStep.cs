// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundLogic;
using System.Drawing;


// Contains calculated data about a drone flight, derived from raw flight data and ground elevation data
namespace SkyCombDrone.DroneLogic
{

    // FlightStep data is derived from FlightSection data. It contains additional data sources & calculated data.
    public class FlightStep : FlightStepModel
    {
        // The corresponding FlightSection.
        // There is a 1 to 1 relationship between Sections and Steps.
        public FlightSection FlightSection { get; }

        public FlightLeg? FlightLeg { get; set; }



        public FlightStep(FlightSection flightSection, List<string>? settings = null) : base(flightSection, settings)
        {
            FlightSection = flightSection;
            FlightLeg = null;
        }


        // Smooth the Section data, based on a window of "2*smoothRadius+1" Sections, to give Step data.
        // Calculates FlightStep smoothed attributes AltitudeM 
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md for the rationale for this function.
        public bool CalculateSettings_SmoothAltitude(int smoothRadius, FlightSections sections)
        {
            // If flight stepId duration is too great don't try to smooth it. This happens very rarely.
            if (this.TimeMs > FlightSection.MaxSensibleSectionDurationMs)
                return false;

            Assert(smoothRadius >= 1, "CalculateSettings_SmoothAltitudeM: No smoothing");

            float sumAltitudeWeight = 0;
            float sumAltitudeM = 0;

            // Collect data from the previous, this and next Sections around thisStep, weighted by their distance in time.
            var thisSectionId = FlightSection.SectionId;
            for (int j = Math.Max(0, thisSectionId - smoothRadius); j <= thisSectionId + smoothRadius; j++)
            {
                if (sections.Sections.TryGetValue(j, out FlightSection? nearbySection))
                {
                    // If a "large gap" stepId is nearby then don't smooth this step.
                    // This "averaging" function assumes an even number of "sensible" neighbours before AND after this step
                    if (nearbySection.TimeMs > FlightSection.MaxSensibleSectionDurationMs)
                        return false;

                    bool middleSection = (nearbySection.SectionId == thisSectionId);
                    int stepsDistance = Math.Abs(thisSectionId - nearbySection.SectionId);
                    var time_diff = Math.Abs(nearbySection.SumTimeMs - FlightSection.SumTimeMs);
                    if ((!middleSection) && (time_diff == 0))
                        continue;
                    // Give most weight to the middle section, less weight to other sections, depending on their distance.
                    float weight = middleSection ? 1.0f : Math.Max(0.0f, Math.Min(0.9f, 1.0f * (smoothRadius - stepsDistance) / smoothRadius));

                    if (nearbySection.AltitudeM != UnknownValue)
                    {
                        sumAltitudeM += nearbySection.AltitudeM * weight;
                        sumAltitudeWeight += weight;
                    }
                }
            }

            if (sumAltitudeWeight > 0)
                AltitudeM = sumAltitudeM / sumAltitudeWeight;

            return true;
        }


        // Smooth the Section data, based on a window of "numSmoothSteps" Sections, to give Step data.
        // Calculates FlightStep smoothed attributes LocationM, YawDegs, PitchDegs.
        // Refer https://github.com/PhilipQuirke/SkyCombAnalystHelp/Drone.md
        // chapter on NumSmoothSteps for the rationale for this function.
        // Do not assume that each step covers the same time period.
        public bool CalculateSettings_SmoothLocationYawPitch(int smoothRadius, FlightSections sections)
        {
            // If flight stepId duration is too great don't try to smooth it. This happens very rarely.
            if (TimeMs > FlightSection.MaxSensibleSectionDurationMs)
                return false;

            var thisSectionId = FlightSection.SectionId;
            Assert(smoothRadius >= 1, "CalculateSettings_SmoothLocationYawPitch: No smoothing");


            (bool success, float sumLocnWeight, float sumNorthingM, float sumEastingM, float sumPosYawDegs, float sumNegYawDegs, float sumPosYawWeight, float sumNegYawWeight, float sumPitchDegs, float sumPitchWeight)
                = sections.Smooth(thisSectionId, smoothRadius);
            if (!success)
                return false;


            if (sumLocnWeight > 1)
                DroneLocnM = new(sumNorthingM / sumLocnWeight, sumEastingM / sumLocnWeight);

            if ((sumPosYawWeight > 0) && (sumNegYawWeight > 0))
            {
                // Do not try to smooth the yaw. Have a yaw transitions from +ve to -ve (or vice versa)  
            }
            else if (sumNegYawWeight > 0)
                YawDeg = sumNegYawDegs / sumNegYawWeight;
            else if (sumPosYawWeight > 0)
                YawDeg = sumPosYawDegs / sumPosYawWeight;

            if ((YawDeg > -0.001) && (YawDeg < +0.001))
                YawDeg = 0;


            if (sumPitchWeight > 0)
                PitchDeg = sumPitchDegs / sumPitchWeight;

            if ((PitchDeg > -0.001) && (PitchDeg < +0.001))
                PitchDeg = 0;

            return true;
        }


        // Estimate the ground elevation (in metres) of this flight step
        public void CalculateSettings_DemM(GroundData groundData)
        {
            DemM = groundData?.DemModel?.GetElevationByDroneLocn(DroneLocnM) ?? DemM;
        }


        // Estimate the surface elevation (in metres) of this flight step
        public void CalculateSettings_DsmM(GroundData groundData)
        {
            DsmM = groundData?.DsmModel?.GetElevationByDroneLocn(DroneLocnM) ?? DsmM;
        }


        // Use a weighted average of DroneToGroundAltStartSyncM and DroneToGroundAltEndSyncM
        // to improve the AltitudeM accuracy.
        public void CalculateSettings_AltitudeM_ApplyOnGroundAt(
            float droneToGroundAltStartSyncM,
            float droneToGroundAltEndSyncM,
            float maxStepId)
        {
            AltitudeM +=
                droneToGroundAltStartSyncM * ((maxStepId - StepId) / maxStepId) +
                droneToGroundAltEndSyncM * (StepId / maxStepId);
        }


        // Best estimate of drone altitude (height) above sea level in metres e.g. 61.241 m. Aka absolute altitude.
        public float FixedAltitudeM { get { return (AltitudeM == UnknownValue ? UnknownValue : AltitudeM + FixAltM); } }
        // Vertical distance from drone to ground
        public float FixedDistanceDown { get { return FixedAltitudeM - DemM; } }


        // Reported camera down angle (measured from the vertical) based on drone data.
        // A positive value means the camera is looking forward. 
        public float CameraToVerticalForwardDeg
        {
            get
            {
                if (FlightSection.Drone.DroneConfig.GimbalDataAvail == GimbalDataEnum.ManualNo)
                {
                    // Camera may be pointing straight down (CameraDownDeg=90)
                    // or forward in direction of flight (CameraDownDeg=0)
                    // or in between (say CameraDownDeg=72)
                    // Calculate difference between CameraDownAngle and the vertical.
                    // Assumes drone camera down angle is constant over the period image is seen. 
                    int cameraToVertDeg = FlightSection.Drone.DroneConfig.FixedCameraToVerticalForwardDeg;
                    Assert(cameraToVertDeg >= 0 && cameraToVertDeg <= 90, "BestCameraDownDeg: Bad cameraToVertDeg");
                    return cameraToVertDeg;
                }
                else
                {
                    // Gimbal pitch is available!
                    // Use it to calculate the angle to the vertical.
                    // This may differ for the first and last feature.
                    var cameraToVertDeg = 90 + PitchDeg;

                    // Pitch is normally -45 to -90. Rarely can be +35 (looking up) or -125 (looking backwards).
                    // Assert(cameraToVertDeg >= 0 && cameraToVertDeg <= 135, "BestCameraDownDeg: Bad firstCameraToVertDeg");

                    return cameraToVertDeg;
                }
            }
        }
        // Best estimate of camera down angle (measured from the vertical)
        public float FixedCameraToVerticalForwardDeg { get { return CameraToVerticalForwardDeg + FixPitchDeg; } }


        // Best estimate of camera yaw. May differ slightly from camera yaw as reported by the drone.
        public float FixedYawDeg { get { return YawDeg + FixYawDeg; } }


        // Calculate CameraDownDegInputImageCenter, InputImageSizeM,
        // InputImageCenterDem and InputImageCenterDem
        // Depends on Camera.VFOVDeg, CameraDownDeg, AltitudeM, DsmM, Yaw, Zoom & GroundData
        // Handles undulating ground between drone and image center.
        public void CalculateSettings_InputImageCenterDemDsm(VideoModel videoData, GroundData? groundData)
        {
            InputImageCenter = new();

            // Can only calculate this if we can compare the ground elevation & drone altitude.
            if ((DsmM == UnknownValue) || (DroneLocnM == null) || (videoData == null))
                return;

            // If the camera is pointing at the horizon, then the 
            // image area is huge and useless for animal detection.
            // If camera image includes the horizon, then ignore this step.
            // (For thermal cameras the horizon is often brighter causing
            // thermal bloom with causes over estimates of temperature.)
            var vfovDeg = videoData.VFOVDeg;
            float degreesToVerticalForward = FixedCameraToVerticalForwardDeg;
            if (degreesToVerticalForward >= 90 - vfovDeg / 2)
                return;

            // Vertical distance from drone to ground (including FixAltM) at drone location
            double droneLocnDownVertM = FixedDistanceDown;

            // Distance across ground to center of image area - in the direct of flight.
            // Note that the drone camera gimbal automatically compensates for
            // PitchDeg & RollDeg so we can ignore them.

            // The actual camera area imaged depends on CameraDownDeg.
            // This assumes the land under the drone to the image centre is flat.
            double flatEarthForwardM = droneLocnDownVertM * Math.Tan(degreesToVerticalForward * DegreesToRadians);

            // Get unit vector in the direction the camera is pointing
            var unitVector = InputImageUnitVector;

            // Working out the center of the image area
            // Can't assume land is flat. Land may be rising or falling.
            // Can't assume a steady change. There may be an isolated hill in view.

            // Start by assuming the land under the drone + image is flat.
            var flatEarthLocn = DroneLocnM.Add(unitVector, (float)flatEarthForwardM);


            // If camera is pointing nearly straight down,
            // then horizontal distance is small, and from drone's height
            // distortion is small enough to ignore.
            InputImageCenter = null;
            if ((flatEarthForwardM > 3) && (AltitudeM > DsmM + 3) &&
                (groundData != null) && (groundData.DsmModel != null))
            {
                // Walk from DroneLocnM towards the flatEarthLocn, evaluating the
                // earth DSM every 2 metres, until the DSM elevation is higher than
                // than the drone line of sight towards the flatEarthLocn or we reach
                // flatEarthLocn. We have DSM data at 1m intervals, in a grid pattern.
                var paceForwardM = 2;
                var paceDsmFall = Math.Cos(degreesToVerticalForward * DegreesToRadians);
                var numPaces = (int)(flatEarthForwardM / paceForwardM);
                for (int paceNum = 1; paceNum < numPaces; paceNum++)
                {
                    var paceM = paceNum * paceForwardM;
                    var paceLocn = DroneLocnM.Add(unitVector, paceM);
                    var viewDsm = FixedAltitudeM - paceDsmFall * paceM;
                    var inputImageDsmM = groundData.DsmModel.GetElevationByDroneLocn(paceLocn);

                    // Drone altitude inaccuracies can mean we never reach the earthDSM.
                    InputImageCenter = paceLocn;
                    if (inputImageDsmM >= viewDsm)
                        break;
                }
            }
            if (InputImageCenter == null)
                InputImageCenter = flatEarthLocn;

            // InputImageSizeM
            double viewLength = Math.Sqrt(droneLocnDownVertM * droneLocnDownVertM + flatEarthForwardM * flatEarthForwardM);
            double halfHFOVRadians = 0.5 * videoData.HFOVDeg * DegreesToRadians;
            float imageXSizeM = (float)(viewLength * 2 * Math.Sin(halfHFOVRadians));

            // If this drone camera supports zooming,
            // the zoom reduces the input area.
            // For Lennard Sparks drone zoom is in the range 1 to 6.07
            var zoom = (Zoom >= 1 ? Zoom : 1);
            imageXSizeM /= zoom;

            int videoHeight = videoData.ImageHeight;
            int videoWidth = videoData.ImageWidth;
            InputImageSizeM = new(imageXSizeM, imageXSizeM * videoHeight / videoWidth);
            InputImageSizeM.AssertGood();
        }


        // Calculate InputImageArea corners
        // Area covered by the step's video image (may be forward of drone's location).
        public (DroneLocation corner1, DroneLocation corner2, DroneLocation corner3, DroneLocation corner4)
            Calculate_InputImageArea_Corners()
        {
            // Get unit vector in the direction the camera is pointing
            var unitVector = InputImageUnitVector;
            var sinYaw = unitVector.Value.Y;
            var cosYaw = unitVector.Value.X;

            var halfWidth = InputImageSizeM.Value.X / 2.0;
            var halfHeight = InputImageSizeM.Value.Y / 2.0;

            // Rotate the image area by the drone's yaw.
            DroneLocation corner1 = new(
                (float)(sinYaw * (-halfHeight) + cosYaw * (+halfWidth)),
                (float)(cosYaw * (-halfHeight) - sinYaw * (+halfWidth)));
            DroneLocation corner2 = new(
                (float)(sinYaw * (+halfHeight) + cosYaw * (+halfWidth)),
                (float)(cosYaw * (+halfHeight) - sinYaw * (+halfWidth)));
            DroneLocation corner3 = new(
                (float)(sinYaw * (+halfHeight) + cosYaw * (-halfWidth)),
                (float)(cosYaw * (+halfHeight) - sinYaw * (-halfWidth)));
            DroneLocation corner4 = new(
                (float)(sinYaw * (-halfHeight) + cosYaw * (-halfWidth)),
                (float)(cosYaw * (-halfHeight) - sinYaw * (-halfWidth)));

            return (
                corner1.Translate(InputImageCenter),
                corner2.Translate(InputImageCenter),
                corner3.Translate(InputImageCenter),
                corner4.Translate(InputImageCenter)
            );
        }


        // Calculate physical location of this feature based on:
        // 1) the POSITION in the image of the feature (given by horizontalFraction, verticalFraction, say 0.4, 0.1)
        // 2) the CENTER of the drone physical field of vision (given by FlightStep.InputImageCenter, say 240m Northing, 78m Easting) 
        // 3) the SIZE of the drone physical field of vision (given by InputImageSizeM, say 18m by 9m)
        // 4) the DIRECTION of flight of the drone (given by YawDeg, say -73 degrees)
        // This is the key translation from IMAGE to PHYSICAL coordinate system. 
        // Does NOT consider land contour undulations in image area
        public DroneLocation? CalcImageFeatureLocationM(DroneLocation deltaBlockLocnM, double horizontalFraction, double verticalFraction)
        {
            if (InputImageSizeM == null)
                return null;

            // Physical offset of the object within the drone field of vision InputImageSizeM.
            // This is NOT rotated to drone direction of flight.
            // The horizontalFraction incorporates the Sine of the objects horizontal angle from the center of the image.
            // The verticalFraction incorporates the Sine of the objects vertical angle from the center of the image.
            PointF objectInImageLocnM = new(
                (float)(InputImageSizeM.Value.X * (horizontalFraction - 0.5)),
                (float)(InputImageSizeM.Value.Y * (verticalFraction - 0.5)));

            // Physical offset of the object within the drone physical field of vision.
            // Rotated to match the flight direction of the drone.
            DroneLocation objectLocationRotatedM =
                new(DroneLocation.RotatePoint(objectInImageLocnM, Math.PI - YawRad));

            // With multiple blocks per step we translate by the block delta
            var answer = InputImageCenter.Translate(deltaBlockLocnM);

            // Add the rotated physical offset of object to the center of the drone physical field of vision
            return answer.Translate(objectLocationRotatedM.Negate());
        }


        // After the location of this step has been refined, 
        // recalculate the LinealM, SpeedMps, SumLinealM, InputImageCenter & InputImageSizeM
        public void CalculateSettings_RefineLocationData(VideoModel videoData, FlightStep? prevStep, GroundData? groundData)
        {
            if (prevStep == null)
                return;

            CalculateSettings_LinealM(prevStep);

            // Calculate CameraDownDegInputImageCenter and InputImageSizeM.
            // Depends on CameraDownDeg, AltitudeM, DemM, Yaw & Zoom.
            // Handles undulating ground between drone and image center.
            CalculateSettings_InputImageCenterDemDsm(videoData, groundData);
        }
    };


    public class FlightStepList : SortedList<int, FlightStep>
    {

        public void AddStep(FlightStep step)
        {
            BaseConstants.Assert(step.StepId >= 0, "FlightStepList.AddStep: No Id");
            Add(step.StepId, step);
        }


        // Return a subset of this.Steps matching the specified legId
        public FlightStepList GetLegSteps(int legId)
        {
            FlightStepList answer = new();

            foreach (var theStep in this)
                if (theStep.Value.FlightLegId == legId)
                    answer.Add(theStep.Value.StepId, theStep.Value);

            return answer;
        }


        // For each step, set FixAltM/FixYawDeg/FixPitchDeg and recalculate the ground image area viewed
        public void CalculateSettings_FixValues(float fixAltM, float fixYawDeg, float fixPitchDeg, VideoModel videoData, GroundData? groundData)
        {
            foreach (var theStep in this)
            {
                theStep.Value.FixAltM = fixAltM;
                theStep.Value.FixYawDeg = fixYawDeg;
                theStep.Value.FixPitchDeg = fixPitchDeg;

                theStep.Value.CalculateSettings_InputImageCenterDemDsm(videoData, groundData);
            }
        }


        // Summarise various attributes of the specified range of Steps
        public void CalculateSettings_Summarise(FlightStepSummaryModel summary, int fromStepId, int toStepId)
        {
            summary.ResetSteps();

            int numSteps = 0;
            int numSpeedSteps = 0;
            float sumSpeedMps = 0;
            foreach (var thisStepPair in this)
            {
                var thisStep = thisStepPair.Value;

                if (thisStep.StepId < fromStepId ||
                    thisStep.StepId > toStepId)
                    continue;
                numSteps++;

                summary.SummariseStep(thisStep);

                var thisSpeedMps = thisStep.SpeedMps;
                if (thisSpeedMps != BaseConstants.UnknownValue)
                {
                    numSpeedSteps++;
                    sumSpeedMps += thisSpeedMps;
                }
            }

            if (numSpeedSteps > 0)
                summary.AvgSpeedMps = sumSpeedMps / numSpeedSteps;
        }


        // Summarise various attributes of all Steps
        public void CalculateSettings_Summarise(FlightStepsModel summary, FlightSections sections)
        {
            CalculateSettings_Summarise(summary, sections.MinTardisId, sections.MaxTardisId);

            summary.AssertGoodRevision(sections);
        }
    }


    // FlightSteps is a list of FlightStep and other summary data (based on the FlightInputList and ground data)
    public class FlightSteps : FlightStepsModel
    {
        private Drone Drone { get; }
        private FlightSections Sections { get; }

        // The list of flight sections sorted in time order.
        // The index sequence will be 1, 2, 3, etc. Rarely, gaps occur,but only if the drone flight log has time gaps.
        public FlightStepList Steps = new();


        public FlightSteps(Drone drone, List<string>? settings = null)
            : base(drone.FlightSections.FileName, settings)
        {
            Drone = drone;
            Sections = drone.FlightSections;
            FileName = drone.FlightSections.FileName;
        }


        public void SetTardisMaxKey()
        {
            TardisMaxKey = 0;
            if (Steps.Count > 0)
                TardisMaxKey = Steps.Keys[Steps.Count - 1];
        }


        // Return the child FlightStep
        public override TardisModel? GetTardisModel(int index)
        {
            FlightStep? answer = null;
            Steps.TryGetValue(index, out answer);
            return answer;
        }


        // Return FlightStep.DemM minus FlightStep.AltitudeM
        private (float altLessDem, float altLessDsm) CalcAltitudeLessDemDsm(GroundData ground, FlightStep? theStep)
        {
            if ((theStep != null) &&
                (theStep.AltitudeM != UnknownValue) &&
                (theStep.DemM != UnknownValue))
            {
                var altLessDem = theStep.AltitudeM - theStep.DemM; // Generally positive
                if (Math.Abs(altLessDem) > ground.DemModel.ElevationAccuracyM)
                {
                    var altLessDsm = (theStep.DsmM != UnknownValue ? theStep.AltitudeM - theStep.DsmM : UnknownValue); // Generally positive
                    return (altLessDem, altLessDsm);
                }
            }

            return (UnknownValue, UnknownValue);
        }
        private float CalcDemLessInputAltitude(GroundData ground, int stepId)
        {
            (var altLessDem, var altLessDsm) = CalcAltitudeLessDemDsm(ground, Steps[stepId]);

            if (altLessDem == UnknownValue)
                return 0;

            return -altLessDem;
        }


        // Calculate the average height of the drone above the DEM
        // Calculate the minimum height of the drone above the DSM
        public void CalculateSettings_AvgAndMinHeightM(GroundData ground)
        {
            AvgHeightOverDemM = UnknownValue;
            MinHeightOverDsmM = UnknownValue;

            int numSteps = 0;
            float sumAltLessDem = 0;
            float minAltLessDsm = 10000;
            foreach (var thisStep in Steps)
            {
                (float altLessDem, float altLessDsm) = CalcAltitudeLessDemDsm(ground, thisStep.Value);
                if (altLessDem > 0)
                {
                    numSteps++;
                    sumAltLessDem += altLessDem;
                    if (altLessDsm > 0)
                        minAltLessDsm = Math.Min(minAltLessDsm, altLessDsm);
                }
            }

            if (numSteps > 0)
            {
                AvgHeightOverDemM = (float)Math.Round(sumAltLessDem / numSteps, ElevationNdp);
                MinHeightOverDsmM = (float)Math.Round(minAltLessDsm, ElevationNdp);
            }
        }


        // Drone altitudes are often measured using barometic pressure, which is inaccurate.
        // See if we can improve the accuracy of the input drone altitude data.          
        public void CalculateSettings_OnGroundAt(GroundData ground)
        {
            try
            {
                OnGroundAtFixStartM = 0;
                OnGroundAtFixEndM = 0;

                // If the drone flight video record started &/or ended when the drone was on the ground
                // then the ground DEM and drone Altitude should match (within the ground.ElevationAccuracyM error).
                // If they don't we assume the ground DEM us more accurate, and correct the drone altitude.
                switch (Drone.DroneConfig.OnGroundAt)
                {
                    case OnGroundAtEnum.Start:
                        // Drone was on ground at start of the flight
                        OnGroundAtFixStartM = CalcDemLessInputAltitude(ground, Sections.MinTardisId);
                        OnGroundAtFixEndM = OnGroundAtFixStartM;
                        break;

                    case OnGroundAtEnum.End:
                        // Drone was on ground at end of the flight
                        OnGroundAtFixEndM = CalcDemLessInputAltitude(ground, Sections.MaxTardisId);
                        OnGroundAtFixStartM = OnGroundAtFixEndM;
                        break;

                    case OnGroundAtEnum.Both:
                        // Drone was on ground at the start and the end of the flight.   
                        OnGroundAtFixStartM = CalcDemLessInputAltitude(ground, Sections.MinTardisId);
                        OnGroundAtFixEndM = CalcDemLessInputAltitude(ground, Sections.MaxTardisId);
                        break;

                    case OnGroundAtEnum.Neither:
                    case OnGroundAtEnum.Auto:
                        // If the minimum drone altitude is below the minimum ground DEM then correct it.
                        // This case has been seen for a drone flight from a sea beach where the drone MinAltitude was NEGATIVE 56 metres!
                        if ((MinDemM != UnknownValue) && (Sections.MinAltitudeM < MinDemM))
                        {
                            OnGroundAtFixStartM = MinDemM - Sections.MinAltitudeM;
                            OnGroundAtFixEndM = OnGroundAtFixStartM;
                        }
                        break;
                }


                // If OnGroundAt data gave altitude delats then apply them to all steps 
                if (HasOnGroundAtFix)
                    foreach (var thisStep in Steps)
                        thisStep.Value.CalculateSettings_AltitudeM_ApplyOnGroundAt(
                            OnGroundAtFixStartM,
                            OnGroundAtFixEndM,
                            Sections.MaxTardisId);
            }
            catch (Exception ex)
            {
                throw ThrowException("FlightSteps.CalculateSettings_OnGroundAt", ex);
            }
        }


        // Calculate CameraDownDeg, InputImageCenter and InputImageSizeM.
        // Depends on AltitudeM, DemM, Yaw & Zoom.
        public void CalculateSettings_CameraDownDeg(VideoData videoData, GroundData? groundData)
        {
            foreach (var thisStep in Steps)
                thisStep.Value.CalculateSettings_InputImageCenterDemDsm(videoData, groundData);
        }


        // Copy the raw data from FlightSection to FlightStep 
        // This includes TardisId aka SectionId aka StepId
        public void CopySectionToStepSettings()
        {
            foreach (var thisStep in Steps)
                thisStep.Value.CopyTardis(thisStep.Value.FlightSection);
        }


        // Calculate Step.AltitudeM by smoothing raw Section.AltitudeM data 
        public void CalculateSettings_SmoothAltitudeM()
        {
            foreach (var thisStep in Steps)
            {
                FlightStep theStep = thisStep.Value;

                var smooth = Drone.DroneConfig.SmoothSectionRadius;
                if ((smooth >= 1) && (Steps.Count > smooth * 2))
                    theStep.CalculateSettings_SmoothAltitude(smooth, Sections);
            }
        }


        // Calculate Step.LocationM, LinealM, SumLinealM, Yaw, DeltaYaw & Pitch by smoothing raw Section data 
        public void CalculateSettings_SmoothLocationYawPitch()
        {
            FlightStep prevStep = null;
            foreach (var thisStep in Steps)
            {
                FlightStep theStep = thisStep.Value;

                // Smooth the data  
                bool sensibleStep = true;
                var smooth = Drone.DroneConfig.SmoothSectionRadius;
                if ((smooth >= 1) && (Steps.Count > smooth * 2))
                    sensibleStep = theStep.CalculateSettings_SmoothLocationYawPitch(smooth, Sections);

                if (sensibleStep)
                {
                    theStep.CalculateSettings_LinealM(prevStep);
                    theStep.CalculateSettings_DeltaYawDeg(prevStep);
                }

                // Smoothing should not generate values much outside the original envelope
                float epsilon = 0.3f;
                float speed_epsilon = 0.5f; // Needed for D:\SkyComb\Data_Input\CC\2024-03-D\DJI_20240324153817_0001_T.SRT
                var theStepSpeed = theStep.SpeedMps;
                Assert(theStep.DroneLocnM.NorthingM <= Sections.MaxDroneLocnM.NorthingM + epsilon, "CalculateSettings_SmoothLocationYawPitch: Bad LocationM.NorthingM");
                Assert(theStep.DroneLocnM.EastingM <= Sections.MaxDroneLocnM.EastingM + epsilon, "CalculateSettings_SmoothLocationYawPitch: Bad LocationM.EastingM");
                Assert(theStep.TimeMs <= Sections.MaxTimeMs + epsilon, "CalculateSettings_SmoothLocationYawPitch: Bad TimeMs");
                Assert(theStep.LinealM <= Sections.MaxLinealM + epsilon, "CalculateSettings_SmoothLocationYawPitch: LinealM " + theStep.LinealM + " > " + Sections.MaxLinealM);
                Assert(theStepSpeed <= Sections.MaxSpeedMps + speed_epsilon, "CalculateSettings_SmoothLocationYawPitch: SpeedMps " + theStepSpeed + " > " + Sections.MaxSpeedMps);
                Assert(theStep.PitchDeg <= Sections.MaxPitchDeg + 1 + epsilon, "CalculateSettings_SmoothLocationYawPitch: MaxPitchDeg " + theStep.PitchDeg + " > " + Sections.MaxPitchDeg);
                Assert(theStep.PitchDeg >= Sections.MinPitchDeg - 1 - epsilon, "CalculateSettings_SmoothLocationYawPitch: MinPitchDeg " + theStep.PitchDeg + " < " + Sections.MinPitchDeg);

                float delta_yaw_epsilon = 3; // For example refer DJI_0120 step 207 where drone turns 80 degrees in ~1s and we have 5 sections
                Assert(theStep.DeltaYawDeg <= Sections.MaxDeltaYawDeg + delta_yaw_epsilon, "CalculateSettings_SmoothLocationYawPitch: MaxDeltaYawDeg " + theStep.DeltaYawDeg + " > " + Sections.MaxDeltaYawDeg);
                Assert(theStep.DeltaYawDeg >= Sections.MinDeltaYawDeg - delta_yaw_epsilon, "CalculateSettings_SmoothLocationYawPitch: MinDeltaYawDeg " + theStep.DeltaYawDeg + " < " + Sections.MinDeltaYawDeg);

                prevStep = theStep;
            }
        }


        // User has edited the drone settings including CameraDownDeg & OnGroundAt
        public void CalculateSettings_ConfigHasChanged(GroundData groundData, VideoData videoData)
        {
            // Calculate Step.AltitudeM by smoothing Section.AltitudeM
            CalculateSettings_SmoothAltitudeM();

            // Modify Step.AltitudeM using OnGroundAt info
            CalculateSettings_OnGroundAt(groundData);

            // Calculate CameraDownDeg, InputImageCenter and InputImageSizeM. Depends on AltitudeM, DemM & StepVelocityMps
            CalculateSettings_CameraDownDeg(videoData, groundData);

            // Update Altitude summary figures etc
            Steps.CalculateSettings_Summarise(this, Sections);
        }


        // From the provided data, calculate this objects summary settings (without using leg information).
        public void CalculateSettings(VideoData videoData, GroundData groundData)
        {
            try
            {
                // Add new Steps to match Sections one to one.
                foreach (var thisSection in Sections.Sections)
                    AddStep(new FlightStep(thisSection.Value));

                // Copy raw Section settings to Steps
                CopySectionToStepSettings();

                // Calculate Location, LinealM, SumLinealM, Yaw, DeltaYaw, Pitch. May smooth (average) raw data 
                CalculateSettings_SmoothLocationYawPitch();

                // Calculate Step.AltitudeM by smoothing Section.AltitudeM
                CalculateSettings_SmoothAltitudeM();

                FlightStep? prevStep = null;
                foreach (var thisStep in Steps)
                {
                    FlightStep theStep = thisStep.Value;

                    theStep.CalculateSettings_DemM(groundData);
                    theStep.CalculateSettings_DsmM(groundData);

                    prevStep = theStep;
                }

                // Modify Step.AltitudeM using OnGroundAt info
                CalculateSettings_OnGroundAt(groundData);

                // Calculate InputImageCenter and InputImageSizeM.
                // Depends on CameraDownDeg, AltitudeM, DemM & StepVelocityMps
                CalculateSettings_CameraDownDeg(videoData, groundData);

                Steps.CalculateSettings_Summarise(this, Sections);
                SetTardisMaxKey();

                CalculateSettings_AvgAndMinHeightM(groundData);
            }
            catch (Exception ex)
            {
                throw ThrowException("FlightSteps.CalculateSettings", ex);
            }
        }


        // For the Steps in each leg, refine the location settings.
        // A leg has ~ constant altitude, in a ~ constant direction for a significant duration 
        // and travels a significant distance. Pitch, Roll and Speed may NOT be mostly constant.         
        // Alters the LocationM, LinealM, SpeedMps, SumLinealM, StepVelocityMps, ImageVelocityMps, InputImageCenter & InputImageSizeM
        public void CalculateSettings_RefineLocationData(VideoData videoData, FlightLegs legs, GroundData? groundData)
        {
            /*
            if ((legs != null) && (legs.Legs.Count > 0))
                foreach (var leg in legs.Legs)
                {
                    // A leg is has ~ constant altitude, in a ~ constant direction for a significant duration
                    // and travels a significant distance. Pitch, Roll and Speed may NOT be mostly constant. 
                    // Time interval between Steps can also vary.

                    // Calculate the summary of this leg's steps before smoothing them
                    Steps.CalculateSettings_Summarise(this, leg.MinStepId, leg.MaxStepId);
                    FlightStepSummaryModel rawSummary = new();
                    rawSummary.CopySteps(this);

                    // If the drone is travelling at a reasonable speed then it has reasonable inertia,
                    // and the speed should be reasonably smooth, with avg and max speed being similar. 
                    // Edge case, in DJI_0198 leg 5 is the drone slowing from 5.4mps to 0mps with avg of 1.3m/s
                    if (rawSummary.AvgSpeedMps >= 1.5 && rawSummary.MaxSpeedMps < 2 * rawSummary.AvgSpeedMps)
                    {
                        // Use simple linear smoothing.
                        var minStep = Steps[leg.MinStepId];
                        var maxStep = Steps[leg.MaxStepId];
                        var minLocn = minStep.DroneLocnM;
                        var maxLocn = maxStep.DroneLocnM;
                        var deltaLocn = new RelativeLocation(
                            maxLocn.NorthingM - minLocn.NorthingM,
                            maxLocn.EastingM - minLocn.EastingM);
                        float deltaTime = maxStep.SumTimeMs - minStep.SumTimeMs;

                        FlightStep? prevStep = null;
                        for (int theStepId = leg.MinStepId + 1; theStepId <= leg.MaxStepId - 1; theStepId++)
                        {
                            Steps.TryGetValue(theStepId, out FlightStep? theStep);
                            if (theStep == null)
                                continue;

                            var fraction = (theStep.SumTimeMs - minStep.SumTimeMs) / deltaTime;
                            Assert(fraction > 0, "CalculateSettings_RefineLocationData: Fraction logic 1");
                            Assert(fraction < 1, "CalculateSettings_RefineLocationData: Fraction logic 2");

                            theStep.DroneLocnM = new(
                                minLocn.NorthingM + deltaLocn.NorthingM * fraction,
                                minLocn.EastingM + deltaLocn.EastingM * fraction);

                            // Recalculate the LinealM, SpeedMps, SumLinealM, StepVelocityMps, ImageVelocityMps, InputImageCenter & InputImageSizeM
                            theStep.CalculateSettings_RefineLocationData(videoData, prevStep, groundData);

                            prevStep = theStep;
                        }
                    }
                    else
                    {
                        // Use cubic spline smoothing for more complex edge cases
                        int arraySize = 0;
                        for (int theStepId = leg.MinStepId; theStepId <= leg.MaxStepId; theStepId++)
                        {
                            Steps.TryGetValue(theStepId, out FlightStep? theStep);
                            if ((theStep != null) && (theStep.DroneLocnM != null))
                                arraySize++;
                        }

                        float[] timeRaw = new float[arraySize];
                        float[] northingRaw = new float[arraySize];
                        float[] eastingRaw = new float[arraySize];

                        int arrayIndex = 0;
                        for (int theStepId = leg.MinStepId; theStepId <= leg.MaxStepId; theStepId++)
                        {
                            Steps.TryGetValue(theStepId, out FlightStep? theStep);
                            if ((theStep != null) && (theStep.DroneLocnM != null))
                            {
                                timeRaw[arrayIndex] = theStep.SumTimeMs;
                                northingRaw[arrayIndex] = theStep.DroneLocnM.NorthingM;
                                eastingRaw[arrayIndex] = theStep.DroneLocnM.EastingM;
                                arrayIndex++;
                            }
                        }

                        // Calculate a cubic spline smoothing of the Leg.
                        // We maintain the time sequence, as we have frames at these time intervals.
                        CubicSpline cubicSpline = new();
                        var northingSmooth = cubicSpline.FitAndEval(timeRaw, northingRaw, timeRaw);
                        var eastingSmooth = cubicSpline.FitAndEval(timeRaw, eastingRaw, timeRaw);


                        // Recalculate the Steps Locations, Distance Traveled and Speed
                        arrayIndex = 0;
                        Steps.TryGetValue(leg.MinStepId - 1, out FlightStep? prevStep);
                        for (int theStepId = leg.MinStepId; theStepId <= leg.MaxStepId; theStepId++)
                        {
                            Steps.TryGetValue(theStepId, out FlightStep? theStep);
                            if ((theStep != null) && (theStep.DroneLocnM != null))
                            {
                                theStep.DroneLocnM.NorthingM = northingSmooth[arrayIndex];
                                theStep.DroneLocnM.EastingM = eastingSmooth[arrayIndex];

                                // Recalculate the LinealM, SpeedMps, SumLinealM, StepVelocityMps, ImageVelocityMps, InputImageCenter & InputImageSizeM
                                theStep.CalculateSettings_RefineLocationData(videoData, prevStep, groundData);

                                arrayIndex++;
                                prevStep = theStep;
                            }
                        }

                        // As last leg step may have moved, update (non-Location data) one step past the leg.
                        Steps.TryGetValue(leg.MaxStepId + 1, out FlightStep? nextStep);
                        if ((nextStep != null) && (prevStep != null))
                            // Recalculate the LinealM, SpeedMps, SumLinealM, StepVelocityMps, ImageVelocityMps, InputImageCenter & InputImageSizeM
                            nextStep.CalculateSettings_RefineLocationData(videoData, prevStep, groundData);
                    }

                    // Calculate the summary of this leg's steps after smoothing them
                    Steps.CalculateSettings_Summarise(this, leg.MinStepId, leg.MaxStepId);
                    // Check that this smoothing has not changed the data envelope 
                    AssertGoodStepRevision(rawSummary);
                }
            */

            // Calculate the summary of all (leg and non-leg) steps.
            // Check that it is a reasonable revision of the sections.
            Steps.CalculateSettings_Summarise(this, Sections);
        }


        public void AssertGood()
        {
            Assert(FileName != "", "FlightSteps.AssertGood: No FileName");
            Assert(Steps.Count != 0, "FlightSteps.AssertGood: No Steps");
        }


        // Add a FlightStep linked to a FlightSection 
        public void AddStep(FlightStep theSection)
        {
            Steps.Add(theSection.FlightSection.TardisId, theSection);
            SetTardisMaxKey();
        }


        // The flight data sometimes has a gap or say 1.5s without video or flight data.
        // So if asked for an "out of range" stepId, we return the closest stepId.
        public FlightStep? StepIdToNearestFlightStep(int stepID)
        {
            FlightStep? answer;

            if (Steps.TryGetValue(stepID, out answer))
                return answer;

            // If we didn't find a stepId at the exact time, find the closest stepId
            for (int i = 1; i <= 8; i++)
            {
                if (Steps.TryGetValue(stepID + i, out answer))
                    return answer;

                if (Steps.TryGetValue(stepID - i, out answer))
                    return answer;
            }

            // We have now tried -8 to +8 slots. Give up.
            return null;
        }


        // Return the FlightStep that is closest to the specified flightMs
        public FlightStep? MsToNearestFlightStep(int flightMs)
        {
            if (Steps.Count == 0)
                return null;

            if (flightMs <= Steps[0].SumTimeMs)
                return Steps[0];

            if (flightMs >= Steps[Sections.MaxTardisId].SumTimeMs)
                return Steps[Sections.MaxTardisId];

            FlightStep? nearestStep = null;
            int nearestDelta = int.MaxValue;

            // PQR ToDo Increase speed of this loop by bisection of the sorted list of steps
            for (int stepId = MinStepId; stepId <= MaxStepId; stepId++)
            {
                FlightStep? thisStep;
                if (Steps.TryGetValue(stepId, out thisStep))
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


        // Return the FlightStep with a SumTimeMs at or lower than flightMs
        public FlightStep? FlightStepAtOrBeforeFlightMs(FlightStep hintStep, int flightMs)
        {
            int hintId = hintStep.StepId;

            FlightStep answer;
            // We've seen flights with 1.8s gaps between steps.
            for (int i = 8; i >= -8; i--)
                if (Steps.TryGetValue(hintId + i, out answer))
                    if (answer.SumTimeMs <= flightMs)
                        return answer;

            return null;
        }
        // Return the FlightStep with a SumTimeMs at or lower than flightMs. Slow but accurate
        public FlightStep? FlightStepAtOrBeforeFlightMs(int flightMs)
        {
            FlightStep? answer = null;

            foreach (var step in Steps)
            {
                if (step.Value.SumTimeMs > flightMs)
                    break;

                answer = step.Value;
            }

            return answer;
        }

        // Returns percentage of FlightSteps where the drone AltitudeM is less than ground DemM.
        // A good answer is 0. A bad OnGroundAt value can mean a value of say 45%
        public float PercentAltitudeLessThanDem()
        {
            if (Steps.Count == 0)
                return 0.0f;

            int badSteps = 0;
            foreach (var step in Steps)
                if (step.Value.DemM - step.Value.AltitudeM > 1) // Allow 1m error in AltitudeM
                    badSteps++;

            return 100 * badSteps / Steps.Count;
        }
    }
}
