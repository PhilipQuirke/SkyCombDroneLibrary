// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
 

namespace SkyCombDrone.DroneLogic
{
    // Class to parse flight log information from a CSV file
    // Newer drones provide the flight log as a CSV and not a text SRT file
    public class DroneCsvParser : DroneLogParser
    {
        // Parse the drone flight data from a CSV file
        // Returns true if successful, and populates the FlightSections
        public (bool success, GimbalDataEnum cameraPitchYawRoll) ParseFlightLogSectionsFromCSV(VideoData videoData, FlightSections sections, Drone drone)
        {
            GimbalDataEnum cameraPitchYawRoll = GimbalDataEnum.ManualNo;
            videoData.CameraType = "";
            sections.Sections.Clear();

            // See if there is an SRT file with the same name as the video file, just a different extension.
            // Alternatively, we accept a M4T*.CSV file in the same directory
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

                int sectionId = 0;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = line.Split('\t', ',');
                    if (fields.Length < headers.Length) continue;

                    var section = new FlightSection(drone, sectionId++);

                    // Parse time e.g. "2025-09-06T06:38:33.670" 
                    string timeStr = fields[colMap["time"]];
                    if (DateTime.TryParse(timeStr, out DateTime dt))
                    {
                        if (minDateTime == null)
                        {
                            minDateTime = dt;
                            section.TimeMs = 0;
                        }
                        else
                        {
                            section.TimeMs = (int)(dt - minDateTime.Value).TotalMilliseconds;
                        }
                        section.StartTime = TimeSpan.FromMilliseconds(section.TimeMs);

                        maxDateTime = dt;
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
