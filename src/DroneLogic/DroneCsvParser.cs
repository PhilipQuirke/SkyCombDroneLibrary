// Copyright SkyComb Limited 2025. All rights reserved.
using SkyCombDrone.DroneModel;
using SkyCombDrone.PersistModel;
using SkyCombGround.PersistModel;
 

namespace SkyCombDrone.DroneLogic
{
    // Class to parse flight log information from a CSV file
    // Newer drones provide the flight log as a CSV and not a text SRT file
    public class DroneCsvParser : DroneLogParser
    {
        // Parse the drone flight data from a CSV file
        // Returns true if successful, and populates the FlightSections
        public (bool success, GimbalDataEnum cameraPitchYawRoll) ParseFlightLogSectionsFromCSV(VideoData video, FlightSections sections, Drone drone)
        {
            GimbalDataEnum cameraPitchYawRoll = GimbalDataEnum.ManualNo;
            video.CameraType = "";
            sections.Sections.Clear();

            // See if there is an SRT file with the same name as the video file, just a different extension.
            // Alternatively, we accept a M4T*.CSV file in the same directory
            var logFileName = DataStoreFactory.FindFlightLogFileName(video.FileName);
            if (logFileName == "")
                return (false, cameraPitchYawRoll);

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
                DateTime? minDateTime = null, maxDateTime = null;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = line.Split('\t', ',');
                    if (fields.Length < headers.Length) continue;

                    var section = new FlightSection(drone, sectionId++);

                    // Parse time
                    string timeStr = fields[colMap["time"]];
                    if (DateTime.TryParse(timeStr, out DateTime dt))
                    {
                        if (minDateTime == null) minDateTime = dt;
                        maxDateTime = dt;
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
            return (sections.Sections.Count > 0, cameraPitchYawRoll);
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
