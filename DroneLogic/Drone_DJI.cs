// Copyright SkyComb Limited 2023. All rights reserved. 
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.PersistModel;
using System;


// Contains all in-memory data we hold about a drone flight, the videos taken, the flight log, and ground DEM and DSM elevations.
namespace SkyCombDrone.DroneLogic
{
    // Class to load flight log information from a drone manufactured by vendor DJI
    public class Drone_DJI
    {
        public const string FileType = "SRT";


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

            // Sample file data from different types of DJI drones:
            //      
            //      1
            //      00:00:00,000-- > 00:00:00,033
            //      < font size = "36" > FrameCnt : 1, DiffTime: 33ms
            //      2022 - 07 - 10 19:59:42,210,511
            //      [iso: 12800][shutter: 1 / 30.0][fnum: 280][ev: 1.7][ct: 5199][color_md: default][focal_len: 240][dzoom_ratio: 10000, delta: 0],[latitude: -36.891878] [longtitude: 174.703095] [altitude: 51.773998] [Drone: Yaw:-62.1, Pitch: 2.7, Roll: 1.6] </ font >
            //      
            //      1
            //      00:00:00,000-- > 00:00:00,016
            //      < font size = "28" > SrtCnt : 1, DiffTime: 16ms
            //      2022 - 06 - 27 14:31:29.447
            //      [iso: 140][shutter: 1 / 1250.0][fnum: 170][ev: 0][ct: 5358][color_md: default][focal_len: 240][latitude: -36.999425][longitude: 174.567936][rel_alt: 1.100 abs_alt: -70.436] </ font >
            //      
            //      1
            //      00:00:00,000 --> 00:00:00,033
            //      < font size="28">SrtCnt : 1, DiffTime : 33ms
            //      2022 - 10-23 15:53:15.928
            //      [iso: 120] [shutter: 1 / 640.0] [fnum: 170] [ev: 0] 
            //      ag: 1000 sdg: 1000 idg: 4096 1 / 592.610] xhs: 168 ag_reg: 256
            //      worked[ag: 1000 sdg: 1000 idg: 4109 shu: 1 / 592.694]
            //      tar: 120 luma: 122 lv: 12013 usteb 0 iqeb 401
            //      peb: -260 ak: 262[262 0 0] * r_ll:1000
            //      alg: 148[87 123] > 51 * 0[123 175] > 112
            //      gr: 304 geb: -242 reb: -508 lvr: 1000 peb_range[-401 500 lvr: 1000]
            //      anti - flk[mode: 0 frq: 0]
            //      [RGBGain: (2253, 1000, 1730)][ct: 5207]
            //      [adj_dbg_info:[CCM: 362, 32853, 32793, 32800, 347, 32830, 4, 32896, 378]
            //      [color_temperature: 4932]
            //      [iridix_strength: 13][dmsc_sharp_alt_ld: 29][dmsc_sharp_alt_ldu: 22][dmsc_sharp_alt_lu: 7]
            //      [sinter_strength_1: 5][sinter_strength_4: 5]
            //      [color_md: default][focal_len: 240][dzoom_ratio: 10000, delta: 0],[latitude: -35.443070][longitude: 174.351088][rel_alt: 18.200 abs_alt: 52.793][cmpr: lens pos = 37(stat 18), point(49, 49), window: (10, 10), (10, 10) of(30, 30), temp: 3200, inf: 36][sensor_temperature: 32] </ font >
            //
            //      19
            //      00:00:00,598-- > 00:00:00,634
            //      < font size = "28" > FrameCnt: 19, DiffTime: 36ms
            //      2023 - 05 - 31 19:04:26.691
            //      [focal_len: 40.00][dzoom_ratio: 1.00], [latitude: -37.920295] [longitude: 176.456417] [rel_alt: 62.370 abs_alt: 227.373] [gb_yaw: -142.5 gb_pitch: -28.7 gb_roll: 0.0] </ font >

            File = null;

            try
            {
                sections.Thermal = true; // Default value - may be overridden later.
                sections.Sections.Clear();

                // See if there is an SRT file with the same name as the video file, just a different extension
                sections.FileName = GenericDataStore.SwapExtension(video.FileName, ".SRT");
                if (!System.IO.File.Exists(sections.FileName))
                    return (false, cameraPitchYawRoll);

                // For DJI drones, flight information is in the SRT text file 
                sections.FileType = Drone_DJI.FileType;

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
                        while (line.Trim().Length > 0)
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
                                sections.FileType = "SRT (DJI M2E Dual)";
                                thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");
                            }
                            else
                            {
                                token = "[longitude:";
                                tokenPos = line.IndexOf(token);
                                if (tokenPos >= 0)
                                {
                                    sections.FileType = "SRT (DJI)";
                                    thisSection.GlobalLocation.Longitude = FindTokenValue(line, token, tokenPos, "]");
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
                throw Constants.ThrowException("Drone_DJI: Unable to load flight log from " + sections.FileName + ", Sections=" + sections.Sections.Count + ", " + ex.Message);
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
        // Refer https://www.dji.com/nz/mavic-2-enterprise/specs
        public static void SetCameraHFOV(Drone drone)
        {
            if ((drone != null) && (drone.FlightSections != null) &&
                drone.FlightSections.FileType.StartsWith(Drone_DJI.FileType))
            {
                if (drone.HasThermalVideo)
                    drone.ThermalVideo.HFOVDeg = 57;
                if (drone.HasOpticalVideo)
                    drone.OpticalVideo.HFOVDeg = 85;
            }
        }
    }
}


