// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;
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
        public int CalcSeenMs = 0; // Area seen by input video
        public int SaveDataStoreMs = 0;
        public int CalcEffortMs { get { return CalcVideosMs + CalcSectionsMs + CalcGroundMs + CalcStepsMs + CalcSeenMs + SaveDataStoreMs; } }

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
                { "Calc Seen Ms", CalcSeenMs },
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
            CalcSeenMs = StringToNonNegInt(settings[4]);
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


        public static Drone Create(
            Action<string> showDroneSettings,
            Func<string, DateTime> readDateEncodedUtc,
            DataStore dataStore, DroneConfigModel config, string groundDirectory, string inputFileName)
        {
            Drone answer;

            try
            {
                EffortMs();

                showDroneSettings("Loading drone...");
                answer = new Drone(config);

                showDroneSettings("Loading drone video...");
                if (answer.LoadSettings_Videos(dataStore, readDateEncodedUtc))
                {
                    answer.EffortDurations.LoadVideosMs = EffortMs();

                    showDroneSettings("Loading drone flight log...");
                    var loadedFlight = answer.LoadSettings_Flight(dataStore);
                    answer.EffortDurations.LoadFlightLogMs = EffortMs();

                    showDroneSettings("Loading ground elevations...");
                    var loadedGround = answer.LoadSettings_Ground(dataStore);
                    answer.EffortDurations.LoadGroundMs = EffortMs();

                    if (!(loadedFlight && loadedGround))
                    {
                        // If we failed to load previous data, it maybe because there was none to load.
                        // For clarity, we hide any small amount of time we spent evaluating this.
                        if (answer.EffortDurations.LoadFlightLogMs < 5)
                            answer.EffortDurations.LoadFlightLogMs = 0;
                        if (answer.EffortDurations.LoadGroundMs < 5)
                            answer.EffortDurations.LoadGroundMs = 0;

                        showDroneSettings("Calculating video data...");
                        answer.CalculateSettings_Video();
                        answer.EffortDurations.CalcVideosMs = EffortMs();

                        showDroneSettings("Calculating flight sections...");
                        answer.CalculateSettings_FlightSections();
                        answer.EffortDurations.CalcSectionsMs = EffortMs();

                        showDroneSettings("Calculating ground elevations...");
                        answer.CalculateSettings_Ground(groundDirectory);
                        answer.EffortDurations.CalcGroundMs = EffortMs();

                        showDroneSettings("Calculating flight steps and legs...");
                        answer.CalculateSettings_StepsAndLegs();
                        if (!answer.CalculateSettings_OnGroundAt_IsValid())
                        {
                            answer.Config.OnGroundAt = OnGroundAtEnum.Neither;
                            answer.CalculateSettings_ConfigHasChanged();
                        }
                        answer.EffortDurations.CalcStepsMs = EffortMs();

                        showDroneSettings("Calculating area seen...");
                        answer.DefaultConfigRunFromTo();
                        answer.EffortDurations.CalcSeenMs = EffortMs();

                        showDroneSettings("Updating datastore...");
                        answer.SaveSettings(dataStore);
                    }
                }

                showDroneSettings("Drone and ground data ready.");
            }
            catch (Exception ex)
            {
                throw Constants.ThrowException("DroneDataFactory.Create", ex);
            }

            return answer;
        }
    }
}
