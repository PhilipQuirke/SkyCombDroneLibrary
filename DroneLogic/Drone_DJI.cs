// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    // Class to load flight log information from a drone manufactured by vendor DJI
    public class Drone_DJI
    {
        public const string DjiPrefix = "SRT";
        public const string DjiGeneric = "SRT (DJI)";
        public const string DjiM2E = "SRT (DJI M2E Dual)";
        public const string DjiMavic3 = "SRT (DJI Mavic 3";
        public const string DjM300 = "SRT (DJI M300)";

        System.IO.StreamReader File = null;



        private double FindTokenValue(string line, string token, int tokenPos, string suffix)
        {
            var tokenValue = line.Substring(tokenPos + token.Length);
            return double.Parse(tokenValue.Substring(0, tokenValue.IndexOf(suffix)).Trim());
        }


        private string ReadLine()
        {
            return File.ReadLine();
        }


        private string ReadLineNoSpaces()
        {
            string line = ReadLine();
            if (line == null)
                return null;

            return line.Replace(" ", "");
        }


        // Load the drone flight data for a DJI Mavic 2 Enterprise or DJI Mini from a SRT file
        public (bool success, GimbalDataEnum cameraPitchYawRoll)
            LoadFlightLogSections(VideoData video, FlightSections sections, Drone drone )
        {
            GimbalDataEnum cameraPitchYawRoll = GimbalDataEnum.ManualNo;

            // The file has a format which repeats:
            //      A series of data lines repeated to one frame
            //      One blank line

            // The file includes the following data
            //      longitude, latitude, altitude, yaw, pitch, roll,
            //      FrameCnt: Frame number
            //      DiffTime: 33.3ms = 30fps
            //      iso: ISO = 100
            //      shutter: 1/80.0 = 1/80th sec
            //      fnum: FStop = 450 = f4.5
            //      focal_len: 280 = f2.8 lens
            //      ev: Exposure value
            //      ct: Color Temperature. Mid-day sun is around 5500

            // For sample file data from different types of DJI drones refer 
            // https://github.com/PhilipQuirke/SkyCombAnalystHelp/blob/main/FlightLogs.md 

            File = null;

            try
            {
                sections.Thermal = true; // Default value - may be overridden later.
                sections.Sections.Clear();

                // See if there is an SRT file with the same name as the video file, just a different extension
                sections.FileName = BaseDataStore.SwapFileNameExtension(video.FileName, ".SRT");
                if (!System.IO.File.Exists(sections.FileName))
                    return (false, cameraPitchYawRoll);

                // For DJI drones, flight information is in the SRT text file 
                sections.FileType = DjiGeneric;


                File = new(sections.FileName);

                // Read the first line
                string line = ReadLineNoSpaces();

                // Loop through all the lines
                FlightSection prevSection = null;
                int wantSectionId = 0;
                while (line != null)
                {
                    // Have already read line "15" containing the video frame number

                    // Read line "00:00:01,595 --> 00:00:01,711"
                    line = ReadLine();
                    line = line.Substring(line.IndexOf('>') + 2);
                    // Set the StartTime to nearest second 
                    var startTime = TimeSpan.Parse(line.Substring(0, line.IndexOf(',')));
                    line = line.Substring(line.IndexOf(',') + 1);
                    // Parse the separate milliseconds field
                    int milliseconds = ConfigBase.StringToNonNegInt(line);
                    // In rare cases the milliseconds is greater than 1000 e.g. 1824
                    // Add milliseconds to the timespan StartTime
                    startTime += TimeSpan.FromMilliseconds(milliseconds);


                    // We want at most 1 section per FlightSection.SectionMinMs.
                    // On rare occassions, a flight log where the steps are normally 33ms apart may have a single gap of 1800ms!
                    // Hence we end up with some gaps in the TardisId sequence.
                    var thisSectionId = FlightSection.MsToRoughFlightSectionID((int)startTime.TotalMilliseconds);
                    if (thisSectionId >= wantSectionId)
                    {
                        FlightSection thisSection = new(drone, thisSectionId);
                        thisSection.StartTime = startTime;

                        wantSectionId = thisSectionId + 1;


                        // Read line "<font size="36">FrameCnt : 15, DiffTime : 116ms"
                        line = ReadLine();
                        if(line == null)
                            break;


                        // M2E Dual: Read line "2022-04-10 18:03:55,167,480"
                        // DJI Mini: Read line "2022-06-27 14:31:29.480"
                        // For M2E Dual the time appears to be in local time (not UTC)
                        line = File.ReadLine();
                        if (line == null)
                            break;
                        DateTime theDateTime;
                        if (line.IndexOf(',') > 0)
                        {
                            theDateTime = DateTime.Parse(line.Substring(0, line.IndexOf(',')));
                            line = line.Substring(line.IndexOf(',') + 1);
                            var theMilliSeconds = ConfigBase.StringToNonNegInt(line.Substring(0, line.IndexOf(',')));
                            theDateTime = theDateTime.AddMilliseconds(theMilliSeconds);
                        }
                        else
                            theDateTime = DateTime.Parse(line);
                        if (prevSection == null)
                            sections.MinDateTime = theDateTime;
                        else
                            sections.MaxDateTime = theDateTime;

                        // Read & process the next line(s) until we get a blank line
                        line = ReadLineNoSpaces();
                        if (line == null)
                            break;
                        while ((line != null) && line.Trim().Length > 0)
                        {
                            // Find the fnum (if any) 
                            var token = "[fnum:";
                            var tokenPos = line.IndexOf(token);
                            if (tokenPos >= 0)
                            {
                                // Only optical videos have this.
                                sections.Thermal = false;

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
                                var tokenValue = line.Substring(tokenPos + token.Length);
                                video.ColorMd = tokenValue.Substring(0, tokenValue.IndexOf("]")).Trim();
                            }

                            // Find the focal length (if any)
                            token = "[focal_len:";
                            tokenPos = line.IndexOf(token);
                            if (tokenPos >= 0)
                            {
                                var tokenValue = (int)FindTokenValue(line, token, tokenPos, "]");
                                if (prevSection == null)
                                {
                                    video.MinFocalLength = tokenValue;
                                    video.MaxFocalLength = tokenValue;
                                }
                                else
                                {
                                    video.MinFocalLength = Math.Min(video.MinFocalLength, tokenValue);
                                    video.MaxFocalLength = Math.Max(video.MaxFocalLength, tokenValue);
                                }
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
                                sections.FileType = DjiM2E;
                                thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");
                            }
                            else
                            {
                                token = "[longitude:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    sections.FileType = DjiMavic3;
                                    thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");
                                }
                                else 
                                {
                                    // try to parse "GPS(-41.3899,174.0177,0.0M) BAROMETER:97.7M"
                                    token = "GPS(";
                                    tokenPos = line.IndexOf(token);
                                    if (tokenPos >= 0)
                                    {
                                        sections.FileType = DjM300;
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
                                            }
                                        }
                                    }
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


                            // PQR Input text not used:
                            // Dave Clark's DJI has: [dzoom_ratio: 10000, delta: 0]
                            // Lennard Spark's DJI has: [dzoom_ratio: 1.00]

                            line = ReadLineNoSpaces();
                        }

                        // Add the FlightSection to the Flight
                        sections.AddSection(thisSection, prevSection);

                        prevSection = thisSection;
                    }
                    else
                    {
                        line = ReadLineNoSpaces();
                        while (line != null && line.Trim().Length > 0)
                            line = ReadLineNoSpaces();
                    }

                    // Read the first line of the next-frame lines.
                    // If there is not a next frame, returns null.
                    line = ReadLine();
                }

                // The M2E has a published thermal frame rate of 8fps, implying a thermal TimeDelta of 113 to 116 ms.
                // But on rare occasions, the TimeDelta is much higher than that - closer to 2 seconds!
                // For example, the DJI_0050 SRT data contains 3 long pauses: 1821ms at frame 357, 1824ms at frame 1255, 1824ms at frame 2844.
                // As the SRT and MP4 frame count match, there are no thermal images during the long pause.
                // (The corresponding optical video DJI_0049 has no long pauses. All timediffs are 33 or 34 ms)
            }
            catch (Exception ex)
            {
                throw BaseConstants.ThrowException("Drone_DJI: Unable to load flight log from " + sections.FileName + ", Sections=" + sections.Sections.Count + ", " + ex.Message);
            }
            finally
            {
                if (File != null)
                    File.Close();
                File = null;
            }

            return (sections.Sections.Count > 0, cameraPitchYawRoll);
        }


        // Horizontal field of view in degrees. Differs per manufacturer's camera.
        public static void SetCameraHFOV(Drone drone)
        {
            if ((drone != null) && drone.HasFlightSections &&
                drone.FlightSections.FileType.StartsWith(Drone_DJI.DjiPrefix))
            {
                switch (drone.FlightSections.FileType)
                {
                    case DjiMavic3:
                        // Lennard Sparks DJI Mavic 3t
                        // Thermal camera: 640×512 @ 30fps
                        // DFOV: Diagonal Field of View = 61 degrees
                        // so HFOV = 381. degrees and VFOV = 47.6 degrees 
                        if (drone.HasThermalVideo)
                            drone.ThermalVideo.HFOVDeg = 38;
                        break;

                    case DjM300:
                        // Colin Aitchison DJI M300 with XT2 19mm
                        // https://www.pbtech.co.nz/product/CAMDJI20219/DJI-Zenmuse-XT2-ZXT2B19FR-Camera-19mm-Lens--30-Hz says:
                        // FOV 57.12 Degrees x 42.44 Degrees
                        if (drone.HasThermalVideo)
                            drone.ThermalVideo.HFOVDeg = 42;
                        break;

                    default:
                    case DjiM2E:
                        // Philip Quirke's DJI Mavic 2 Enterprise Dual
                        // Refer https://www.dji.com/nz/mavic-2-enterprise/specs
                        if (drone.HasThermalVideo)
                            drone.ThermalVideo.HFOVDeg = 57;
                        if (drone.HasOpticalVideo)
                            drone.OpticalVideo.HFOVDeg = 77;
                        break;
                }
            }
        }
    }
}


