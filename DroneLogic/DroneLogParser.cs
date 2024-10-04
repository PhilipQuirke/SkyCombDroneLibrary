// Copyright SkyComb Limited 2024. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    // Class to parse flight log information from a drone flight log
    public class DroneLogParser
    {
        protected System.IO.StreamReader? File = null;


        // Return the value of the token in the line, where token end is marked by the suffix
        protected string FindTokenString(string line, string token, int tokenPos, string suffix1, string suffix2 = "")
        {
            var tokenValue = line.Substring(tokenPos + token.Length);
            var index1 = tokenValue.IndexOf(suffix1);
            var index2 = (suffix2 == "" ? 1000 : tokenValue.IndexOf(suffix2));

            return tokenValue.Substring(0, Math.Min(index1, index2)).Trim();
        }


        // Return the value of the token in the line, where token end is marked by the suffix
        protected double FindTokenValue(string line, string token, int tokenPos, string suffix1, string suffix2 = "")
        {
            return double.Parse(FindTokenString(line, token, tokenPos, suffix1, suffix2));
        }


        // The file has a format which repeats:
        //      A framecount line
        //      A timespan line
        //      2 to 10 data lines. Format is specific to the drone & camera. It may include a blank line!
        //      One blank line
        // For sample drone flight log data from different types of drones and camera refer to:
        // https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/FlightLogs.md 
        protected List<string>? ReadParagraph()
        {
            List<string>? answer = new();
            int null_count = 0;

            // Some lines contain a "<font>" to "</font>" section that may contain a blank line!
            bool in_font_clause = false;
            while (true)
            {
                string line = File.ReadLine();
                if (line == null)
                {
                    if (!in_font_clause)
                        break;

                    null_count++;
                    if (null_count > 5)
                        // File has ended mid paragraph
                        break;
                }
                else
                {
                    line = line.Trim();
                    if ((line.Length == 0) && !in_font_clause)
                        break;

                    if (in_font_clause)
                        in_font_clause = !line.Contains("</font>");
                    else
                        in_font_clause = line.Contains("<font"); // e.g. <font size="28">

                    if (line.Length > 0)
                        answer.Add(line);
                }
            }

            if (answer.Count == 0)
                answer = null;

            return answer;
        }


        // Parse the RHS of the line to read a TimeSpan:
        //      00:02:59,000 --> 00:03:00,000
        // The M300 has this bug edge case:
        //      00:02:59,000 --> 00:02:60,000
        protected TimeSpan ParseDuration(string line)
        {
            TimeSpan answer;

            line = line.Replace(" ", "");
            line = line.Substring(line.IndexOf('>') + 2);

            // Set the StartTime to nearest second 
            try
            {
                answer = TimeSpan.Parse(line.Substring(0, line.IndexOf(',')));
            }
            catch (Exception ex)
            {
                if (line.Contains(":60"))
                {
                    line = line.Replace(":60", ":00");
                    answer = TimeSpan.Parse(line.Substring(0, line.IndexOf(',')));

                    answer += TimeSpan.FromSeconds(60);
                }
                else
                    throw ex;
            }

            // Parse the separate milliseconds field
            line = line.Substring(line.IndexOf(',') + 1);
            int milliseconds = ConfigBase.StringToNonNegInt(line);

            // In rare cases the milliseconds is greater than 1000 e.g. 1824
            // Add milliseconds to the timespan StartTime
            answer += TimeSpan.FromMilliseconds(milliseconds);

            return answer;
        }


        // Parse the line to read a DateTime
        // The time appears to be in local time (not UTC)
        protected DateTime ParseDateTime(string line)
        {
            DateTime answer;

            if (line.IndexOf(',') > 0)
            {
                answer = DateTime.Parse(line.Substring(0, line.IndexOf(',')));
                line = line.Substring(line.IndexOf(',') + 1);
                var theMilliSeconds = ConfigBase.StringToNonNegInt(line.Substring(0, line.IndexOf(',')));
                answer = answer.AddMilliseconds(theMilliSeconds);
            }
            else
                answer = DateTime.Parse(line);

            return answer;
        }


        protected void FreeResources()
        {
            File?.Close();
            File = null;
        }
    }


    // Class to parse flight log (SRT) information from a drone manufactured by vendor DJI
    public class DroneSrtParser : DroneLogParser
    {
        // Parse the drone flight data for a DJI drone from a SRT file
        public (bool success, GimbalDataEnum cameraPitchYawRoll)
            ParseFlightLogSections(VideoData video, FlightSections sections, Drone drone)
        {
            GimbalDataEnum cameraPitchYawRoll = GimbalDataEnum.ManualNo;
            video.CameraType = "";

            // The drone flight log file has a repeating format per frame containing:
            //      A framecount line
            //      A timespan line
            //      2 to 10 data lines. Format is specific to the drone & camera. It may include a blank line!
            //      One blank line

            // The repeating section may contain this data:
            //      longitude, latitude, altitude, yaw, pitch, roll,
            //      FrameCnt: Frame number
            //      DiffTime: 33.3ms = 30fps
            //      iso: ISO = 100
            //      shutter: 1/80.0 = 1/80th sec
            //      fnum: FStop = 450 = f4.5
            //      focal_len: 280 = f2.8 lens
            //      ev: Exposure value
            //      ct: Color Temperature. Mid-day sun is around 5500

            // For sample drone flight log data from different types of drones and camera refer to:
            // https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/FlightLogs.md 

            File = null;

            try
            {
                sections.Sections.Clear();

                // See if there is an SRT file with the same name as the video file, just a different extension
                sections.FileName = BaseDataStore.SwapFileNameExtension(video.FileName, ".SRT");
                if (!System.IO.File.Exists(sections.FileName))
                    return (false, cameraPitchYawRoll);

                // For DJI drones, flight information is in the SRT text file 
                video.CameraType = VideoModel.DjiGeneric;


                File = new(sections.FileName);

                // Loop through all the lines
                FlightSection? prevSection = null;
                int wantSectionId = 0;
                while (true)
                {
                    // On occasion the flight log ends mid paragraph
                    // giving a bad flight step. We ignore incomplete paragraphs.
                    bool paragraph_good = false;

                    var paragraph = ReadParagraph();
                    // Four is the lowest number of lines seen in a drone flight log paragraph
                    if ((paragraph == null) || (paragraph.Count < 4))
                        break;


                    // Evaluate the drone type
                    if (video.CameraType == VideoModel.DjiGeneric)
                        switch (paragraph.Count())
                        {
                            case 12: video.CameraType = VideoModel.DjiH20T; break;
                            case 11: video.CameraType = VideoModel.DjiH20N; break;
                            case 6: video.CameraType = VideoModel.DjiMavic3; break;
                            case 5: video.CameraType = VideoModel.DjiM3T; break; // Can be a DjiM2E or a DjiM3T. Code below distinguishes difference.
                            case 4: video.CameraType = VideoModel.DjiM300XT2; break;
                        }


                    // Line 0: The video frame number e.g 15

                    // Line 1: The time period covered "00:00:01,595 --> 00:00:01,711"
                    var line = paragraph[1];
                    var startTime = ParseDuration(line);


                    // We want at most 1 section per FlightSection.SectionMinMs.
                    // On rare occassions, a flight log where the steps are normally 33ms apart may have a single gap of 1800ms!
                    // Hence we end up with some gaps in the TardisId sequence.
                    var thisSectionId = FlightSection.MsToRoughFlightSectionID((int)startTime.TotalMilliseconds);
                    if (thisSectionId >= wantSectionId)
                    {
                        FlightSection thisSection = new(drone, thisSectionId);
                        thisSection.StartTime = startTime;

                        wantSectionId = thisSectionId + 1;


                        // Line 2:
                        // M300: Time e.g. 2021.09.18 21:23:27
                        // Most drones: Frame duration e.g. "<font size="36">FrameCnt : 15, DiffTime : 116ms"
                        line = paragraph[2];
                        if (line.Substring(0, 2) == "20")
                        {
                            // Matrice 300 paragraph looks like:
                            //      8
                            //      00:00:07,000-- > 00:00:08,000
                            //      2021.09.18 21:23:30
                            //      GPS(-44.3266, 170.5650, 0.0M) BAROMETER: 363.3M

                            if (prevSection == null)
                                sections.MinDateTime = ParseDateTime(line);
                            else
                                sections.MaxDateTime = ParseDateTime(line);

                            // Parse "GPS(-41.3899,174.0177,0.0M) BAROMETER:97.7M"
                            line = paragraph[3].Replace(" ", "");
                            string token = "GPS(";
                            int tokenPos = line.IndexOf(token);
                            if (tokenPos >= 0)
                            {
                                thisSection.GlobalLocation.Latitude = FindTokenValue(line, token, tokenPos, ",");

                                token = ",";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, ",");

                                    token = "BAROMETER:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                    {
                                        thisSection.AltitudeM = (float)FindTokenValue(line, token, tokenPos, "M");

                                        paragraph_good = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // M2E Dual: "2022-04-10 18:03:55,167,480"
                            // DJI Mini: "2022-06-27 14:31:29.480"
                            line = paragraph[3];
                            if (prevSection == null)
                                sections.MinDateTime = ParseDateTime(line);
                            else
                                sections.MaxDateTime = ParseDateTime(line);

                            int lineNum = 4;
                            while (lineNum < paragraph.Count)
                            {
                                line = paragraph[lineNum].Replace(" ", "");

                                // Find the fnum (if any) 
                                var token = "[fnum:";
                                var tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    var tokenValue = (int)FindTokenValue(line, token, tokenPos, "]");
                                    if (prevSection == null)
                                    {
                                        video.MinFStop = tokenValue;
                                        video.MaxFStop = tokenValue;
                                    }
                                    else
                                    {
                                        video.MinFStop = Math.Min(video.MinFStop, tokenValue);
                                        video.MaxFStop = Math.Max(video.MaxFStop, tokenValue);
                                    }
                                }

                                // Find the ColorMD
                                token = "[color_md:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    video.ColorMd = FindTokenString(line, token, tokenPos, "]");
                                    if ((video.ColorMd == "") || (video.ColorMd == "unknown"))
                                        video.ColorMd = "default";
                                }

                                // Find the focal length (if any)
                                token = "[focal_len:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                    thisSection.FocalLength = (float)FindTokenValue(line, token, tokenPos, "]");

                                // Find the zoom (if any)
                                token = "[dzoom_ratio:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    var zoom = (float)FindTokenValue(line, token, tokenPos, "]", ",");
                                    // Files like DJI_20240717213841_0002_T.SRT contain [dzoom_ratio: 10000, delta:0]
                                    // Sensible zooms are 2, 4, 8, etc. A zoom of greater than 100 is not sensible.
                                    if (zoom < 100)
                                        thisSection.Zoom = zoom;
                                }

                                // Find the latitude
                                token = "[latitude:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                    thisSection.GlobalLocation.Latitude = FindTokenValue(line, token, tokenPos, "]");

                                // Find the longitude
                                token = "[longtitude:"; // sic. Older versions mispelt the longitude as longtitude.
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    video.CameraType = VideoModel.DjiM2E;
                                    thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");

                                    paragraph_good = true;
                                }
                                else
                                {
                                    token = "[longitude:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                    {
                                        thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");
                                        paragraph_good = true;
                                    }
                                }


                                // Note:
                                // If the drone flew in ATTI or OPTI mode (and not GPS mode)
                                // then the latitude and longitude will be zero.
                                // and thisSection.GlobalLocation.Specified will be false.


                                // Find the altitude
                                token = "[altitude:"; // For M2E Dual
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                    thisSection.AltitudeM = (float)FindTokenValue(line, token, tokenPos, "]");
                                else
                                {
                                    // For DJI Mini 2022 e.g.[rel_alt:1.100 abs_alt:-70.436]
                                    // For Lennard Spark's DJI: [rel_alt: 62.370 abs_alt: 227.373]
                                    token = "abs_alt:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.AltitudeM = (float)FindTokenValue(line, token, tokenPos, "]");
                                }

                                // Find the yaw
                                token = "gb_yaw:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    // For a DJI Mavic 3t get: [gb_yaw: -142.5 gb_pitch: -28.7 gb_roll: 0.0]
                                    cameraPitchYawRoll = GimbalDataEnum.AutoYes;

                                    thisSection.YawDeg = (float)FindTokenValue(line, token, tokenPos, "gb_pitch");

                                    // Find the pitch
                                    token = "gb_pitch:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.PitchDeg = (float)FindTokenValue(line, token, tokenPos, "gb_roll");

                                    // Set the roll
                                    token = "gb_roll:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.RollDeg = (float)FindTokenValue(line, token, tokenPos, "]");
                                }
                                else
                                {
                                    // For older DJIs: [Drone: Yaw:147.9, Pitch:4.5, Roll:-0.1]
                                    token = "Yaw:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.YawDeg = (float)FindTokenValue(line, token, tokenPos, ",");

                                    // Find the pitch
                                    token = "Pitch:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.PitchDeg = (float)FindTokenValue(line, token, tokenPos, ",");

                                    // Set the roll
                                    token = "Roll:";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                        thisSection.RollDeg = (float)FindTokenValue(line, token, tokenPos, "]");
                                }

                                lineNum++;
                            }
                        }

                        if (paragraph_good)
                            // Add the FlightSection to the Flight
                            sections.AddSection(thisSection, prevSection);

                        prevSection = thisSection;
                    }
                }

                sections.SetTardisMaxKey();

                // The M2E has a published thermal frame rate of 8fps, implying a thermal TimeDelta of 113 to 116 ms.
                // But on rare occasions, the TimeDelta is much higher than that - closer to 2 seconds!
                // For example, the DJI_0050 SRT data contains 3 long pauses: 1821ms at frame 357, 1824ms at frame 1255, 1824ms at frame 2844.
                // As the SRT and MP4 frame count match, there are no thermal images during the long pause.
                // (The corresponding optical video DJI_0049 has no long pauses. All timediffs are 33 or 34 ms)
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("Drone_DJI_SRT: Unable to parse flight log " + sections.FileName + ", Sections=" + sections.Sections.Count + ", " + ex.Message);
            }
            finally
            {
                FreeResources();
            }

            return (sections.Sections.Count > 0, cameraPitchYawRoll);
        }
    }
}