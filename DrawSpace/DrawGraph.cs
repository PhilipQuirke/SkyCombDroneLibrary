// Copyright SkyComb Limited 2023. All rights reserved.
using Emgu.CV;
using Emgu.CV.Structure;
using SkyCombDrone.DroneLogic;
using SkyCombGround.CommonSpace;
using SkyCombDrone.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.DrawSpace
{
    // Draw a horizontal line graph of the drone's attributes.
    // Horizontal axis is either ElapsedFlightDurationMs or SumLinealM.
    public abstract class DrawGraph : Draw
    {
        public DroneDrawScope DroneDrawScope = null;

        public string Title;
        public string Description;
        public string VertTopLabel;
        public string VertBottomLabel;
        public string HorizLeftLabel;
        public string HorizRightLabel;

        public DataPairList Metrics = null;

        public Size Size;
        public Image<Bgr, byte> BaseImage = null;

        // Horizontal mapping of raw data to graph pixels.
        public float MinHorizRaw = 0;
        public float MaxHorizRaw = 1;
        public float StepWidthPxs = 1;
        public float StepsPerStride = 1;

        // Vertical mapping of origin to graph pixels.
        public float VertFraction = 1;


        // Margins "under" the axis.
        static public readonly int DroneVertAxisX = 2;
        static public readonly int DroneHorizAxisY = 2;


        public DrawGraph(DroneDrawScope drawScope)
        {
            DroneDrawScope = drawScope;
        }


        protected void DrawNoData(ref Image<Bgr, byte> image)
        {
            DrawAxises(ref image);

            NoDataText(ref image, new Point(50, (int)(Size.Height * 0.48)));

            MinHorizRaw = 0;
            MaxHorizRaw = 1;
            StepWidthPxs = 1;
            StepsPerStride = 1;
            VertFraction = 1;
        }


        // Calculate the width of a FlightStep on the graph.
        // For long flight durations, multiple steps can be compacted into one graph pixel (stepsPerStride).
        protected void CalculateStepWidthAndStride(float minHorizRaw, float maxHorizRaw)
        {
            MinHorizRaw = minHorizRaw;
            MaxHorizRaw = maxHorizRaw;

            // Calculate the step distance on the horizontal axis.
            StepWidthPxs = 1.0F * (Size.Width - DroneVertAxisX) / (MaxHorizRaw - MinHorizRaw);

            // Calculate the number of steps between drawing.
            // Ranges from 1 (short video) to say 52.8 (long video). 
            StepsPerStride = 1;
            if (StepWidthPxs < 1)
                StepsPerStride = 1 / StepWidthPxs;
        }
        protected void CalculateStepWidthAndStrideBySection()
        {
            CalculateStepWidthAndStride(0, DroneDrawScope.LastDrawStepId - DroneDrawScope.FirstDrawStepId + 1);
        }


        // The horizontal position of the specified TardisId
        protected int StepToWidth(float thisValue, float minValue)
        {
            return (int)(DroneVertAxisX + (thisValue - minValue) * StepWidthPxs);
        }
        protected int StepToWidth(float thisValue)
        {
            return StepToWidth(thisValue, MinHorizRaw);
        }
        protected int StepToWidthBySection(int theStepId)
        {
            if (theStepId == UnknownValue)
                return UnknownValue;

            return StepToWidth(theStepId, DroneDrawScope.FirstDrawStepId);
        }


        // The height on the vertical axis of graph that theDatum corresponds to
        // The height is measured in pixels downwards. 
        protected int RawDataToHeightPixels(double theDatum, double theMax, string useCase = "", bool assert = true)
        {
            int thisPxsDown;

            var heightRangePxs = (Size.Height - DroneHorizAxisY - 1);
            if (theDatum == UnknownValue)
                thisPxsDown = heightRangePxs;
            else
            {
                var datumFraction = theDatum / theMax;

                var pxsDowntoOrigin = heightRangePxs * VertFraction;
                var pxsUpFromOrigin = pxsDowntoOrigin * datumFraction;

                // If theDatum == theMax we want the datum to appear on (the top row of) the graph => thisPxsDown=0 
                // If theDatum == 0 we want the datum to appear at the origin. So => thisPxsDown=pxsDowntoOrigin
                thisPxsDown = (int)(pxsDowntoOrigin - pxsUpFromOrigin);

                if (assert)
                {
                    // The height should never be outside the graph vertical range
                    // PQR Fails on DJI_0098 on first run Assert(thisPxsDown >= 0, "DroneDatumToHeight: thisPxsDown negative. " + useCase);
                    // PQR Fails on DJI_0120 on third run Assert(thisPxsDown <= Size.Height, "DroneDatumToHeight: thisPxsDown too big. " + useCase);
                }
            }

            return thisPxsDown;
        }


        // Draw vertical and horizontal axis
        protected void DrawAxises(ref Image<Bgr, byte> image)
        {
            // Vertical axis
            var xPos = Size.Height - DroneVertAxisX;
            Line(ref image, new PointF(DroneVertAxisX, 0), new PointF(DroneVertAxisX, xPos), DroneColors.BlackBgr, 1);

            // Horizontal axis
            var yPos = RawDataToHeightPixels(0, 1, "DrawHorizAxis");
            Line(ref image, new PointF(DroneVertAxisX, yPos), new PointF(Size.Width, yPos), DroneColors.BlackBgr, 1);
        }


        // Overdraw the vertical axis in FocusColor
        protected void OverDrawVertAxis(ref Image<Bgr, byte> image, float runMin, float runMax, float axisMax)
        {
            var droneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
            var minProcHeight = RawDataToHeightPixels(runMin, axisMax, "OverDrawVertAxis:min");
            var maxProcHeight = RawDataToHeightPixels(runMax, axisMax, "OverDrawVertAxis:max");
            var indent = DroneVertAxisX - HighlightThickness;

            Line(ref image, new PointF(indent, minProcHeight), new PointF(indent, maxProcHeight), droneBgr, HighlightThickness);
            Line(ref image, new PointF(indent, minProcHeight), new PointF(HighlightThickness * 3, minProcHeight), droneBgr, HighlightThickness);
            Line(ref image, new PointF(indent, maxProcHeight), new PointF(HighlightThickness * 3, maxProcHeight), droneBgr, HighlightThickness);
        }


        // Overdraw the horizontal axis in FocusColor to show the frame range processed.
        protected void OverDrawHorzAxis(ref Image<Bgr, byte> image)
        {
            var droneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
            var minProcWidth = StepToWidthBySection(DroneDrawScope.FirstRunStepId);
            var maxProcWidth = StepToWidthBySection(DroneDrawScope.LastRunStepId);

            Line(ref image, new PointF(minProcWidth, Size.Height - 1), new PointF(maxProcWidth, Size.Height - 1), droneBgr, HighlightThickness);
            Line(ref image, new PointF(minProcWidth, Size.Height - 1), new PointF(minProcWidth, Size.Height - 3), droneBgr, HighlightThickness);
            Line(ref image, new PointF(maxProcWidth, Size.Height - 1), new PointF(maxProcWidth, Size.Height - 3), droneBgr, HighlightThickness);
        }


        // Trim Height value to the available image size (in pixels)
        protected int TrimHeight(int height)
        {
            if (height > Size.Height + 1)
                // Height is bad - show it at maximum graph height - often above the drone flight path! 
                return Size.Height;

            if (height < 0)
                // Height is bad - show it at minimum graph height - often below the ground! 
                return 0;

            return height;
        }


        protected void SetHorizLabelsByTime()
        {
            HorizLeftLabel = VideoData.DurationMsToString(DroneDrawScope.FirstDrawMs, 0);
            HorizRightLabel = VideoData.DurationMsToString(DroneDrawScope.LastDrawMs, 0);
        }


        // Draw circle to show the drone current position.
        protected void DrawDroneCircle(ref Image<Bgr, byte> image, int x, int y)
        {
            if ((DroneDrawScope.Drone != null) && DroneDrawScope.CurrRunFlightStepValid())
                Circle(ref image, new Point(x, y), DroneColors.ColorToBgr(DroneColors.ActiveDroneColor));
        }


        // Initialise this graph object
        public abstract void Initialise(Size size);


        // Generate an image of the graph as per scope settings.
        public abstract Image<Bgr, byte> CurrImage();
    }


    // Code to draw drone / ground altitude data. Horizontal axis is time.
    public abstract class DrawVertRange : DrawGraph
    {
        protected float MinVertRaw = UnknownValue;
        protected float MaxVertRaw = UnknownValue;

        protected float VertRangeRaw { get { return MaxVertRaw - MinVertRaw; } }

        abstract public float GetVertRaw(FlightStep step);

        protected DrawVertRange(DroneDrawScope drawScope) : base(drawScope)
        {
        }

        protected void SetVerticalLabels(string suffix = "", string format = "0")
        {
            VertBottomLabel = SafeFloatToStr(MinVertRaw, format) + suffix;
            VertTopLabel = SafeFloatToStr(MaxVertRaw, format) + suffix;
        }
    }


    // Code to draw drone / ground altitude data. Horizontal axis is time.
    public abstract class DrawAltitude : DrawVertRange
    {
        protected DrawAltitude(DroneDrawScope drawScope) : base(drawScope)
        {
        }


        override public float GetVertRaw(FlightStep step) { return step.AltitudeM; }


        protected void DrawAltitudeStep(
            ref Image<Bgr, byte> image,
            FlightStep prevStep, FlightStep thisStep,
            float prevWidth, float thisWidth)
        {
            // Draw ground elevation
            if (prevStep.DemM != UnknownValue && thisStep.DemM != UnknownValue)
            {
                var prevHeight = RawDataToHeightPixels(prevStep.DemM - MinVertRaw, VertRangeRaw, "PrevDem", false);
                var thisHeight = RawDataToHeightPixels(thisStep.DemM - MinVertRaw, VertRangeRaw, "ThisDem", false);
                Line(ref image, new PointF(prevWidth, prevHeight), new PointF(thisWidth, thisHeight), DroneColors.ColorToBgr(DroneColors.GroundLineColor), 2);
            }

            // Draw surface (tree top) elevation
            if (prevStep.DsmM != UnknownValue && thisStep.DsmM != UnknownValue)
            {
                var prevHeight = RawDataToHeightPixels(prevStep.DsmM - MinVertRaw, VertRangeRaw, "PrevDsm", false);
                var thisHeight = RawDataToHeightPixels(thisStep.DsmM - MinVertRaw, VertRangeRaw, "ThisDsm", false);
                Line(ref image, new PointF(prevWidth, prevHeight), new PointF(thisWidth, thisHeight), DroneColors.ColorToBgr(DroneColors.SurfaceLineColor), 2);
            }

            // Draw drone altitude
            var prevAltitude = GetVertRaw(prevStep);
            var thisAltitude = GetVertRaw(thisStep);
            if (prevAltitude != UnknownValue && thisAltitude != UnknownValue)
            {
                var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                var lastRunSectionId = DroneDrawScope.LastRunStepId;
                var inScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
                var outScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);

                var prevHeight = RawDataToHeightPixels(prevAltitude - MinVertRaw, VertRangeRaw, "PrevAlt", false);
                var thisHeight = RawDataToHeightPixels(thisAltitude - MinVertRaw, VertRangeRaw, "ThisAlt", false);
                var highlight =
                    (firstRunSectionId != UnknownValue) && (lastRunSectionId != UnknownValue) &&
                    (thisStep.FlightSection.TardisId >= firstRunSectionId) &&
                    (thisStep.FlightSection.TardisId <= lastRunSectionId);
                Line(ref image, new PointF(prevWidth, prevHeight), new PointF(thisWidth, thisHeight),
                    highlight ? inScopeDroneBgr : outScopeDroneBgr,
                    highlight ? HighlightThickness : NormalThickness);
            }
        }


        // Draw object height-error as vertically-stretched H with centroid, at object height.
        // Draw object horizontal attribure as horizontally-stretched H with centroid, at object height.
        protected void DrawObject(
            ref Image<Bgr, byte> image, Bgr theBgr,
            float minHeight, float avgHeight, float maxHeight,
            float firstWidth, float middleWidth, float lastWidth)
        {
            // Draw the object centroid 
            image.Draw(new Rectangle((int)(middleWidth - 2), (int)(avgHeight - 2), 4, 4), theBgr, HighlightThickness);

            // Draw object horizontal metric as stretched H with centroid, at object height
            Line(ref image, new PointF(firstWidth, avgHeight - 1), new PointF(firstWidth, avgHeight + 1), theBgr);
            Line(ref image, new PointF(firstWidth, avgHeight), new PointF(lastWidth, avgHeight), theBgr);
            Line(ref image, new PointF(lastWidth, avgHeight - 1), new PointF(lastWidth, avgHeight + 1), theBgr);

            // Draw object verrtical metric as stretched H with centroid, at object location
            Line(ref image, new PointF(middleWidth - 1, minHeight), new PointF(middleWidth + 1, minHeight), theBgr);
            Line(ref image, new PointF(middleWidth, minHeight), new PointF(middleWidth, maxHeight), theBgr);
            Line(ref image, new PointF(middleWidth - 1, maxHeight), new PointF(middleWidth + 1, maxHeight), theBgr);
        }
    }


    // Code to draw drone / ground altitude data. Horizontal axis is lineal distance travelled.
    public class DrawAltitudeByLinealM : DrawAltitude
    {
        public DrawAltitudeByLinealM(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Ground elevation (brown), Surface elevation (green), and " +
                "Drone altitude (blue) on vertical axis, " +
                "against drone distance flown. ";
        }


        // Draw altitude data dependant on Drone/GroundSpace data (but not RunSpace data)
        public override void Initialise(Size size)
        {
            try
            {
                Size = size;
                var image = LightGrayImage(size);

                CalculateStepWidthAndStride(
                    DroneDrawScope.FloorMinSumLinealM,
                    DroneDrawScope.CeilingMaxSumLinealM);

                if (DroneDrawScope.Drone == null)
                {
                    Title = "Drone Altitude";
                    DrawNoData(ref image);
                }
                else
                {
                    DrawAxises(ref image);

                    (MinVertRaw, MaxVertRaw) = DroneDrawScope.MinMaxVerticalAxisM;
                    if (VertRangeRaw > 0)
                    {
                        var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                        var lastRunSectionId = DroneDrawScope.LastRunStepId;
                        var inScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
                        var outScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);

                        int prevSectionId = 0;
                        FlightStep prevStep = null;
                        for (int thisSectionId = DroneDrawScope.FirstDrawStepId; thisSectionId <= DroneDrawScope.LastDrawStepId; thisSectionId++)
                        {
                            // Draw the first/last frames, first/last frames processed & a frame every stepsPerStride
                            if (DroneDrawScope.DrawStepId(thisSectionId) ||
                                thisSectionId - prevSectionId > StepsPerStride)
                            {
                                var thisStep = DroneDrawScope.Drone.FlightSteps.StepIdToNearestFlightStep(thisSectionId);
                                if (thisStep == null)
                                    continue;

                                if (prevStep != null)
                                {
                                    var thisWidth = StepToWidth(thisStep.SumLinealM);
                                    var prevWidth = StepToWidth(prevStep.SumLinealM);

                                    DrawAltitudeStep(ref image, prevStep, thisStep, prevWidth, thisWidth);
                                }

                                prevStep = thisStep;
                            }
                        }

                        Title = "Elevations: " + DroneDrawScope.DescribeElevation;
                        SetVerticalLabels("m");
                        HorizLeftLabel = SafeFloatToStr(MinHorizRaw, "0") + "m";
                        HorizRightLabel = SafeFloatToStr(MaxHorizRaw, "0") + "m";
                        Metrics = DroneDrawScope.GetSettings_Altitude;
                    }
                }
                BaseImage = image.Clone();
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawAltitudeByLinealM.Initialise", ex);
            }
        }


        // Draw altitude data based on Drone/GroundSpace data
        public override Image<Bgr, byte> CurrImage()
        {
            var image = BaseImage.Clone();

            if ((VertRangeRaw > 0) && (DroneDrawScope.CurrRunFlightStep!=null))
                DrawDroneCircle(ref image,
                    StepToWidth(DroneDrawScope.CurrRunFlightStep.SumLinealM),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep) - MinVertRaw, VertRangeRaw, "CurrAlt"));

            return image.Clone();
        }
    }


    // Code to draw drone / ground altitude data. Horizontal axis is time
    public class DrawAltitudeByTime : DrawAltitude
    {
        public DrawAltitudeByTime(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of ground elevation in brown, surface (tree-top) elevation in green " +
                "and drone altitude in black && blue (on vertical axis in meters) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds). ";
        }


        // Draw altitude data dependant on Drone/GroundSpace data (but not RunSpace data)
        public override void Initialise(Size size)
        {
            try
            {
                Size = size;

                var image = LightGrayImage(size);

                if (DroneDrawScope.Drone == null)
                {
                    Title = "Drone Altitude";
                    DrawNoData(ref image);
                }
                else
                {
                    DrawAxises(ref image);

                    CalculateStepWidthAndStrideBySection();

                    (MinVertRaw, MaxVertRaw) = DroneDrawScope.MinMaxVerticalAxisM;
                    if (VertRangeRaw > 0)
                    {
                        var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                        var lastRunSectionId = DroneDrawScope.LastRunStepId;
                        var inScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
                        var outScopeDroneBgr = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);

                        int prevSectionId = 0;
                        FlightStep prevStep = null;
                        for (int thisSectionId = DroneDrawScope.FirstDrawStepId; thisSectionId <= DroneDrawScope.LastDrawStepId; thisSectionId++)
                        {
                            // Draw the first/last frames, first/last frames processed & a frame every stepsPerStride
                            if (DroneDrawScope.DrawStepId(thisSectionId) ||
                                thisSectionId - prevSectionId > StepsPerStride)
                            {
                                var thisStep = DroneDrawScope.Drone.FlightSteps.StepIdToNearestFlightStep(thisSectionId);
                                if (thisStep == null)
                                    continue;

                                if (prevStep != null)
                                {
                                    var thisWidth = StepToWidthBySection(thisStep.FlightSection.TardisId);
                                    var prevWidth = thisWidth - StepWidthPxs;

                                    DrawAltitudeStep(ref image, prevStep, thisStep, prevWidth, thisWidth);
                                }

                                prevStep = thisStep;
                            }
                        }

                        // Overdraw the horizontal axis in FocusColor to show the frame range processed.
                        if ((firstRunSectionId != UnknownValue) && (lastRunSectionId != UnknownValue))
                            OverDrawHorzAxis(ref image);

                        Title = DroneDrawScope.DescribeElevation;
                        SetVerticalLabels("m");
                        SetHorizLabelsByTime();

                        Metrics = DroneDrawScope.GetSettings_Altitude;
                    }
                }

                BaseImage = image.Clone();
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawAltitudeByTime.Initialise", ex);
            }
        }


        // Draw altitude data based on Drone/GroundSpace data
        public override Image<Bgr, byte> CurrImage()
        {
            var image = BaseImage.Clone();

            if (VertRangeRaw > 0)
                DrawDroneCircle(ref image,
                    StepToWidthBySection(DroneDrawScope.CurrRunStepId),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep) - MinVertRaw, VertRangeRaw, "CurrAlt"));

            return image.Clone();
        }
    }


    // Code to draw a drone metric measured against time.
    public abstract class DrawTimeGraph : DrawVertRange
    {
        public DrawTimeGraph(DroneDrawScope drawScope) : base(drawScope)
        {
        }


        public void DrawLines(ref Image<Bgr, byte> image)
        {
            try
            {

                VertFraction = (float)(MaxVertRaw / (MaxVertRaw - MinVertRaw));
                DrawAxises(ref image);

                SetHorizLabelsByTime();

                var droneBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);

                CalculateStepWidthAndStrideBySection();

                float maxRunRaw = -9999;
                float minRunRaw = 9999;

                // Draw the line graph
                int prevSectionId = 0;
                int prevHeight = 0;
                for (int thisSectionId = DroneDrawScope.FirstDrawStepId; thisSectionId <= DroneDrawScope.LastDrawStepId; thisSectionId++)
                {
                    // Draw the first/last frames, first/last frames processed & a frame every stepsPerStride
                    if (DroneDrawScope.DrawStepId(thisSectionId) ||
                        thisSectionId - prevSectionId > StepsPerStride)
                    {
                        var thisStep = DroneDrawScope.Drone.FlightSteps.StepIdToNearestFlightStep(thisSectionId);
                        if (thisStep == null)
                            continue;

                        bool highlight = thisSectionId >= DroneDrawScope.FirstRunStepId && thisSectionId <= DroneDrawScope.LastRunStepId;

                        float thisRunRaw = GetVertRaw(thisStep);
                        if (highlight)
                        {
                            maxRunRaw = Math.Max(maxRunRaw, thisRunRaw);
                            minRunRaw = Math.Min(minRunRaw, thisRunRaw);
                        }

                        var thisHeight = RawDataToHeightPixels(thisRunRaw, MaxVertRaw, "DrawTimeGraph.DrawLines");

                        Line(ref image,
                            new PointF(StepToWidthBySection(prevSectionId), prevHeight),
                            new PointF(StepToWidthBySection(thisSectionId), thisHeight),
                            highlight ? droneBgr : DroneColors.BlackBgr,
                            highlight ? HighlightThickness : NormalThickness);

                        prevSectionId = thisSectionId;
                        prevHeight = thisHeight;
                    }
                }

                OverDrawVertAxis(ref image, minRunRaw, maxRunRaw, MaxVertRaw);
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawTimeGraph.DrawLines", ex);
            }
        }


        public override Image<Bgr, byte> CurrImage()
        {
            var image = BaseImage.Clone();

            if (VertRangeRaw > 0)
                DrawDroneCircle(ref image,
                    StepToWidthBySection(DroneDrawScope.CurrRunStepId),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep), MaxVertRaw, "DrawTimeGraph"));

            return image.Clone();
        }
    }


    // Code to draw drone speed data
    public class DrawSpeed : DrawTimeGraph
    {
        public DrawSpeed(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of drone speed (on vertical axis in metres per second) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds).";
        }


        override public float GetVertRaw(FlightStep step) { return step.SpeedMps(); }


        // Show drone (aka ground) speed as a graph.
        // Paint line in blue (if it relates to the From/To Blocks) and black (if it does not)
        public override void Initialise(Size size)
        {
            Size = size;
            MinVertRaw = 0;
            MaxVertRaw = UnknownValue;
            if ((DroneDrawScope.Drone != null) && (DroneDrawScope.MaxSpeedMps != UnknownValue))
                // For better visuals, don't let maxSpeed be tiny. At least 2m/s
                MaxVertRaw = (float)Math.Max(2.0, Math.Ceiling(DroneDrawScope.MaxSpeedMps));

            var image = LightGrayImage(size);

            if (MaxVertRaw == UnknownValue)
            {
                Title = "Drone Speed";
                DrawNoData(ref image);
            }
            else
            {
                Title = DroneDrawScope.DescribeSpeed;
                SetVerticalLabels("m/s");
                Metrics = DroneDrawScope.GetSettings_Speed;
                DrawLines(ref image);
            }

            BaseImage = image.Clone();
        }
    }


    // Code to draw drone pitch data
    public class DrawPitch : DrawTimeGraph
    {
        public DrawPitch(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of drone pitch (on vertical axis in degrees) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds). " +
                "A positive pitch value means drone is tilted upwards, and a negative pitch value means drone is tilted downards.";
        }


        override public float GetVertRaw(FlightStep step) { return step.PitchDeg; }


        // Show drone pitch as a graph.
        // Paint line in blue (if it relates to the From/To Blocks) and black (if it does not)
        public override void Initialise(Size size)
        {
            Size = size;
            MinVertRaw = DroneDrawScope.FloorMinPitchDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxPitchDeg;

            var image = LightGrayImage(size);

            if ((DroneDrawScope.Drone == null) || (MaxVertRaw == UnknownValue))
            {
                Title = "Drone Pitch";
                DrawNoData(ref image);
            }
            else
            {
                Title = DroneDrawScope.DescribePitch;
                SetVerticalLabels();
                Metrics = DroneDrawScope.GetSettings_Pitch;
                DrawLines(ref image);
            }

            BaseImage = image.Clone();
        }
    }


    // Code to draw drone delta yaw data
    public class DrawDeltaYaw : DrawTimeGraph
    {
        public DrawDeltaYaw(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of drone delta yaw (on vertical axis in degrees) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds). " +
                "A positive delta yaw means a turn anti-clockwise, and a negative delta yaw means a turn clockwise.";
        }


        override public float GetVertRaw(FlightStep step) { return step.DeltaYawDeg; }


        // Show drone Delta Yaw as a graph.
        // Paint line in blue (if it relates to the From/To Blocks) and black (if it does not)
        public override void Initialise(Size size)
        {
            Size = size;
            MinVertRaw = DroneDrawScope.FloorMinDeltaYawDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxDeltaYawDeg;

            var image = LightGrayImage(size);

            if ((MaxVertRaw == UnknownValue) || (DroneDrawScope.Drone == null))
            {
                Title = "Drone Delta Yaw";
                DrawNoData(ref image);
            }
            else
            {
                Title = DroneDrawScope.DescribeDeltaYaw;
                SetVerticalLabels("", "0.0");
                Metrics = DroneDrawScope.GetSettings_DeltaYaw;
                DrawLines(ref image);
            }

            BaseImage = image.Clone();
        }
    }


    // Code to draw drone roll data
    public class DrawRoll : DrawTimeGraph
    {
        public DrawRoll(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of drone roll (on vertical axis in degrees) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds). " +
                "A positive pitch value means drone is tilted to left(TBC), and a negative pitch value means drone is tilted to right(TBC).";
        }


        override public float GetVertRaw(FlightStep step) { return step.RollDeg; }


        // Show drone roll as a graph.
        // Paint line in blue (if it relates to the From/To Blocks) and black (if it does not)
        public override void Initialise(Size size)
        {
            Size = size;
            MinVertRaw = DroneDrawScope.FloorMinRollDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxRollDeg;

            var image = LightGrayImage(size);

            if ((DroneDrawScope.Drone == null) || (MaxVertRaw == UnknownValue))
            {
                Title = "Drone Roll";
                DrawNoData(ref image);
            }
            else
            {
                Title = DroneDrawScope.DescribeRoll;
                SetVerticalLabels();
                Metrics = DroneDrawScope.GetSettings_Roll;
                DrawLines(ref image);
            }

            BaseImage = image.Clone();
        }
    }



    // Code to draw drone leg data
    public class DrawLeg : DrawGraph
    {
        public DrawLeg(DroneDrawScope drawScope) : base(drawScope)
        {
            Description =
                "Graph of the drone legs in blue (which have near constant altitude, direction && pitch) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds).";
        }


        // Show drone legs as a dotted line graph.
        public override void Initialise(Size size)
        {
            Size = size;
            Title = "Drone Legs";
            SetHorizLabelsByTime();

            var image = LightGrayImage(size);

            var outColor = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);
            var inColor = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);

            CalculateStepWidthAndStrideBySection();

            int lineY = 15;
            var fromPoint = new PointF(DroneVertAxisX, lineY);
            var toPoint = new PointF(size.Width, lineY);
            Line(ref image, fromPoint, toPoint, outColor, NormalThickness);

            if ((DroneDrawScope.Drone != null) && DroneDrawScope.Drone.HasFlightSteps && DroneDrawScope.Drone.HasFlightLegs)
            {
                var flightLegs = DroneDrawScope.Drone.FlightLegs;

                lineY = 13;
                foreach (var leg in flightLegs.Legs)
                {
                    var fromStep = DroneDrawScope.Drone.FlightSteps.StepIdToNearestFlightStep(leg.MinStepId);
                    var toStep = DroneDrawScope.Drone.FlightSteps.StepIdToNearestFlightStep(leg.MaxStepId);
                    if ((fromStep == null) || (toStep == null))
                        continue;

                    fromPoint = new PointF(StepToWidthBySection(fromStep.FlightSection.TardisId), lineY);
                    toPoint = new PointF(StepToWidthBySection(toStep.FlightSection.TardisId), lineY);
                    Line(ref image, fromPoint, toPoint, inColor, HighlightThickness);

                    // Draw name of leg above the line
                    var midPoint = new Point((int)fromPoint.X, lineY - 3);
                    Text(ref image, leg.LegName, midPoint, 1, inColor);
                }

                Metrics = new DataPairList
                    {
                        { "Num Legs", flightLegs.Legs.Count },
                    };
            }

            BaseImage = image.Clone();
        }


        // Show drone Leg as a graph 
        public override Image<Bgr, byte> CurrImage()
        {
            return BaseImage.Clone();
        }
    }
}

