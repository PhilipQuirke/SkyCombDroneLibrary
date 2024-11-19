// Copyright SkyComb Limited 2024. All rights reserved. 
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.DroneLogic
{
    public class VideoData : VideoModel, IDisposable
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


        public VideoData(string fileName, Func<string, DateTime> readDateEncodedUtc) : base(fileName, readDateEncodedUtc)
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

                AssertDataAccess();

                CurrFrameMat = DataAccess.QueryFrame();

                // Get the current position in the video in frames
                CurrFrameId = (int)DataAccess.Get(CapProp.PosFrames);

                // Get the current position in the video in milliseconds
                CurrFrameMs = (int)DataAccess.Get(CapProp.PosMsec);

                if ((expectedFrameId > 0) && (expectedFrameId < FrameCount))
                    Assert(CurrFrameId == expectedFrameId,
                        "GetFrameInternal: Unexpected frame." +
                        " CurrFrameId=" + CurrFrameId +
                        " ExpectedFrameId=" + expectedFrameId);
            }
            catch (Exception ex)
            {
                ResetCurrFrame();
                throw ThrowException("GetFrameInternal", ex);
            }
        }



        // Set the current position in the video in frames.
        // This function is slow. Try to avoid using it.
        public void SetAndGetCurrFrameId(int frameId)
        {
            AssertDataAccess();

            // Calling SetCurrFrameId(14) then GetFrameInternal() will give you frame 15 (not 14).
            DataAccess.Set(CapProp.PosFrames, frameId - 1);

            GetFrameInternal(frameId);
        }


        // Set the current position in the video in milliseconds.
        // This function is slow. Try to avoid using it.
        public void SetAndGetCurrFrameMs(int posMsec)
        {
            AssertDataAccess();

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
            AssertDataAccess();
            Assert(fromVideoS <= toVideoS, "CalculateFromToS: Bad from/to sec");

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

            if (fromVideoS < toVideoS + 1)
                Assert(firstVideoFrameId <= lastVideoFrameId, "CalculateFromToS: Bad from/to frame id"); // PQR fudge

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


        private bool disposed = false;


        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    ResetCurrFrame();
                }
                base.Dispose(disposing);
                disposed = true;
            }
        }
    }


    public abstract class OneVideo : BaseConstants, IDisposable
    {
        // The primary input video to analyse. Prefer thermal to optical video.
        public VideoData? InputVideo { get; set; } = null;

        public bool HasInputVideo { get { return InputVideo != null; } }


        // Clear video file handles. More immediate than waiting for garbage collection
        public void FreeResources_Video()
        {
            if (InputVideo != null)
            {
                InputVideo.Dispose();
                InputVideo = null;
            }
        }


        // Clear the video frame(s) data
        public void ResetCurrFrame()
        {
            if (HasInputVideo)
                InputVideo.ResetCurrFrame();
        }


        // Do we have good video frame data
        public bool HaveFrame()
        {
            return HasInputVideo && InputVideo.HaveFrame;
        }


        // Reset input AND display video frame position & load image
        public void SetAndGetCurrFrame(int inputFrameId)
        {
            InputVideo.SetAndGetCurrFrameId(inputFrameId);
        }


        // Get (advance to) the next frame of the video
        public bool GetNextFrame()
        {
            // For the "input" video, we efficiently get the next frame. This is fast.
            // Sets InputVideo member data CurrFrameID & CurrFrameMs
            return InputVideo.GetNextFrame();
        }


        // Return cached frame
        public Mat? CurrFrame()
        {
            return InputVideo.CurrFrameMat;
        }


        private bool disposed = false;


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    FreeResources_Video();
                }
                disposed = true;
            }
        }


        ~OneVideo()
        {
            Dispose(false);
        }
    }
}
