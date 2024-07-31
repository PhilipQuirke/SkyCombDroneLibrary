// Copyright SkyComb Limited 2023. All rights reserved. 
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.DroneLogic
{
    public class VideoData : VideoModel
    {
        // The current video FrameID position as at the last GetFrameInternal call
        public int CurrFrameId { get; set; } = UnknownValue;

        // The current video position (in milliseconds) as at the last GetFrameInternal call
        public int CurrFrameMs { get; set; } = UnknownValue;
        // The current video frame as at the last GetFrameInternal call
        public Mat? CurrFrameMat { get; set; } = null;


        // Do we have a frame cached?
        public bool HaveFrame
        {
            get
            {
                return
                    (CurrFrameId >= 0) &&
                    (CurrFrameMs >= 0) &&
                    (CurrFrameMat != null) &&
                    (CurrFrameMat.Width > 0) &&
                    (CurrFrameMat.Height > 0);
            }
        }


        public VideoData(string fileName, bool thermal, Func<string, DateTime> readDateEncodedUtc) : base(fileName, thermal, readDateEncodedUtc)
        {
            ResetCurrFrame();
        }


        public void ResetCurrFrame()
        {
            CurrFrameId = UnknownValue;
            CurrFrameMs = UnknownValue;
            CurrFrameMat?.Dispose();
            CurrFrameMat = null;
        }


        // Returns the next frame of the video. Sets CurrVideoFrameID and CurrVideoFrameMs.
        // WARNING: Calling SetFramePos(14) then QueryFrame() will give you frame 15 (not 14).
        private void GetFrameInternal(int expectedFrameId)
        {
            try
            {
                ResetCurrFrame();

                CurrFrameMat = DataAccess.QueryFrame();

                // Get the current position in the video in frames
                CurrFrameId = (int)DataAccess.Get(CapProp.PosFrames);

                // Get the current position in the video in milliseconds
                CurrFrameMs = (int)DataAccess.Get(CapProp.PosMsec);

                if ((expectedFrameId > 0) && (expectedFrameId < FrameCount))
                    Assert(CurrFrameId == expectedFrameId,
                        "GetFrameInternal: Unexpected frame." +
                        " Thermal=" + Thermal +
                        " CurrFrameId=" + CurrFrameId +
                        " ExpectedFrameId=" + expectedFrameId);
            }
            catch (Exception ex)
            {
                throw ThrowException("VideoData.GetFrameInternal", ex);
            }
        }



        // Set the current position in the video in frames.
        // This function is slow. Try to avoid using it.
        public void SetAndGetCurrFrameId(int frameId)
        {
            // Calling SetCurrFrameId(14) then GetFrameInternal() will give you frame 15 (not 14).
            DataAccess.Set(CapProp.PosFrames, frameId - 1);

            GetFrameInternal(frameId);
        }


        // Set the current position in the video in milliseconds.
        // This function is slow. Try to avoid using it.
        public void SetAndGetCurrFrameMs(int posMsec)
        {
            DataAccess.Set(CapProp.PosMsec, posMsec);

            GetFrameInternal(UnknownValue);
        }


        // Returns the next frame of the video. Sets CurrVideoFrameID and CurrVideoFrameMs.
        // WARNING: Calling SetFramePos(14) then QueryFrame() will give you frame 15 (not 14).
        public bool GetNextFrame()
        {
            int desiredFrame = CurrFrameId + 1;
            if (desiredFrame >= FrameCount)
                return false;

            GetFrameInternal(CurrFrameId + 1);
            return true;
        }


        // Advance to the desiredFrameMs (incrementally or by jumping). Only used on DisplayVideo.
        public void SetOrGetNextFramebyMs(int desiredFrameMs)
        {
            if ((CurrFrameMs < 0) || (desiredFrameMs < CurrFrameMs) || (desiredFrameMs - CurrFrameMs > 1000))
                // SetCurrFrameMs is slow but better than reading dozens of frames.
                // For example, DJI M2E Dual optical video is 30fps. Dont want to read dozens of frames.
                SetAndGetCurrFrameMs(desiredFrameMs);

            else
            {
                // Get the closest frame after the desired point. 
                do
                {
                    GetNextFrame();
                    // PQR this test is iffy. What if frame-to-frame time is 114ms and CurrFrameMs is 1ms less than desiredFrameMs? 
                } while (HaveFrame && (CurrFrameMs < desiredFrameMs));
            }
        }


        // Calculate settings. 
        // This function is slow. Try to minimise usage.
        public void CalculateSettings()
        {
            // Refer https://github.com/opencv/opencv/issues/15749
            SetAndGetCurrFrameId(FrameCount);

            // Get video length in milliseconds. This is slow.
            DurationMs = CurrFrameMs;

            // The above call is not 100% reliable
            if (DurationMs <= 0)
                // Fall back to the approximate method (as Fps is approximate, especially for drone videos)
                CalculateApproxDurationMs();
        }


        // Given fromS and toS, which video frames will we process?
        public (int firstVideoFrameId, int lastVideoFrameId, int firstVideoFrameMs, int lastVideoFrameMs)
            CalculateFromToS(float fromVideoS, float toVideoS)
        {
            int firstVideoFrameId = 1;
            int lastVideoFrameId = FrameCount;
            int firstVideoFrameMs = 0;
            int lastVideoFrameMs = DurationMs;

            if ((toVideoS > 0) && (toVideoS * 1000 < DurationMs))
            {
                lastVideoFrameMs = (int)(toVideoS * 1000);
                // Refer https://github.com/opencv/opencv/issues/15749
                SetAndGetCurrFrameMs(lastVideoFrameMs);
                lastVideoFrameId = CurrFrameId;
            }

            // Do this one second, as we most likely want video at this frame for subsequent actions.
            if (fromVideoS > 0)
            {
                firstVideoFrameMs = (int)(fromVideoS * 1000);
                // Refer https://github.com/opencv/opencv/issues/15749
                SetAndGetCurrFrameMs(firstVideoFrameMs);
                firstVideoFrameId = CurrFrameId;
            }

            return (firstVideoFrameId, lastVideoFrameId, firstVideoFrameMs, lastVideoFrameMs);
        }


        public static string OutputVideoFileName(string inputFileName, string outputElseInputDirectory)
        {
            return
                outputElseInputDirectory + "\\" +
                RemoveFileNameSuffix(ShortFileName(inputFileName)) +
                "_SkyComb" +
                inputFileName.Substring(inputFileName.Length - 4);
        }


        // Create an output video file writer
        public static (VideoWriter, string) CreateVideoWriter(
            string inputFileName,
            string outputElseInputDirectory,
            double Fps, Size frameSize)
        {
            var outputFilename = OutputVideoFileName(inputFileName, outputElseInputDirectory);

            var videoWriter = new VideoWriter(
                outputFilename,
                VideoWriter.Fourcc('M', 'P', '4', 'V'),  // TODO. Handle different video types.
                Fps,
                frameSize,
                true);

            return (videoWriter, outputFilename);
        }
    }


    public abstract class TwoVideos : BaseConstants
    {
        // The primary input video to analyse. Prefer thermal to optical video.
        public VideoData? InputVideo { get; set; } = null;

        // The secondary video data (if any) to display analysis results on. Prefer optical to thermal. 
        public VideoData? DisplayVideo { get; set; } = null;


        public bool HasInputVideo { get { return InputVideo != null; } }
        public bool HasDisplayVideo { get { return DisplayVideo != null; } }
        public bool HasTwoVideos { get { return HasInputVideo && HasDisplayVideo; } }


        // The thermal camera video (if any)
        public VideoData ThermalVideo { get { return HasInputVideo && InputVideo.Thermal ? InputVideo : null; } }
        // The optical (aka visible-light) camera video (if any)
        public VideoData OpticalVideo { get { return HasInputVideo && !InputVideo.Thermal ? InputVideo : (HasDisplayVideo && !DisplayVideo.Thermal ? DisplayVideo : null); } }


        public bool HasThermalVideo { get { return ThermalVideo != null; } }
        public bool HasOpticalVideo { get { return OpticalVideo != null; } }


        public int PercentVideoOverlap { get { return VideoData.PercentOverlap(InputVideo, DisplayVideo); } }


        public virtual void ClearData_Video()
        {
            // These objects may be holding open file handles or similar resources.
            if (HasInputVideo)
                InputVideo.Close();
            InputVideo = null;

            if (HasDisplayVideo)
                DisplayVideo.Close();
            DisplayVideo = null;
        }


        // The thermal and optical video files may not start at the same millisecond.
        public int VideoStartOffsetMs()
        {
            if (PercentVideoOverlap <= 0)
                return UnknownValue;

            return (int)InputVideo.DateEncodedUtc.Subtract(DisplayVideo.DateEncodedUtc).TotalMilliseconds;
        }


        // Clear the video frame(s) data
        public void ResetCurrFrames()
        {
            if (HasInputVideo)
                InputVideo.ResetCurrFrame();

            if (HasDisplayVideo)
                DisplayVideo.ResetCurrFrame();
        }


        // Do we have good video frame(s) data
        public bool HaveFrames()
        {
            bool haveFrames = HasInputVideo && InputVideo.HaveFrame;

            if (HasDisplayVideo)
                haveFrames = haveFrames && DisplayVideo.HaveFrame;

            return haveFrames;
        }


        // Reset input AND display video frame position & load image(s)
        public void SetAndGetCurrFrames(int inputFrameId, int delayMs)
        {
            InputVideo.SetAndGetCurrFrameId(inputFrameId);

            if (InputVideo.HaveFrame)
                // Reset display video (if any) frame position to start displaying from
                if (HasDisplayVideo)
                {
                    int displayFrameMs = (int)(InputVideo.CurrFrameMs + delayMs);
                    if ((displayFrameMs > 0) && (displayFrameMs < DisplayVideo.DurationMs))
                        // Refer https://github.com/opencv/opencv/issues/15749
                        DisplayVideo.SetAndGetCurrFrameMs(displayFrameMs);
                    else
                        DisplayVideo.SetAndGetCurrFrameId(0);
                }
        }


        // Get (advance to) the next frame of the video(s)
        public bool GetNextFrames(int delayMs)
        {
            // For the "input" video, we efficiently get the next frame. This is fast.
            // Sets InputVideo member data CurrFrameID & CurrFrameMs
            if (!InputVideo.GetNextFrame())
                return false;

            // Does the Drone have a separate video for displaying (normally optical)
            if (HasDisplayVideo)
            {
                if (InputVideo.HaveFrame)
                    DisplayVideo.SetOrGetNextFramebyMs(
                        (int)(InputVideo.CurrFrameMs + delayMs));
                else
                    DisplayVideo.ResetCurrFrame();
            }
            return true;
        }


        // Return cached frame(s)
        public (Mat? inputMat, Mat? displayMat) CurrFrames()
        {
            Mat? inputMat = InputVideo.CurrFrameMat;
            Mat? displayMat = (HasDisplayVideo ? DisplayVideo.CurrFrameMat : null);
            return (inputMat, displayMat);
        }
    }
}
