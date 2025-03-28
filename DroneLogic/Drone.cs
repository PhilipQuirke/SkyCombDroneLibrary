﻿// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundLogic;
using SkyCombGround.PersistModel;
using System.Diagnostics;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    // Drone is the interface that video runners must use. 
    // Contains video(s), flight log(s), ground & surface elevations (if any), and calculated data.
    // Normal use case is a drone creating a thermal video and a flight log.
    public class Drone : OneVideo, IDisposable
    {
        public DroneConfigModel DroneConfig;


        // Time to load / calculate this object 
        public EffortDurations EffortDurations { get; set; }


        // The primary input flight data to process (if any). Includes drone location and altitude over time data.
        public FlightSections? FlightSections { get; set; }

        // The calculated step data (if any) derived from the input flight data
        public FlightSteps? FlightSteps { get; set; }

        // The calculated leg data (if any) derived from the input flight data
        public FlightLegs? FlightLegs { get; set; }


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
        public bool HasDroneFocalLength { get { return HasFlightSteps && FlightSteps.MaxFocalLength != UnknownValue; } }
        public bool HasDroneZoom { get { return HasFlightSteps && FlightSteps.MaxZoom != UnknownValue; } }
        public bool HasGroundData { get { return (GroundData != null) && (GroundData.DemModel != null) && (GroundData.DemModel.NumElevationsStored > 0); } }


        // Do we use the flight leg information?
        public bool UseFlightLegs { get { return HasFlightLegs && DroneConfig.UseLegs && FlightLegs.Legs.Count > 0; } }
        // How many legs to show in the UI
        public int NumLegsShown { get { return UseFlightLegs ? FlightLegs.Legs.Count : 0; } }


        // Some drone steps we do not use (aka process) as the thermal camera is pointing too near the horizontal
        public bool FlightStepInRunScope(FlightStep flightStep)
        {
            return ((!DroneConfig.UseGimbalData) || (Math.Abs(flightStep.PitchDeg) >= DroneConfig.MinCameraDownDeg));
        }


        public Drone(DroneConfigModel config)
        {
            DroneConfig = config;
            EffortDurations = new();
            FreeResources();
        }


        // Clear video file handles etc. More immediate than waiting for garbage collection
        public void FreeResources()
        {
            FreeResources_Video();
            FreeResources_Flight();
            FreeResources_Ground();
        }


        public void FreeResources_Flight()
        {
            FlightSections = null;
            FlightSteps = null;
            FlightLegs = null;
        }


        public void FreeResources_Ground()
        {
            GroundData?.Dispose();
            GroundData = null;
        }


        // Load video(s) objects
        public bool LoadSettings_Videos(DroneDataStore dataStore, Func<string, DateTime> readDateEncodedUtc)
        {
            try
            {
                DroneLoad dataReader = new(dataStore, this);
                dataStore.SelectWorksheet(DroneDataStore.FileSettingsTabName);

                // Without a video we can't do anything
                if (dataStore.ThermalVideoName != "")
                    InputVideo = new VideoData(dataStore.ThermalVideoName, readDateEncodedUtc);

                if (HasInputVideo)
                    return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Suppressed Drone.LoadSettings_Videos failure: " + ex.ToString());
            }

            FreeResources_Video();
            return false;
        }


        // Load existing drone flight (if any) from the DataStore 
        public bool LoadSettings_Flight(DroneDataStore dataStore, bool fullLoad = true)
        {
            int phase = 0;
            try
            {
                if (dataStore.SelectWorksheet(DroneDataStore.DroneSettingsTabName))
                {
                    DroneLoad dataReader = new(dataStore, this);

                    // Load the summary (settings) data 
                    phase = 1;
                    dataReader.UserInputSettings(DroneConfig);
                    dataReader.LegSettings(DroneConfig);
                    dataReader.EffortSettings();

                    phase = 2;
                    if (dataStore.ThermalVideoName != "")
                        FlightSections = dataReader.LoadSettings(
                            dataStore.ThermalVideoName, InputVideo,
                            dataStore.ThermalFlightName,
                            DroneLoad.MidColOffset);


                    phase = 3;
                    FlightSteps = new(this, dataReader.FlightStepsSettings());
                    FlightSteps.FileName = dataStore.ThermalFlightName;

                    FlightLegs = new();


                    // Load the FlightSections (if any)
                    phase = 4;
                    if (fullLoad && dataStore.SelectWorksheet(DataConstants.SectionDataTabName))
                    {
                        dataReader.FlightSections(FlightSections);

                        if (HasFlightSections)
                            FlightSections.AssertGood();
                    }


                    // Load FlightSteps (if any)
                    phase = 5;
                    if (fullLoad && dataStore.SelectWorksheet(DataConstants.StepDataTabName))
                    {
                        dataReader.FlightSteps(FlightSections, FlightSteps);
                        FlightSteps.AssertGood();
                    }


                    // Load FlightLegs (if any)
                    phase = 6;
                    if (fullLoad && dataStore.SelectWorksheet(DataConstants.LegDataTabName))
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

            FreeResources_Flight();
            return false;
        }


        // Load ground data (if any) from the DataStore 
        public bool LoadSettings_Ground(DroneDataStore droneDataStore, bool fullLoad)
        {
            GroundData = GroundLoad.Load(droneDataStore, fullLoad);

            return HasGroundData;
        }


        // Calculate video settings
        public void CalculateSettings_Video()
        {
            if (HasInputVideo)
                InputVideo.CalculateSettings();
        }


        // Calculate FlightSections settings by parsing the flight logs (if any). Updates Drone.CameraType
        public void CalculateSettings_FlightSections()
        {
            FlightSections = null;
            LoadFlightDataFromTextFile(InputVideo);
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
            if ((DroneConfig.OnGroundAt == OnGroundAtEnum.Neither) ||
                (DroneConfig.OnGroundAt == OnGroundAtEnum.Auto))
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
                FlightLegs.Calculate_Pass1(FlightSections, FlightSteps, DroneConfig);

                // Do we default to using legs? User can override in UI.
                // We use legs if the total leg distance is > 33% of the total flight distance
                DroneConfig.UseLegs =
                    (FlightSections.Sections.Count > 200) &&
                    (FlightLegs.Legs.Count > 2);
                if (DroneConfig.UseLegs)
                {
                    var legsLinealM = FlightLegs.SumLinealM();
                    var sectionsLinealM = FlightSections.Sections.Last().Value.SumLinealM;
                    DroneConfig.UseLegs = (legsLinealM / sectionsLinealM > 0.33f);
                }

                // Refine the flight steps settings using leg information
                FlightSteps.CalculateSettings_RefineLocationData(InputVideo, FlightLegs, GroundData);

                FlightLegs.Calculate_Pass3(FlightSteps);
                FlightLegs.AssertGood(HasFlightSteps);
            }
            else
            {
                FlightLegs = new();
                FlightLegs.Calculate_NoFlightData(DroneConfig);

                DroneConfig.UseLegs = false;
            }
        }


        // Calculate swathe seen by the input video over specified steps
        public void CalculateSettings_SwatheSeen(int minStepId, int maxStepId)
        {
            try
            {
                if ((GroundData == null) ||
                    (GroundData.DemModel == null) ||
                    (!GroundData.DemModel.HasElevationData()) ||
                    (!HasInputVideo) ||
                    (!HasFlightSections) ||
                    (!HasGroundData))
                    return;

                GroundData.SwatheModel = new(GroundData.DemModel);

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
                    GroundData.SwatheModel.DroneRectSeen(topLeftLocn, topRightLocn, bottomRightLocn, bottomLeftLocn);
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("Drone.CalculateSettings_SwatheSeen: " + ex.Message);
            }
        }


        public void SaveSettings(DroneDataStore dataStore, bool firstSave)
        {
            var effort = Stopwatch.StartNew();

            // Save the input data to the datastore. Will be used as a data cache for future runs.
            // Fails if OutputElseInputDirectory does not exist, or user is editing the datastore.
            dataStore.Open();

            GroundSave.Save(dataStore, GroundData);

            DroneSave datawriter = new(dataStore, this);
            datawriter.SaveDroneSettings(firstSave);
            datawriter.SaveData_Detail(true, effort);

#if DEBUG
            // Check that the Flight DEM and DSM values align with the Ground data.
            DroneDataFactory.SanityCheckGroundElevationData(this, GroundData);

            // Check that the DEM and DSM ground data values survive the round trip.
            GroundData reloadedGroundData = SkyCombGround.PersistModel.GroundCheck.GroundData_RoundTrip_PreservesElevationsWithinTolerance(GroundData, dataStore.DataStoreFileName);
            // Check that the Flight DEM and DSM values align with the (compacted, stored, loaded, uncompacted) Ground data.
            DroneDataFactory.SanityCheckGroundElevationData(this, reloadedGroundData);
            reloadedGroundData.Dispose();
            dataStore.FreeResources();
#endif
        }


        public string DescribeFlightPath
        {
            get
            {
                string answer = "";

                if (HasFlightSections)
                    answer += FlightSections.DescribePath;

                if (HasFlightLegs)
                    answer += FlightLegs.DescribeLegs;

                if (HasFlightSteps)
                    answer += FlightSteps.DescribeLinealM;

                return answer;
            }
        }


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
        // - CameraDownDeg is used in DrawSpace.
        // - OnGroundAt is used in CalculateSettings_OnGroundAt to calculate DroneToGroundAltStartSyncM, DroneToGroundAltEndSyncM, FlightStep.AltitudeM
        public void CalculateSettings_ConfigHasChanged()
        {
            DroneConfig.ValidateFixedCameraDownDeg();

            if (DroneConfig.GimbalDataAvail != GimbalDataEnum.ManualNo)
            {
                // These "sanity checks" are only needed if we do not have gimbal data
                DroneConfig.MaxLegStepPitchDeg = 95; // Degrees
                DroneConfig.MaxLegSumPitchDeg = 95; // Degrees
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
            DroneConfig.RunVideoFromS = startMs / 1000.0f;
            DroneConfig.RunVideoToS = endMs / 1000.0f;
        }


        // Set the RunFromS and RunToS config values based on the start/end step Ids.
        public void SetConfigRunFromToBySection(int startSectionId, int endSectionId)
        {
            if (HasFlightSteps)
            {
                SetConfigRunFromTo(
                    SectionIdToVideoMs(startSectionId),
                    SectionIdToVideoMs(endSectionId));

                CalculateSettings_SwatheSeen(startSectionId, endSectionId);
            }
        }



        // Default the RunFromS and RunToS config values 
        public void DefaultConfigRunFromTo()
        {
            if (HasFlightLegs && DroneConfig.UseLegs)
                // If the flight is more than 1/3 legs, use the first and last legs to default the Run From/To.
                // This is the "interesting" part of the flight that the Yolo and Comb processes are best applied to.
                SetConfigRunFromToBySection(
                    FlightLegs.Legs[0].MinStepId,
                    FlightLegs.Legs[^1].MaxStepId);

            else if (HasFlightSteps)
                SetConfigRunFromToBySection(
                    FlightSteps.MinStepId,
                    FlightSteps.MaxStepId);

            else if (HasInputVideo)
            {
                // Default the RunFrom/To to the full video length
                DroneConfig.RunVideoFromS = 0;
                DroneConfig.RunVideoToS = InputVideo.DurationMs / 1000.0f;

                CalculateSettings_SwatheSeen(1, 9999);
            }
        }


        static DateTime RoundToHour(DateTime dt)
        {
            long ticks = dt.Ticks + 18000000000;
            return new DateTime(ticks - ticks % 36000000000, dt.Kind);
        }


        // Load flight data file associated with the video
        private void LoadFlightDataFromTextFile(VideoData theVideoData)
        {
            if (theVideoData == null)
                return;

            // Try to load the flight log from an DJI SRT text file
            var flightData = new FlightSections();
            (bool success, GimbalDataEnum cameraPitchYawRoll) =
                new DroneSrtParser().ParseFlightLogSections(theVideoData, flightData, this);
            // If theVideoData.FileName contains text H20T then set the CameraType
            if (theVideoData.FileName.ToUpper().Contains("H20T"))
                theVideoData.CameraType = VideoModel.DjiH20T;
            else if (theVideoData.FileName.ToUpper().Contains("H30T"))
                theVideoData.CameraType = VideoModel.DjiH30T;


            if (!success)
            {
                // Try to load the flight log from a second drone manufacturer's flight log file. PQR TODO
                flightData = new FlightSections();
                success = false;
            }

            if (!success)
                return;

            DroneConfig.GimbalDataAvail = cameraPitchYawRoll;
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

                FlightSections = flightData;
            }
        }


        // After selecting/loading a file and its settings, user has edited the drone settings.
        // The new settings have been loaded into the config objects. Update our drone data accordingly.
        public void WriteDataStore(DroneDataStore dataStore, bool firstSave)
        {
            // We need to update the Drone datastore
            dataStore.Open();

            DroneSave datawriter = new(dataStore, this);
            datawriter.SaveDroneSettings(firstSave);
            datawriter.SaveData_Detail(false);

            dataStore.FreeResources();
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


        public const int DateIndex = 0;
        public const int TimeIndex = 1;
        public const int DurationIndex = 2;
        public const int CountryXIndex = 3;
        public const int CountryYIndex = 4;
        public const int EastingMIndex = 5;
        public const int NorthingMIndex = 6;
        public const int FileNameIndex = 7;
        public const int GoogleMapsIndex = 8;


        // Get the drone settings needed to describe the flight in the SkyCombFlights app
        public DataPairList GetSettingsForSkyCombFlights()
        {
            string date = "";
            string time = "";
            float countryX = 0;
            float countryY = 0;
            float eastingM = 0;
            float northingM = 0;

            if (FlightSections != null)
            {
                if (FlightSections.MinDateTime != DateTime.MinValue)
                {
                    date = FlightSections.MinDateTime.ToString("dd-MM-yyyy");
                    time = FlightSections.MinDateTime.ToString("HH:mm:ss");
                }

                var minC = FlightSections.MinCountryLocation;
                var maxC = FlightSections.MaxCountryLocation;
                if ((maxC != null) && (minC != null))
                {
                    countryX = (minC.EastingM + maxC.EastingM) / 2.0f;
                    countryY = (minC.NorthingM + maxC.NorthingM) / 2.0f;
                    eastingM = (maxC.EastingM - minC.EastingM);
                    northingM = (maxC.NorthingM - minC.NorthingM);
                }
            }

            return new DataPairList
            {
                { "Date", date },
                { "Time", time },
                { "Duration", InputVideo.DurationMsToString(0) },
                { "Country X", countryX, 0 },
                { "Country Y", countryY, 0 },
                { "Easting M", eastingM, 0 },
                { "Northing M", northingM, 0 },
                { "File name", InputVideo.ShortFileName() },
                { "Google Maps", GoogleMapsLink() },
            };
        }


        private bool disposed = false;


        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    FreeResources();
                }
                base.Dispose(disposing);
                disposed = true;
            }
        }


        ~Drone()
        {
            Dispose(false);
        }
    }
}


