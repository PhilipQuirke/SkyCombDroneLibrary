// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundSpace;
using SkyCombGround.PersistModel;
using System.Diagnostics;
using System.Drawing;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    // Drone is the interface that video runners must use. 
    // Contains video(s), flight log(s), ground & surface elevations (if any), and calculated data.
    // This class is NOT dependent on ProcessLogic or RunSpace, so is constant per "set of input files" and can be cached & re-used.
    //
    // This class can load data from 1 to 4 files supporting several use cases including:
    //
    // 1. THERMAL VIDEO ONLY
    // An owner of a Autel Evo 2 640T provided a thermal video (but no flight log).
    //      IRX_0009.mp4 - the thermal video
    //
    // 2. OPTICAL VIDEO PLUS FLIGHT LOG
    // A DJI Mini only has a optical camera. Taking an video creates 2 files:
    //      DJI_0020.mp4 - the optical video
    //      DJI_0020.srt - DJI-specific text file containing location and orientation data, etc.
    //
    // 3. THERMAL VIDEO PLUS FLIGHT LOG
    // A drone creating a thermal video and a flight log
    //
    // 4. OPTICAL AND THERMAL VIDEOS PLUS FLIGHT LOGS (Recommended)
    // A DJI M2E Dual has optical and thermal cameras. Taking a video creates 4 files in this order:
    //      DJI_0119.mp4 - the optical video
    //      DJI_0119.srt - a DJI-specific SRT file with basic data and extra optical-camera settings
    //      DJI_0120.mp4 - the thermal video
    //      DJI_0120.srt - a DJI-specific SRT file with basic data (location, orientation, etc)
    public class Drone : TwoVideos
    {
        public DroneConfigModel Config;


        // Time to load / calculate this object 
        public EffortDurations EffortDurations { get; set; }


        // The primary input flight data to process (if any). Includes drone location and altitude over time data.
        public FlightSections? FlightSections { get; set; }

        // The calculated step data (if any) derived from the input flight data
        public FlightSteps? FlightSteps { get; set; }

        // The calculated leg data (if any) derived from the input flight data
        public FlightLegs? FlightLegs { get; set; }

        // The secondary display camera drone flight information (if any)
        public FlightSections? DisplaySections { get; set; }

        // Ground (aka DEM) and surface (aka DSM) elevation (if any), in meters above sea level.
        // Covers area corresponding to the drone flight log plus a 20m buffer.
        public GroundData? GroundData { get; set; }



        public bool HasFlightSections { get { return (FlightSections != null) && FlightSections.Sections.Count > 0; } }
        public bool HasFlightSteps { get { return (FlightSteps != null) && FlightSteps.Steps.Count > 0; } }
        public bool HasFlightLegs { get { return (FlightLegs != null) && FlightLegs.Legs.Count > 0; } }
        public bool HasDroneSpeed { get { return HasFlightSteps && FlightSteps.MaxSpeedMps != UnknownValue; } }
        public bool HasDroneAltitude { get { return HasFlightSections && FlightSections.MaxAltitudeM != UnknownValue; } }
        public bool HasDronePitch { get { return HasFlightSteps && FlightSteps.MaxPitchDeg != UnknownValue; } }
        public bool HasDroneYaw { get { return HasFlightSteps && FlightSteps.MaxDeltaYawDeg != UnknownValue; } }
        public bool HasDroneRoll { get { return HasFlightSteps && FlightSteps.MaxRollDeg != UnknownValue; } }
        public bool HasDisplaySections { get { return DisplaySections != null; } }
        public bool HasGroundData { get { return (GroundData != null) && (GroundData.DemGrid != null) && (GroundData.DemGrid.NumElevationsStored > 0); } }


        
        public Drone(DroneConfigModel config)
        {
            Config = config;
            EffortDurations = new();
            ClearData_Flight();
            ClearData_Video();
            ClearData_Ground();
        }


        public void ClearData_Flight()
        {
            FlightSections = null;
            FlightSteps = null;
            FlightLegs = null;
            DisplaySections = null;
        }


        public void ClearData_Ground()
        {
            GroundData = null;
        }


        // Load video(s) objects
        public bool LoadSettings_Videos(DroneDataStore dataStore, Func<string, DateTime> readDateEncodedUtc)
        {
            try
            {
                DroneLoad dataReader = new(dataStore, this);
                dataStore.SelectWorksheet(DroneDataStore.FilesTabName);

                // Without at least one video we can't do anything
                if (dataStore.ThermalVideoName != "")
                {
                    InputVideo = new VideoData(dataStore.ThermalVideoName, true, readDateEncodedUtc);
                    if (dataStore.OpticalVideoName != "")
                        DisplayVideo = new VideoData(dataStore.OpticalVideoName, false, readDateEncodedUtc);
                }
                else
                {
                    if (dataStore.OpticalVideoName != "")
                        InputVideo = new VideoData(dataStore.OpticalVideoName, false, readDateEncodedUtc);
                }

                if (HasInputVideo)
                    return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Suppressed Drone.LoadSettings_Videos failure: " + ex.ToString());
            }

            ClearData_Video();
            return false;
        }


        // Load existing drone flight (if any) from the DataStore 
        public bool LoadSettings_Flight(DroneDataStore dataStore)
        {
            int phase = 0;
            try
            {
                if (dataStore.SelectWorksheet(DroneDataStore.DroneTabName))
                {
                    DroneLoad dataReader = new(dataStore, this);

                    // Load the summary (settings) data 
                    phase = 1;
                    dataReader.UserInputSettings(Config);
                    dataReader.LegSettings(Config);
                    dataReader.EffortSettings();

                    phase = 2;
                    if (dataStore.ThermalVideoName != "")
                    {
                        FlightSections = dataReader.LoadSettings(
                            dataStore.ThermalVideoName, InputVideo,
                            dataStore.ThermalFlightName,
                            DroneLoad.MidColOffset);

                        if (dataStore.OpticalVideoName != "")
                            DisplaySections = dataReader.LoadSettings(
                                dataStore.OpticalVideoName, DisplayVideo,
                                dataStore.OpticalFlightName,
                                DroneLoad.RhsColOffset);
                    }
                    else
                        FlightSections = dataReader.LoadSettings(
                            dataStore.OpticalVideoName, InputVideo,
                            dataStore.OpticalFlightName,
                            DroneLoad.MidColOffset);

                    phase = 3;
                    FlightSteps = new(this, dataReader.FlightStepsSettings());
                    FlightSteps.FileName = (HasThermalVideo ? dataStore.ThermalFlightName : dataStore.OpticalFlightName);

                    FlightLegs = new();


                    // Load the FlightSections (if any)
                    phase = 4;
                    if (dataStore.SelectWorksheet(DataConstants.Sections1TabName))
                    {
                        dataReader.FlightSections(FlightSections);

                        if (HasFlightSections)
                            FlightSections.AssertGood();
                    }


                    // Load FlightSteps (if any)
                    phase = 5;
                    if (dataStore.SelectWorksheet(DataConstants.Steps1TabName))
                    {
                        dataReader.FlightSteps(FlightSections, FlightSteps);
                        FlightSteps.AssertGood();
                    }


                    // Load FlightLegs (if any)
                    phase = 6;
                    if (dataStore.SelectWorksheet(DataConstants.Legs1TabName))
                    {
                        dataReader.FlightLegs(FlightLegs, HasFlightSteps);
                        FlightLegs.AssertGood(HasFlightSteps);
                    }
                    phase = 7;
                    FlightLegs.Set_FlightStep_FlightLeg(FlightSteps);


                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Suppressed Drone.LoadSettings_Flight failure (Phase =" + phase + "):" + ex.ToString());
            }

            ClearData_Flight();
            return false;
        }


        // Load ground data (if any) from the DataStore 
        public bool LoadSettings_Ground(DroneDataStore dataStore)
        {
            GroundData = GroundLoad.Load(dataStore);

            return HasGroundData;
        }


        // Calculate video settings
        public void CalculateSettings_Video()
        {
            if (HasThermalVideo)
                ThermalVideo.CalculateSettings();
            if (HasOpticalVideo)
                OpticalVideo.CalculateSettings();


            Drone_DJI.SetCameraHFOV(this);
            // Add other drone manufacturer specific SetCameraHFOV calls here. PQR TODO
        }


        // Calculate FlightSections settings by parsing the flight logs (if any)
        public void CalculateSettings_FlightSections()
        {
            FlightSections = null;
            DisplaySections = null;
            LoadFlightDataFromTextFile(ThermalVideo);
            LoadFlightDataFromTextFile(OpticalVideo);
        }


        // Calculate ground and surface elevations
        public void CalculateSettings_Ground(string groundDirectory)
        {
            if (HasFlightSections)
            {
                GroundData = GroundDataFactory.Create();
                GroundData.GlobalCalculateElevations(
                    FlightSections.MinGlobalLocation, 
                    FlightSections.MaxGlobalLocation,
                    groundDirectory);
            }
        }


        // Validate the OnGroundAt setting
        public bool CalculateSettings_OnGroundAt_IsValid()
        {
            if ((Config.OnGroundAt == OnGroundAtEnum.Neither) ||
                (Config.OnGroundAt == OnGroundAtEnum.Auto))
                return true;

            // The Config.OnGroundAt value is either Start, End or Both. Is this reasonable?

            if (HasGroundData && HasFlightSteps)
            {
                // If Ground elevation range is say 20m, and drone altitude range is <= 20,
                // then "Both", "Start" & "End" are all counter-indicated.
                if (FlightSteps.MaxDemM - FlightSteps.MinDemM >
                    FlightSteps.MaxAltitudeM - FlightSteps.MinAltitudeM)
                    return false;

                // If drone altitude < ground elevation say 10 % of time
                // then "Both", "Start" & "End" are all counter-indicated.
                if (FlightSteps.PercentAltitudeLessThanDem() > 10)
                    return false;
            }

            return true;
        }


        // Calculate flight steps and legs
        public void CalculateSettings_StepsAndLegs()
        {
            if (HasFlightSections)
            {
                // Calculate the flight steps settings (without leg information)
                FlightSteps = new(this);
                FlightSteps.CalculateSettings(InputVideo, GroundData);
                FlightSteps.AssertGood();

                // Calculate the flight legs Min/MaxStepIds
                FlightLegs = new();
                FlightLegs.Calculate_Pass1(FlightSections, FlightSteps, Config);

                // Refine the flight steps settings using leg information
                FlightSteps.CalculateSettings_RefineLocationData(InputVideo, FlightLegs);

                FlightLegs.Calculate_Pass2(FlightSteps);
                FlightLegs.AssertGood(HasFlightSteps);
            }
            else
            {
                FlightLegs = new();
                FlightLegs.Calculate_NoFlightData(Config);
            }
        }


        // Update SwatheGrid with area seen by the input video over specified steps
        public void CalculateSettings_SwatheSeen(int minStepId, int maxStepId)
        {
            try
            {
                if ((GroundData == null) || (GroundData.DemGrid == null) || ! GroundData.DemGrid.HasElevationData())
                    return;

                if (HasInputVideo && HasFlightSections && HasGroundData)
                {
                    GroundData.SwatheGrid = new(GroundData.DemGrid);

                    // For each flight step, calculate the part of the grid seen
                    for (int stepId = minStepId; stepId <= maxStepId; stepId++)
                    {
                        FlightSteps.Steps.TryGetValue(stepId, out var step);
                        if (step == null)
                            continue;

                        // We need an image area to continue.
                        // For example, if camera is pointing near horizontal the image area
                        // is useless for analysis purposes, and doesnt have a InputImageSizeM
                        if (step.InputImageSizeM == null)
                            continue;

                        // Get corners of area covered by the step's video image (may be forward of drone's location).
                        // This rectangle is commonly rotated relative to the X/Y axises.
                        var (topLeftLocn, topRightLocn, bottomRightLocn, bottomLeftLocn) =
                            step.Calculate_InputImageArea_Corners();

                        // Update the area as "seen"
                        GroundData.SwatheGrid.DroneRectSeen(topLeftLocn, topRightLocn, bottomRightLocn, bottomLeftLocn);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("Drone.CalculateSettings_SwatheSeen: " + ex.Message);
            }
        }


        public void SaveSettings(DroneDataStore dataStore, Bitmap? countryBitmap)
        {
            var effort = Stopwatch.StartNew();

            // Save the input data to the datastore. Will be used as a data cache for future runs.
            // Fails if OutputElseInputDirectory does not exist, or user is editing the datastore.
            dataStore.Open();

            GroundSave.Save(dataStore, GroundData, true);

            DroneSave datawriter = new(dataStore, this);
            datawriter.SaveData_Summary(countryBitmap);
            datawriter.SaveData_Detail(true, effort);

            dataStore.Close();
        }


        public string DescribeFlightPath { get { 
            string answer = "";

            if (HasFlightSections)
                answer += FlightSections.DescribePath;

            if (HasFlightLegs)
                answer += FlightLegs.DescribeLegs;

            if (HasFlightSteps)
                answer += FlightSteps.DescribeLinealM;

            return answer;
        } }


        // Return the FlightStep that is closest to the specified flightMs 
        public FlightStep? MsToNearestFlightStep(int flightMs)
        {
            const int smallMs = 100;

            if (!HasFlightSteps)
                return null;

            // Often processing starts and ends aligned to flight legs
            // Flight legs contain the starting and ending FlightSteps Ids
            // So for speed...
            if (HasFlightLegs)
                foreach (var leg in FlightLegs.Legs)
                {
                    if (Math.Abs(leg.MinSumTimeMs - flightMs) < smallMs)
                        return FlightSteps.Steps[leg.MinStepId];

                    if (Math.Abs(leg.MaxSumTimeMs - flightMs) < smallMs)
                        return FlightSteps.Steps[leg.MaxStepId];

                    if ((leg.MinSumTimeMs < flightMs) && (leg.MaxSumTimeMs > flightMs))
                        return leg.MsToNearestFlightStep(flightMs, FlightSteps.Steps);
                }

            // Just as accurate but slower.
            return FlightSteps.MsToNearestFlightStep(flightMs);
        }


        // After selecting/loading a file and its settings, user has edited the drone settings.
        // The new settings have been loaded into the config objects. Update our data accordingly.
        // The changed settings (from MainFormSettingsToConfig) are:
        // - ThermalToOpticalVideoDelayS is only used in GetFrames, so next run will obey new setting.
        // - CameraDownDeg is used in DrawSpace.
        // - OnGroundAt is used in CalculateSettings_OnGroundAt to calculate DroneToGroundAltStartSyncM, DroneToGroundAltEndSyncM, FlightStep.AltitudeM
        public void CalculateSettings_ConfigHasChanged()
        {
            Config.ValidateCameraDownDeg();

            if (Config.GimbalDataAvail != GimbalDataEnum.ManualNo)
            {
                // These "sanity checks" are only needed if we do not have gimbal data
                Config.MaxLegStepPitchDeg = 95; // Degrees
                Config.MaxLegSumPitchDeg = 95; // Degrees
            }

            if (HasFlightSteps)
                // Alter FlightStep.AltitudeM
                FlightSteps.CalculateSettings_ConfigHasChanged(GroundData, InputVideo);
        }


        // Convert from a SectionID to the Video position in Ms.
        public int SectionIdToVideoMs(int sectionId)
        {
            if ((!HasFlightSections) || (!HasFlightSteps))
                return 0;

            return FlightSteps.StepIdToNearestFlightStep(sectionId).SumTimeMs;
        }


        // Set the RunFromS and RunToS config values to specified values
        public void SetConfigRunFromTo(int startMs, int endMs)
        {
            Config.RunVideoFromS = startMs / 1000.0f;
            Config.RunVideoToS = endMs / 1000.0f;
        }


        // Set the RunFromS and RunToS config values based on the start/end step Ids.
        public void SetConfigRunFromToBySection(int startSectionId, int endSectionId)
        {
            if (HasFlightSteps)
            {
                SetConfigRunFromTo(
                    SectionIdToVideoMs(startSectionId),
                    SectionIdToVideoMs(endSectionId));

                // Calculate swathe seen by the input video over specified steps
                CalculateSettings_SwatheSeen(startSectionId, endSectionId);
            }
        }


        // Default the RunFromS and RunToS config values 
        public void DefaultConfigRunFromTo()
        {
            int maxStepId = FlightSteps.MaxStepId;

            if (HasFlightLegs && ( FlightLegs.LegPercentage(maxStepId) > 33))
                // If the flight is more than 1/3 legs, use the first and last legs to default the Run From/To.
                // This is the "interesting" part of the flight that the Flow and Comb processes are best applied to.
                SetConfigRunFromToBySection(
                    FlightLegs.Legs[0].MinStepId,
                    FlightLegs.Legs[^1].MaxStepId);
            else if (HasInputVideo)
            {
                // Default the RunFrom/To to the full video length
                Config.RunVideoFromS = 0;
                Config.RunVideoToS = InputVideo.DurationMs / 1000.0f;

                // Calculate swathe seen by the input video over specified steps
                CalculateSettings_SwatheSeen(1, maxStepId);
            }
        }


        // Reset input AND display video frame position & load image(s)
        public void SetAndGetCurrFrames(int inputFrameId)
        {
            SetAndGetCurrFrames(inputFrameId, (int)(Config.ThermalToOpticalVideoDelayS * 1000));
        }


        // Get (advance to) the next frame of the video(s)
        public bool GetNextFrames()
        {
            return GetNextFrames((int)(Config.ThermalToOpticalVideoDelayS * 1000));
        }


        static DateTime RoundToHour(DateTime dt)
        {
            long ticks = dt.Ticks + 18000000000;
            return new DateTime(ticks - ticks % 36000000000, dt.Kind);
        }


        public int PercentFlightOverlap { get { return FlightSections.PercentOverlap(FlightSections, DisplaySections); } }


        // The thermal and optical flight data files may not start at the same millisecond.
        public int FlightStartOffsetMs()
        {
            if (PercentFlightOverlap <= 0)
                return BaseConstants.UnknownValue;

            return (int)FlightSections.MinDateTime.Subtract(DisplaySections.MinDateTime).TotalMilliseconds;
        }


        // Load flight data file associated with the video
        private void LoadFlightDataFromTextFile(VideoData theVideoData)
        {
            if (theVideoData == null)
                return;

            // Try to load the flight log from an DJI SRT text file
            var flightData = new FlightSections();
            (bool success, GimbalDataEnum cameraPitchYawRoll) =
                new Drone_DJI().LoadFlightLogSections(theVideoData, flightData, this);

            if (!success)
            {
                // Try to load the flight log from a second drone manufacturer's flight log file
                flightData = new FlightSections();
                success = false; // PQR TODO
            }

            if (!success)
                return;

            Config.GimbalDataAvail = cameraPitchYawRoll;
            flightData.CalculateSettings();
            flightData.AssertGood();

            if (flightData.Sections.Count > 0)
            {
                // Convert DateEncodedUtc to DateEncoded
                // We can't assume this code is being run in the same location the video was recorded.
                // We know the flight data and video were taken at the same time and location.
                // The flight data contains local date times. 
                // So we can use this delta to convert DateEncodedUtc to DateEncoded
                var utcToLocal = RoundToHour(flightData.MinDateTime).Subtract(RoundToHour(theVideoData.DateEncodedUtc));
                theVideoData.DateEncoded = theVideoData.DateEncodedUtc.AddHours(utcToLocal.Hours);
                // Note that flightData.MinDateTime and theVideoData.DateEncoded may differ by a few seconds.

                if (theVideoData.Thermal)
                    FlightSections = flightData;
                else
                {
                    if (FlightSections == null)
                        FlightSections = flightData;
                    else
                        DisplaySections = flightData;
                }
            }
        }


        // After selecting/loading a file and its settings, user has edited the drone settings.
        // The new settings have been loaded into the config objects. Update our drone data accordingly.
        public void WriteDataStore(DroneDataStore dataStore, Bitmap? countryBitmap)
        {
            // We need to update the Drone datastore
            dataStore.Open();

            GroundSave.Save(dataStore, GroundData, false);

            DroneSave datawriter = new(dataStore, this);
            datawriter.SaveData_Summary(countryBitmap);
            datawriter.SaveData_Detail(false);
            dataStore.Close();
        }


        public string GoogleMapsLink()
        {
            if (HasFlightSections && (FlightSections.MaxGlobalLocation != null))
            {
                var latLong =
                    FlightSections.MaxGlobalLocation.Latitude.ToString() + "," +
                    FlightSections.MaxGlobalLocation.Longitude.ToString();

                return
                    @"https://www.google.com/maps?q=" + latLong +
                    @"&ll=" + latLong + @"&z=10";
            }

            return "";
        }


        // Get the drone settings needed to describe the flight in the SkyCombFLights app
        public DataPairList GetSettingsForSkyCombFlights()
        {
            string latitude = "";
            string longitude = "";
            string countryX = "";
            string countryY = "";
            string eastingM = "";
            string northingM = "";

            if(HasFlightSections && (FlightSections.MaxGlobalLocation != null))
            {
                latitude = FlightSections.MaxGlobalLocation.Latitude.ToString();
                longitude = FlightSections.MaxGlobalLocation.Longitude.ToString();
            }
                    
            if(HasFlightSections)
            {
                var max = FlightSections.MaxCountryLocation;
                var min = FlightSections.MinCountryLocation;
                if ((max != null) && (min != null))
                {
                    countryX = max.EastingM.ToString();
                    countryY = max.NorthingM.ToString();
                    eastingM = ((int)(max.EastingM - min.EastingM)).ToString();
                    northingM = ((int)(max.NorthingM - min.NorthingM)).ToString(); 
                }
            }

            return new DataPairList
            {
                { "DateTime", (HasFlightSections ? FlightSections.MinDateTime.ToString(ShortDateFormat) : "") },
                { "Duration", (HasInputVideo ? InputVideo.DurationMsToString(0) : "") },
                { "Latitude", latitude },
                { "Longitude", longitude },
                { "Country X", countryX },
                { "Country Y", countryY },
                { "East M", eastingM },
                { "North M", northingM },
                { "DEM %", (HasGroundData && (GroundData.DemGrid != null) ? GroundData.DemGrid.PercentDatumElevationsAvailable.ToString() : "") },
                { "DSM %", (HasGroundData && (GroundData.DsmGrid != null) ? GroundData.DsmGrid.PercentDatumElevationsAvailable.ToString() : "") },
                { "Google Maps", GoogleMapsLink() },
            };
        }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "% Flight overlap", PercentFlightOverlap },
                { "% Video overlap", PercentVideoOverlap },
                { "Flight start diff (ms)", FlightStartOffsetMs() },
                { "Video start diff (ms)", VideoStartOffsetMs() },
                { "Thermal Video start", ( HasThermalVideo ? ThermalVideo.DateEncoded.ToString(DateFormat) : "" )},
                { "Thermal Flight start", ( HasFlightSections ? FlightSections.MinDateTime.ToString(DateFormat) : "" )},
                { "Optical Video start", ( HasOpticalVideo ? OpticalVideo.DateEncoded.ToString(DateFormat) : "" ) },
                { "Optical Flight start", ( HasDisplaySections ? DisplaySections.MinDateTime.ToString(DateFormat) : "" )},
            };
        }

    }
}


