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
        public DroneDrawScope? DroneDrawScope = null;

        // The size of the image we are drawing on.
        public Size Size;

        // Descriptive information about the graph.
        public string Title = "";
        public string Description = "";
        public string VertTopLabel = "";
        public string VertBottomLabel = "";
        public string HorizLeftLabel = "";
        public string HorizRightLabel = "";
        public DataPairList? Metrics = null;

        // Do we draw and label the vertical and horizontal axises?
        // Shrinks the data area but makes the image more self-contained.
        protected bool LabelAxises { get; } = false;
        // If we label the axises, how many pixels we need for the labels
        protected int LabelHorzPixels { get; set; } = 0;
        protected int LabelVertPixels { get; set; } = 0;
        // Pixel space needed to draw the axises in
        static public readonly int VertAxisHorzPixels = 2;
        static public readonly int HorizAxisVertPixels = 2;


        public Image<Bgr, byte>? BaseImage = null;


        // Horizontal mapping of raw data to graph pixels.
        public float MinHorizRaw = 0;
        public float MaxHorizRaw = 1;
        public float StepWidthPxs = 1;
        public float StepsPerStride = 1;

        // Vertical mapping of origin to graph pixels.
        public float VertFraction = 1;


        public DrawGraph(DroneDrawScope? drawScope, bool labelAxises)
        {
            DroneDrawScope = drawScope;
            LabelAxises = labelAxises;
        }


        protected void DrawNoData(ref Image<Bgr, byte> image)
        {
            DrawAxisesAndLabels(ref image);

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
            StepWidthPxs = 1.0F * (Size.Width - VertAxisHorzPixels - LabelHorzPixels) / (MaxHorizRaw - MinHorizRaw);

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
            return (int)(VertAxisHorzPixels + LabelHorzPixels + (thisValue - minValue) * StepWidthPxs);
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
        protected int RawDataToHeightPixels(double theDatum, double theMax)
        {
            int thisPxsDown;

            var heightRangePxs = (Size.Height - HorizAxisVertPixels - LabelVertPixels - 1);
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
            }

            return thisPxsDown;
        }


        // Origin of axises in pixels
        protected Point OriginPixel { get {
            return new Point(
                VertAxisHorzPixels + LabelHorzPixels, 
                Size.Height - HorizAxisVertPixels - LabelVertPixels);
        } }


        // Draw vertical and horizontal axis
        protected void DrawAxisesAndLabels(ref Image<Bgr, byte> image)
        {
            var origin = OriginPixel;
            var black = DroneColors.BlackBgr;
            var lineThickness = 1;
            var fontThickness = 1;
            var fontScale = 0.5;

            // Vertical axis
            Line(ref image, new Point(origin.X, 0), origin, black, lineThickness);

            // Horizontal axis
            Line(ref image, origin, new Point(Size.Width, origin.Y), black, lineThickness);

            if (LabelAxises)
            {
                int edge = 2;

                // Vertical axis top value
                int indent = edge + (4 - VertTopLabel.Length) * (origin.X - edge) / 4;
                var thePoint = new Point(indent, 20);
                Text(ref image, VertTopLabel, thePoint, fontScale, black, fontThickness);

                // Vertical axis bottom value
                indent = edge + (4 - VertBottomLabel.Length) * (origin.X - edge) / 4;
                thePoint = new Point(indent, origin.Y);
                Text(ref image, VertBottomLabel, thePoint, fontScale, black, fontThickness);

                // Horizontal axis left value
                thePoint = new Point(origin.X, Size.Height - edge);
                Text(ref image, HorizLeftLabel, thePoint, fontScale, black, fontThickness);

                // Horizontal axis right value
                thePoint = new Point(Size.Width - 60, Size.Height - edge);
                Text(ref image, HorizRightLabel, thePoint, fontScale, black, fontThickness);
            }
        }


        // Overdraw the vertical axis in FocusColor
        protected void OverDrawVertAxis(ref Image<Bgr, byte> image, float runMin, float runMax, float axisMax)
        {
            var droneBgr = DroneColors.InScopeDroneBgr;
            var minProcHeight = RawDataToHeightPixels(runMin, axisMax);
            var maxProcHeight = RawDataToHeightPixels(runMax, axisMax);
            var indent = VertAxisHorzPixels - HighlightThickness;

            Line(ref image, new PointF(indent, minProcHeight), new PointF(indent, maxProcHeight), droneBgr, HighlightThickness);
            Line(ref image, new PointF(indent, minProcHeight), new PointF(HighlightThickness * 3, minProcHeight), droneBgr, HighlightThickness);
            Line(ref image, new PointF(indent, maxProcHeight), new PointF(HighlightThickness * 3, maxProcHeight), droneBgr, HighlightThickness);
        }


        // Overdraw the horizontal axis in FocusColor to show the frame range processed.
        protected void OverDrawHorzAxis(ref Image<Bgr, byte> image)
        {
            var droneBgr = DroneColors.InScopeDroneBgr;
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
                Circle(ref image, new Point(x, y), DroneColors.ActiveDroneBgr);
        }


        // Initialise this graph object
        public virtual void Initialise(Size size)
        {
            Size = size;

            LabelHorzPixels = 0;
            LabelVertPixels = 0;
            if (LabelAxises)
            {
                LabelHorzPixels = 50;
                LabelVertPixels = 18;
            }

            BaseImage = Draw.NewImage(size, DroneColors.GrayBgr);
        }


        // Generate an image of the graph as per scope settings.
        public abstract void CurrImage(ref Image<Bgr, byte> image);


        // Generate a bitmap of the graph as per scope settings.
        public virtual Bitmap CurrBitmap()
        {
            var baseImage = BaseImage.Clone();

            CurrImage(ref baseImage);

            return baseImage.ToBitmap();
        }
    }


    // Code to draw drone / ground altitude data. Horizontal axis is time.
    public abstract class DrawVertRange : DrawGraph
    {
        // Minimum and maximum values to graph on the vertical axis
        protected float MinVertRaw = UnknownValue;
        protected float MaxVertRaw = UnknownValue;


        protected float VertRangeRaw { get { return MaxVertRaw - MinVertRaw; } }

        abstract public float GetVertRaw(FlightStep step);


        protected DrawVertRange(DroneDrawScope drawScope, bool labelAxises) : base(drawScope, labelAxises)
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
        protected DrawAltitude(DroneDrawScope drawScope, bool labelAxises) : base(drawScope, labelAxises)
        {
        }


        override public float GetVertRaw(FlightStep step) { return step.FixedAltitudeM; }


        protected void DrawAltitudeStep(
            ref Image<Bgr, byte> image,
            FlightStep prevStep, FlightStep thisStep,
            float prevWidth, float thisWidth)
        {
            // Draw ground elevation
            if (prevStep.DemM != UnknownValue && thisStep.DemM != UnknownValue)
            {
                var prevHeight = RawDataToHeightPixels(prevStep.DemM - MinVertRaw, VertRangeRaw);
                var thisHeight = RawDataToHeightPixels(thisStep.DemM - MinVertRaw, VertRangeRaw);
                Line(ref image, new PointF(prevWidth, prevHeight), new PointF(thisWidth, thisHeight), DroneColors.ColorToBgr(DroneColors.GroundLineColor), 2);
            }

            // Draw surface (tree top) elevation
            if (prevStep.DsmM != UnknownValue && thisStep.DsmM != UnknownValue)
            {
                var prevHeight = RawDataToHeightPixels(prevStep.DsmM - MinVertRaw, VertRangeRaw);
                var thisHeight = RawDataToHeightPixels(thisStep.DsmM - MinVertRaw, VertRangeRaw);
                Line(ref image, new PointF(prevWidth, prevHeight), new PointF(thisWidth, thisHeight), DroneColors.ColorToBgr(DroneColors.SurfaceLineColor), 2);
            }

            // Draw drone altitude
            var prevAltitude = GetVertRaw(prevStep);
            var thisAltitude = GetVertRaw(thisStep);
            if (prevAltitude != UnknownValue && thisAltitude != UnknownValue)
            {
                var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                var lastRunSectionId = DroneDrawScope.LastRunStepId;
                var inScopeDroneBgr = DroneColors.InScopeDroneBgr;
                var outScopeDroneBgr = DroneColors.OutScopeDroneBgr;

                var prevHeight = RawDataToHeightPixels(prevAltitude - MinVertRaw, VertRangeRaw);
                var thisHeight = RawDataToHeightPixels(thisAltitude - MinVertRaw, VertRangeRaw);
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
        public DrawAltitudeByLinealM(DroneDrawScope drawScope, bool labelAxises) : base(drawScope, labelAxises)
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
                base.Initialise(size);

                CalculateStepWidthAndStride(
                    DroneDrawScope.FloorMinSumLinealM,
                    DroneDrawScope.CeilingMaxSumLinealM);

                if (DroneDrawScope.Drone == null)
                {
                    Title = "Drone Altitude";
                    DrawNoData(ref BaseImage);
                }
                else
                {
                    Title = "Elevations: " + DroneDrawScope.DescribeElevation;
                    HorizLeftLabel = SafeFloatToStr(MinHorizRaw, "0") + "m";
                    HorizRightLabel = SafeFloatToStr(MaxHorizRaw, "0") + "m";
                    Metrics = DroneDrawScope.GetSettings_Altitude;
                    (MinVertRaw, MaxVertRaw) = DroneDrawScope.MinMaxVerticalAxisM;

                    SetVerticalLabels("m");
                    DrawAxisesAndLabels(ref BaseImage);

                    if (VertRangeRaw > 0)
                    {
                        var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                        var lastRunSectionId = DroneDrawScope.LastRunStepId;
                        var inScopeDroneBgr = DroneColors.InScopeDroneBgr;
                        var outScopeDroneBgr = DroneColors.OutScopeDroneBgr;

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

                                    DrawAltitudeStep(ref BaseImage, prevStep, thisStep, prevWidth, thisWidth);
                                }

                                prevStep = thisStep;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawAltitudeByLinealM.Initialise", ex);
            }
        }


        // Draw altitude data based on Drone/GroundSpace data
        public override void CurrImage(ref Image<Bgr, byte> image)
        {
            if ((VertRangeRaw > 0) && (DroneDrawScope.CurrRunFlightStep!=null))
                DrawDroneCircle(ref image,
                    StepToWidth(DroneDrawScope.CurrRunFlightStep.SumLinealM),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep) - MinVertRaw, VertRangeRaw));
        }
    }


    // Code to draw drone / ground altitude data. Horizontal axis is time
    public class DrawAltitudeByTime : DrawAltitude
    {
        public DrawAltitudeByTime(DroneDrawScope drawScope, bool labelAxises) : base(drawScope, labelAxises)
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
                base.Initialise(size);

                if (DroneDrawScope.Drone == null)
                {
                    Title = "Drone Altitude";
                    DrawNoData(ref BaseImage);
                }
                else
                {
                    Title = DroneDrawScope.DescribeElevation;
                    Metrics = DroneDrawScope.GetSettings_Altitude;
                    (MinVertRaw, MaxVertRaw) = DroneDrawScope.MinMaxVerticalAxisM;

                    SetVerticalLabels("m");
                    SetHorizLabelsByTime();
                    DrawAxisesAndLabels(ref BaseImage);

                    CalculateStepWidthAndStrideBySection();

                    if (VertRangeRaw > 0)
                    {
                        var firstRunSectionId = DroneDrawScope.FirstRunStepId;
                        var lastRunSectionId = DroneDrawScope.LastRunStepId;
                        var inScopeDroneBgr = DroneColors.InScopeDroneBgr;
                        var outScopeDroneBgr = DroneColors.OutScopeDroneBgr;

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

                                    DrawAltitudeStep(ref BaseImage, prevStep, thisStep, prevWidth, thisWidth);
                                }

                                prevStep = thisStep;
                            }
                        }

                        // Overdraw the horizontal axis in FocusColor to show the frame range processed.
                        if ((firstRunSectionId != UnknownValue) && (lastRunSectionId != UnknownValue))
                            OverDrawHorzAxis(ref BaseImage);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawAltitudeByTime.Initialise", ex);
            }
        }


        // Draw altitude data based on Drone/GroundSpace data
        public override void CurrImage(ref Image<Bgr, byte> image)
        {
            if (VertRangeRaw > 0)
                DrawDroneCircle(ref image,
                    StepToWidthBySection(DroneDrawScope.CurrRunStepId),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep) - MinVertRaw, VertRangeRaw));
        }
    }


    // Code to draw a drone metric measured against time.
    public abstract class DrawTimeGraph : DrawVertRange
    {
        public DrawTimeGraph(DroneDrawScope drawScope) : base(drawScope, false)
        {
        }


        public void DrawLines(ref Image<Bgr, byte> image)
        {
            try
            {
                VertFraction = (float)(MaxVertRaw / (MaxVertRaw - MinVertRaw));
                DrawAxisesAndLabels(ref image);

                SetHorizLabelsByTime();

                var droneBgr = DroneColors.InScopeDroneBgr;

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

                        var thisHeight = RawDataToHeightPixels(thisRunRaw, MaxVertRaw);

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


        public override void CurrImage(ref Image<Bgr, byte> image)
        {
            if (VertRangeRaw > 0)
                DrawDroneCircle(ref image,
                    StepToWidthBySection(DroneDrawScope.CurrRunStepId),
                    RawDataToHeightPixels(GetVertRaw(DroneDrawScope.CurrRunFlightStep), MaxVertRaw));
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


        override public float GetVertRaw(FlightStep step) { return step.SpeedMps; }


        // Show drone (aka ground) speed as a graph.
        // Paint line in blue (if it relates to the From/To Blocks) and black (if it does not)
        public override void Initialise(Size size)
        {
            base.Initialise(size);

            MinVertRaw = 0;
            MaxVertRaw = UnknownValue;
            if ((DroneDrawScope.Drone != null) && (DroneDrawScope.MaxSpeedMps != UnknownValue))
                // For better visuals, don't let maxSpeed be tiny. At least 2m/s
                MaxVertRaw = (float)Math.Max(2.0, Math.Ceiling(DroneDrawScope.MaxSpeedMps));

            if (MaxVertRaw == UnknownValue)
            {
                Title = "Drone Speed";
                DrawNoData(ref BaseImage);
            }
            else
            {
                Title = DroneDrawScope.DescribeSpeed;
                Metrics = DroneDrawScope.GetSettings_Speed;

                SetVerticalLabels("m/s");
                DrawLines(ref BaseImage);
            }
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
            base.Initialise(size);

            MinVertRaw = DroneDrawScope.FloorMinPitchDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxPitchDeg;


            if ((DroneDrawScope.Drone == null) || (MaxVertRaw == UnknownValue))
            {
                Title = "Drone Pitch";
                DrawNoData(ref BaseImage);
            }
            else
            {
                Title = DroneDrawScope.DescribePitch;
                Metrics = DroneDrawScope.GetSettings_Pitch;

                SetVerticalLabels();
                DrawLines(ref BaseImage);
            }
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
            base.Initialise(size);

            MinVertRaw = DroneDrawScope.FloorMinDeltaYawDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxDeltaYawDeg;

            if ((MaxVertRaw == UnknownValue) || (DroneDrawScope.Drone == null))
            {
                Title = "Drone Delta Yaw";
                DrawNoData(ref BaseImage);
            }
            else
            {
                Title = DroneDrawScope.DescribeDeltaYaw;
                Metrics = DroneDrawScope.GetSettings_DeltaYaw;

                SetVerticalLabels("", "0.0");
                DrawLines(ref BaseImage);
            }
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
            base.Initialise(size);

            MinVertRaw = DroneDrawScope.FloorMinRollDeg;
            MaxVertRaw = DroneDrawScope.CeilingMaxRollDeg;

            if ((DroneDrawScope.Drone == null) || (MaxVertRaw == UnknownValue))
            {
                Title = "Drone Roll";
                DrawNoData(ref BaseImage);
            }
            else
            {
                Title = DroneDrawScope.DescribeRoll;
                Metrics = DroneDrawScope.GetSettings_Roll;

                SetVerticalLabels();
                DrawLines(ref BaseImage);
            }
        }
    }



    // Code to draw drone leg data
    public class DrawLeg : DrawGraph
    {
        public DrawLeg(DroneDrawScope drawScope) : base(drawScope, false)
        {
            Description =
                "Graph of the drone legs in blue (which have near constant altitude, direction && pitch) " +
                "against drone flight elapsed time (on horizontal axis in minutes and seconds).";
        }


        // Show drone legs as a dotted line graph.
        public override void Initialise(Size size)
        {
            base.Initialise(size);

            Title = "Drone Legs";
            SetHorizLabelsByTime();

            var outColor = DroneColors.OutScopeDroneBgr;
            var inColor = DroneColors.InScopeDroneBgr;

            CalculateStepWidthAndStrideBySection();

            int lineY = 15;
            var fromPoint = new PointF(VertAxisHorzPixels, lineY);
            var toPoint = new PointF(size.Width, lineY);
            Line(ref BaseImage, fromPoint, toPoint, outColor, NormalThickness);

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
                    Line(ref BaseImage, fromPoint, toPoint, inColor, HighlightThickness);

                    // Draw name of leg above the line
                    var midPoint = new Point((int)fromPoint.X, lineY - 3);
                    Text(ref BaseImage, leg.Name, midPoint, 0.4, inColor);
                }

                Metrics = new DataPairList
                    {
                        { "Num Legs", flightLegs.Legs.Count },
                    };
            }
        }


        // Show drone Leg as a graph 
        public override void CurrImage(ref Image<Bgr, byte> image)
        {
        }
    }
}

