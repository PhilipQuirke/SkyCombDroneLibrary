// Copyright SkyComb Limited 2025. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundLogic;
using System.Diagnostics;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    public class EffortDurations : ConfigBase
    {
        public int CalcVideosMs = 0;
        public int CalcSectionsMs = 0; // Flight log sections
        public int CalcGroundMs = 0; // Covers both DEM and DSM elevations 
        public int CalcStepsMs = 0; // Covers both steps and legs 
        public int CalcSwatheMs = 0; // Swathe (area) seen by input video
        public int SaveDataStoreMs = 0;
        public int CalcEffortMs { get { return CalcVideosMs + CalcSectionsMs + CalcGroundMs + CalcStepsMs + CalcSwatheMs + SaveDataStoreMs; } }

        public int LoadVideosMs = 0;
        public int LoadFlightLogMs = 0; // Covers sections, steps and legs
        public int LoadGroundMs = 0; // Covers both DEM and DSM elevations
        public int LoadEffortMs { get { return LoadVideosMs + LoadFlightLogMs + LoadGroundMs; } }


        // Get the object's settings as datapairs (e.g. for saving to a datastore)
        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Calc Videos Ms", CalcVideosMs },
                { "Calc Sections Ms", CalcSectionsMs },
                { "Calc Ground Ms", CalcGroundMs },
                { "Calc Steps Ms", CalcStepsMs },
                { "Calc Seen Ms", CalcSwatheMs },
                { "Save Data Store Ms", SaveDataStoreMs },
                { "Calc Effort Ms", CalcEffortMs },
                { "Load Videos Ms", LoadVideosMs },
                { "Load Flight Log Ms", LoadFlightLogMs },
                { "Load Ground Ms", LoadGroundMs },
                { "Load Effort Ms", LoadEffortMs },
            };
        }


        // Load this object's settings from strings (loaded from a datastore)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            CalcVideosMs = StringToNonNegInt(settings[0]);
            CalcSectionsMs = StringToNonNegInt(settings[1]);
            CalcGroundMs = StringToNonNegInt(settings[2]);
            CalcStepsMs = StringToNonNegInt(settings[3]);
            CalcSwatheMs = StringToNonNegInt(settings[4]);
            SaveDataStoreMs = StringToNonNegInt(settings[5]);
            // CalcEffortMs 
            LoadVideosMs = StringToNonNegInt(settings[7]);
            LoadFlightLogMs = StringToNonNegInt(settings[8]);
            LoadGroundMs = StringToNonNegInt(settings[9]);
            // LoadEffortMs  
        }
    }


    // Given a file name, locate related content on disk, and depending on what is found, create the Drone object.
    public class DroneDataFactory
    {
        private static Stopwatch? Effort = null;


        private static int EffortMs()
        {
            int answer = 0;
            if (Effort != null)
                answer = (int)Effort.Elapsed.TotalMilliseconds;

            Effort = Stopwatch.StartNew();

            return answer;
        }


        // Some settings differ per manufacturer's camera.
        public static void CalculateCameraSpecifics_InputIsVideo(Drone drone)
        {
            if ((drone != null) && drone.HasInputVideo)
                switch (drone.InputVideo.CameraType)
                {
                    case VideoModel.DjiH20N:
                    case VideoModel.DjiH20T:
                    // PQR TBC.  Fall through

                    default:
                    case VideoModel.DjiMavic3:
                        // Lennard Sparks' DJI Mavic 3t
                        // Thermal camera: 640×512 @ 30fps
                        // DFOV: Diagonal Field of View = 61 degrees
                        // so HFOV = 38.2 degrees and VFOV = 47.6 degrees 
                        drone.InputVideo.HFOVDeg = 38.2f;
                        break;

                    case VideoModel.DjiM3T:
                        // Colin Aitchison's DJI M300 with XT2 19mm
                        // https://www.pbtech.co.nz/product/CAMDJI20219/DJI-Zenmuse-XT2-ZXT2B19FR-Camera-19mm-Lens--30-Hz says:
                        // FOV 57.12 Degrees x 42.44 Degrees
                        drone.InputVideo.HFOVDeg = 42;
                        break;

                    case VideoModel.DjiM2E:
                        // Philip Quirke's DJI Mavic 2 Enterprise Dual
                        // Refer https://www.dji.com/nz/mavic-2-enterprise/specs
                        drone.InputVideo.HFOVDeg = 57;
                        break;
                }
        }


        public static void CalculateCameraSpecifics_InputIsImages(Drone drone, List<DroneImageMetadata> metaData)
        {
            if((drone == null) || (metaData == null) || (metaData.Count == 0))  
                return;

            // Images are taken every 2 to 5 seconds
            int num_seconds_between_images = 3;

            drone.InputVideo = new VideoData("", null); 
            drone.InputVideo.CameraType = metaData[0].CameraModelName;
            drone.InputVideo.Fps = 1.0 / num_seconds_between_images;
            drone.InputVideo.FrameCount = 1;
            drone.InputVideo.DurationMs = num_seconds_between_images * 1000;
            drone.InputVideo.ImageHeight = metaData[0].ImageHeight ?? 640;
            drone.InputVideo.ImageWidth = metaData[0].ImageWidth ?? 512;
            drone.InputVideo.HFOVDeg = (float)(metaData[0].FieldOfViewDegree ?? 38.2);
            drone.InputVideo.DateEncodedUtc = metaData[0].CreateDate;
            drone.InputVideo.DateEncoded = metaData[0].CreateDate;

            drone.DroneConfig.MaxLegGapDurationMs = 2 * num_seconds_between_images * 1000;

            if (drone.HasFlightSections)
            {
                drone.InputVideo.FrameCount = drone.FlightSections.Sections.Count;
                drone.InputVideo.DurationMs = drone.FlightSections.Sections.Last().Value.SumTimeMs;
                drone.InputVideo.Fps = 1.0 * drone.InputVideo.FrameCount / (drone.InputVideo.DurationMs / 1000.0);
            }
        }


#if DEBUG
        // Check that the Flight DEM and DSM values align with the Ground data values.
        public static void SanityCheckGroundElevationData(Drone drone, GroundData groundData)
        {
            int maxAllowedDeltaM = 4; // PQR TODO. This should be 1m

            if (groundData != null && groundData.HasDemModel)
                foreach (var step in drone.FlightSteps.Steps)
                {
                    var theOldDem = step.Value.DemM;
                    if (theOldDem != BaseConstants.UnknownValue)
                    {
                        var theNewDem = groundData.DemModel.GetElevationByDroneLocn(step.Value.DroneLocnM, true);
                        BaseConstants.Assert(Math.Abs(theNewDem - theOldDem) <= maxAllowedDeltaM, "Flight DEM and Ground DEM mismatch");
                    }
                }

            if (groundData != null && groundData.HasDsmModel)
                foreach (var step in drone.FlightSteps.Steps)
                {
                    var theOldDsm = step.Value.DsmM;
                    if (theOldDsm != BaseConstants.UnknownValue)
                    {
                        var theNewDsm = groundData.DsmModel.GetElevationByDroneLocn(step.Value.DroneLocnM, true);
                        BaseConstants.Assert(Math.Abs(theNewDsm - theOldDsm) <= maxAllowedDeltaM, "Flight DSM and Ground DSM mismatch");
                    }
                }
        }
#endif

        public static Drone Create(
            Action<string> showDroneSettings,
            Func<string, DateTime> readDateEncodedUtc,
            DroneDataStore droneDataStore, DroneConfigModel config,
            string groundDirectory,
            bool fullLoad = true)
        {
            Drone answer;
            string phase = "";

            try
            {
                EffortMs();

                phase = "Loading drone...";
                showDroneSettings(phase);
                answer = new Drone(config);

                phase = "Loading video...";
                showDroneSettings(phase);
                if (answer.LoadFileSettings(droneDataStore, readDateEncodedUtc))
                {
                    answer.EffortDurations.LoadVideosMs = EffortMs();

                    phase = "Loading flight log...";
                    showDroneSettings(phase);
                    var loadedFlight = answer.LoadSettings_Flight(droneDataStore, fullLoad);
                    answer.EffortDurations.LoadFlightLogMs = EffortMs();

                    phase = "Loading ground elevations...";
                    showDroneSettings(phase);
                    var loadedGround = answer.LoadSettings_Ground(droneDataStore, fullLoad);
                    answer.EffortDurations.LoadGroundMs = EffortMs();

                    if (!(loadedFlight && loadedGround))
                    {
                        // If we failed to load previous data, it maybe because there was none to load.
                        // For clarity, we hide any small amount of time we spent evaluating this.
                        if (answer.EffortDurations.LoadFlightLogMs < 5)
                            answer.EffortDurations.LoadFlightLogMs = 0;
                        if (answer.EffortDurations.LoadGroundMs < 5)
                            answer.EffortDurations.LoadGroundMs = 0;

                        if (droneDataStore.InputIsVideo)
                        {
                            phase = "Calculating video data...";
                            showDroneSettings(phase);
                            answer.CalculateSettings_Video();
                            answer.EffortDurations.CalcVideosMs = EffortMs();
                        }

                        if (fullLoad)
                        {
                            phase = "Calculating flight sections...";
                            showDroneSettings(phase);
                            if (droneDataStore.InputIsVideo)
                            {
                                answer.CalculateSettings_FlightSections_InputIsVideo();
                                CalculateCameraSpecifics_InputIsVideo(answer);
                            }
                            else
                            {
                                var metaData = answer.CalculateSettings_FlightSections_InputIsImages(droneDataStore.InputFolderName);
                                CalculateCameraSpecifics_InputIsImages(answer, metaData);
                            }

                            answer.EffortDurations.CalcSectionsMs = EffortMs();

                            phase = "Calculating ground elevations...";
                            showDroneSettings(phase);
                            answer.CalculateSettings_Ground(groundDirectory);
                            answer.EffortDurations.CalcGroundMs = EffortMs();

                            phase = "Calculating flight steps and legs...";
                            showDroneSettings(phase);
                            answer.CalculateSettings_FlightSteps();
                            answer.CalculateSettings_FlightLegs();
                            answer.CalculateSettings_ConfigHasChanged();
                            answer.EffortDurations.CalcStepsMs = EffortMs();

                            phase = "Calculating swathe seen...";
                            showDroneSettings(phase);
                            answer.DefaultConfigRunFromTo();
                            answer.EffortDurations.CalcSwatheMs = EffortMs();

                            // Save sections, steps, legs, DEM, DSM, etc.
                            phase = "Saving drone datastore...";
                            showDroneSettings(phase);
                            answer.SaveAllData(droneDataStore, true);
                        }
                    }
                }

#if DEBUG
                // Check that the Flight DEM and DSM values align with the (compacted, stored, loaded, uncompacted) Ground data.
                if (answer.GroundData != null)
                    SanityCheckGroundElevationData(answer, answer.GroundData);
#endif

                phase = "Drone and ground data ready.";
                showDroneSettings(phase);
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("DroneDataFactory.Create(Phase=" + phase + ")", ex);
            }

            return answer;
        }
    }
}
