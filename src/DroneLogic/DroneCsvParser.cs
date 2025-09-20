// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.CommonSpace;


namespace SkyCombDrone.DroneLogic
{
    // Class to parse flight log information from a CSV file
    // Newer drones provide the flight log as a CSV and not a text SRT file
    // Only known case is DJI M4T Matrice
    public class DroneCsvParser : DroneLogParser
    {
        const int MinStepTimeDeltaMs = 33; // Minimum time delta between steps to avoid duplicates


        // Parse the drone flight data from a CSV file
        // Returns true if successful, and populates the FlightSections
        public (bool success, GimbalDataEnum cameraPitchYawRoll) ParseFlightLogSectionsFromCSV(VideoData videoData, FlightSections sections, Drone drone)
        {
            GimbalDataEnum cameraPitchYawRoll = GimbalDataEnum.ManualNo;
            videoData.CameraType = "";
            sections.Sections.Clear();

            var logFileName = DataStoreFactory.FindFlightLogFileName(videoData.FileName);
            if (logFileName == "")
                return (false, cameraPitchYawRoll);

            DateTime? minDateTime = null, maxDateTime = null;

            using (var reader = new System.IO.StreamReader(logFileName))
            {
                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                    return (false, cameraPitchYawRoll);

                var headers = headerLine.Split('\t', ',');
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++)
                    colMap[headers[i].Trim()] = i;

                // Required columns we will load from the CSV
                // altitude_amsl = Altitude above mean sea level
                string[] required = { "time", "longitude", "latitude", "altitude_amsl", "gimbal:pitch", "gimbal:roll", "gimbal:heading" };
                foreach (var req in required)
                    if (!colMap.ContainsKey(req))
                        return (false, cameraPitchYawRoll);

                // The M4T CSV data is sparce - with many 2 second gaps.
                int sectionId = 0;
                DateTime? prevTime = null;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = line.Split('\t', ',');
                    if (fields.Length < headers.Length) continue;

                    // Parse time e.g. "2025-09-06T06:38:33.670" 
                    string timeStr = fields[colMap["time"]];
                    DateTime? dt = null;
                    if (DateTime.TryParse(timeStr, out DateTime parsedDt))
                        dt = parsedDt;

                    // There are many cases where there is only say 2 milliseconds between entries.
                    // If previous valid time exists, and elapsed < 33ms, skip this entry
                    if (dt != null && prevTime != null && (dt.Value - prevTime.Value).TotalMilliseconds < MinStepTimeDeltaMs)
                        continue;

                    var section = new FlightSection(drone, sectionId++);

                    if (dt != null)
                    {
                        if (minDateTime == null)
                        {
                            minDateTime = dt;
                            section.TimeMs = 0;
                        }
                        else
                        {
                            section.TimeMs = (int)(dt.Value - minDateTime.Value).TotalMilliseconds;
                        }
                        section.StartTime = TimeSpan.FromMilliseconds(section.TimeMs);
                        maxDateTime = dt;
                        prevTime = dt;
                    }
                    else
                    {
                        // If time can't be parsed, fallback to 0
                        section.TimeMs = 0;
                        section.StartTime = TimeSpan.Zero;
                    }

                    // Parse longitude/latitude
                    section.GlobalLocation.Longitude = DroneCsvParser.ParseDoubleCsv(fields, colMap, "longitude");
                    section.GlobalLocation.Latitude = DroneCsvParser.ParseDoubleCsv(fields, colMap, "latitude");

                    // Altitude
                    section.AltitudeM = (float)DroneCsvParser.ParseDoubleCsv(fields, colMap, "altitude_amsl");

                    // Gimbal
                    section.PitchDeg = (float)DroneCsvParser.ParseDoubleCsv(fields, colMap, "gimbal:pitch");
                    section.RollDeg = (float)DroneCsvParser.ParseDoubleCsv(fields, colMap, "gimbal:roll");
                    section.YawDeg = (float)DroneCsvParser.ParseDoubleCsv(fields, colMap, "gimbal:heading");
                    if (section.PitchDeg != 0 || section.RollDeg != 0 || section.YawDeg != 0)
                        cameraPitchYawRoll = GimbalDataEnum.AutoYes;

                    // Add section
                    sections.AddSection(section, sectionId > 1 ? sections.Sections[sectionId - 2] : null);
                }
                if (minDateTime != null) sections.MinDateTime = minDateTime.Value;
                if (maxDateTime != null) sections.MaxDateTime = maxDateTime.Value;
                sections.SetTardisMaxKey();
            }

            bool success = sections.Sections.Count > 0;

            if (success)
            {
                // Only known drone so far that provides a CSV flight log is DJI Matrice 4
                // Claude says: DJI Matrice 4T thermal camera specifications are:
                // https://claude.ai/share/e57d3f30-7d69-41f8-a3ba-8a6596b6ccf5

                // DJI Matrice 4T Thermal Camera Configuration
                videoData.CameraType = VideoModel.DjiM4T;
                videoData.Fps = 30; // 30fps for both 640×512 and 1280×1024 modes
                videoData.FocalLength = 53; // DJI Matrice 4T thermal camera has an equivalent focal length of 53 mm 

                // High resolution mode (Super Resolution enabled, Night Mode not activated)
                videoData.ImageHeight = 1024; // 1280 × 1024@30fps (high res mode)
                videoData.ImageWidth = 1280;

                // Standard resolution mode (alternative values)
                // videoData.ImageHeight = 512; // 640 × 512@30fps (standard mode)
                // videoData.ImageWidth = 640;

                // Thermal sensor specifications (uncooled vanadium oxide VOx)
                // Note: Exact physical sensor dimensions not publicly specified by DJI
                // These are estimated values based on typical thermal imaging sensors
                videoData.SensorWidth = 17.0f; // Estimated in mm for thermal sensor
                videoData.SensorHeight = 13.6f; // Estimated in mm for thermal sensor

                // Field of view calculations
                videoData.HFOVDeg = 38.2f; // Calculated horizontal FOV from 45° diagonal FOV
                                           // Diagonal FOV is 45°±0.3° as specified by DJI
                                           // Vertical FOV would be approximately 30.6° for 4:3 aspect ratio

                videoData.DurationMs = (int)(maxDateTime.Value - minDateTime.Value).TotalMilliseconds;

                if (minDateTime != null)
                {
                    // Not sure whether the CSV time is UTC or not.
                    videoData.DateEncodedUtc = minDateTime.Value;
                    videoData.DateEncoded = minDateTime.Value;
                }

                /* 
                 * Additional DJI Matrice 4T Thermal Camera Specifications:
                 * - Aperture: f/1.0
                 * - Focus: 5m to ∞
                 * - Temperature Range (High Gain): -20℃ to 150℃ (-4°F to 302°F)
                 * - Temperature Range (Low Gain): 0℃ to 550℃ (32°F to 1022°F)
                 * - Video Bitrate: 6.5Mbps (H.264), 5Mbps (H.265) for 640×512
                 *                  12Mbps (H.264), 8Mbps (H.265) for 1280×1024
                 * - Photo Formats: JPEG (8bit), R-JPEG (16bit)
                 */

                foreach (var theSection in sections.Sections)
                {
                    theSection.Value.FocalLength = (float) videoData.FocalLength;
                    theSection.Value.Zoom = 1;
                }

                // From manual inspection, the section.GlobalLocation only changes every 2 or 3 steps.
                // The drone is NOT becoming stationary every 2 or 3 steps.
                // Instead the GPS location is only being evaluated every say 5 seconds (roughly corresponding to 2 to 3 steps).
                // Each time a new step is added to the log it contains the latest GPS location values.
                // We need to smooth all section.GlobalLocation assuming:
                // - The drone velocity is smooth - accelerating and decelerating smoothly over time.
                // - A step that contains the same GPS location as the previous one is not adding new location information.

                FlightSection prevprevSection = null;
                FlightSection prevSection = null;
                foreach (var theSection in sections.Sections)
                {
                    var nextSection = theSection.Value;
                    if ((prevprevSection != null) && 
                        (prevSection != null) &&
                       GlobalLocation.DifferentLocations(prevSection.GlobalLocation, nextSection.GlobalLocation) &&
                       !GlobalLocation.DifferentLocations(prevprevSection.GlobalLocation, prevSection.GlobalLocation))
                    {
                        // Update prevSection.GlobalLocation by interpolating between
                        // prevprevSection.GlobalLocation and nextSection.GlobalLocation
                        // Taking into account the StartTime of all three sections.
                        // Interpolate prevSection.GlobalLocation between prevprevSection and nextSection
                        double t0 = prevprevSection.StartTime.TotalMilliseconds;
                        double t1 = prevSection.StartTime.TotalMilliseconds;
                        double t2 = nextSection.StartTime.TotalMilliseconds;
                        double frac = (t1 - t0) / (t2 - t0);
                        if (frac < 0) frac = 0;
                        if (frac > 1) frac = 1;
                        prevSection.GlobalLocation.Longitude = prevprevSection.GlobalLocation.Longitude + frac * (nextSection.GlobalLocation.Longitude - prevprevSection.GlobalLocation.Longitude);
                        prevSection.GlobalLocation.Latitude = prevprevSection.GlobalLocation.Latitude + frac * (nextSection.GlobalLocation.Latitude - prevprevSection.GlobalLocation.Latitude);
                    }

                    prevprevSection = prevSection;
                    prevSection = nextSection;
                }
            }

            return (success, cameraPitchYawRoll);
        }

        private static double ParseDoubleCsv(string[] fields, Dictionary<string, int> colMap, string colName)
        {
            if (colMap.TryGetValue(colName, out int idx) && idx < fields.Length)
            {
                if (double.TryParse(fields[idx], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    return val;
            }
            return 0;
        }
    }
}
