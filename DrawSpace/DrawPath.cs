// Copyright SkyComb Limited 2023. All rights reserved.
using Emgu.CV;
using Emgu.CV.Structure;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneLogic;
using SkyCombDrone.DroneModel;
using SkyCombGround.CommonSpace;
using SkyCombGround.GroundModel;
using SkyCombGround.GroundLogic;
using System.Drawing;



namespace SkyCombDrone.DrawSpace
{
    // Code to draw images related to drone flight path data
    public class DrawPath : DrawGraph
    {
        // Number of shades of green or brown to use in the background
        const int NumShades = 20;
        // A gap (jump) in the Tardis location sequence that makes us NOT draw a line
        const int DrawLineMaxGapM = 10;


        public DroneDrawScope? BaseDrawScope = null;


        // Do we draw the flightsteps on the image?
        public bool DrawSteps = true;
        // Do we draw legs as straight lines or just draw each point?
        public bool DrawLegs = true;
        // Do we draw the NorthingM, EastingM range of the drone's movement as text on the image?
        public bool DrawRange = false;

        public Bgr BackgroundColor = DroneColors.GrayBgr;

        // How to draw text (if any) on the image
        public int TextThickness = 2;
        public int TextFontScale = 1;
        public Bgr TextNormalColor;
        public Bgr TextHighlightColor;

        // Normally zero, used in SkyCombFlights when drawing multiple flights in the same image
        public DroneLocation LocationOffset = new();
        // Move locations to desired centre of FOV
        private DroneLocation? TranslateM;
        // Transform meters to pixels
        private Transform? TransformMToPixels;


        public DrawPath(DroneDrawScope? drawScope, bool drawLegs) : base(drawScope)
        {
            Title = (DroneDrawScope != null ? DroneDrawScope.DescribePath : "");
            Description = "Vertical axis is Northing (in meters). Horizontal axis is Easting (in meters)";
            Metrics = (DroneDrawScope != null ? DroneDrawScope.GetSettings_Altitude : null);

            TextNormalColor = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);
            TextHighlightColor = DroneColors.ColorToBgr(DroneColors.LegNameColor);

            DrawLegs = drawLegs &&
                (DroneDrawScope != null) && 
                (DroneDrawScope.Drone != null) && 
                (DroneDrawScope.Drone.NumLegsShown > 0);

            Reset(drawScope);
        }


        public void Reset(DroneDrawScope drawScope)
        {
            BaseDrawScope = drawScope;

            BaseImage = null;
            TransformMToPixels = null;
            TranslateM = null;
        }


        public bool HasPathGraphTransform()
        {
            return TransformMToPixels != null && TransformMToPixels.Scale > 0;
        }


        // Convert from a Location (in meters) to a Point (in pixels)
        public Point DroneLocnMToPixelPoint(DroneLocation orgLocationM)
        {
            var locationM = orgLocationM.Translate(TranslateM);

            var x = TransformMToPixels.XMargin + TransformMToPixels.Scale * locationM.EastingM;
            var y = TransformMToPixels.YMargin - TransformMToPixels.Scale * locationM.NorthingM;

            return new Point((int)x, (int)y);
        }
        private Point FlightPath_DroneLocnMToPixelPoint(TardisModel theStep)
        {
            return DroneLocnMToPixelPoint(theStep.DroneLocnM);
        }
        private Point FlightPath_DroneLocnMToPixelPoint(TardisSummaryModel flightSteps, int stepId)
        {
            return FlightPath_DroneLocnMToPixelPoint(flightSteps.GetTardisModel(stepId));
        }


        // Convert from a Location (in meters) to a square Rectangle (in pixels)
        public Rectangle DroneLocnMToPixelSquare(DroneLocation locationM, int rectPixels)
        {
            var pixelPoint = DroneLocnMToPixelPoint(locationM);
            Rectangle pixelRect = new(
                pixelPoint.X - rectPixels / 2,
                pixelPoint.Y - rectPixels / 2,
                rectPixels,
                rectPixels);

            return pixelRect;
        }


        // Draw a direction chevron (arrow) on top of the flight path leg.
        private void FlightPath_Chevron(ref Image<Bgr, byte> image, TardisModel? flightStep, Bgr color, int thickness = NormalThickness)
        {
            if ((flightStep == null) || (flightStep.YawDeg == UnknownValue))
                return;

            var thisPoint = DroneLocnMToPixelPoint(flightStep.DroneLocnM);

            var (bottomLeft, centerTop, bottomRight) = flightStep.DirectionChevron();

            var p1 = new Point((int)(thisPoint.X + bottomLeft.X), (int)(thisPoint.Y + bottomLeft.Y));
            var p2 = new Point((int)(thisPoint.X + centerTop.X), (int)(thisPoint.Y + centerTop.Y));
            var p3 = new Point((int)(thisPoint.X + bottomRight.X), (int)(thisPoint.Y + bottomRight.Y));
            image.Draw(new LineSegment2D(p1, p2), color, thickness);
            image.Draw(new LineSegment2D(p2, p3), color, thickness);
        }


        public void DrawText(ref Image<Bgr, byte> image, string text, Point thisPoint, bool highlight = true)
        { 
            Text(ref image, text, thisPoint, TextFontScale,
                highlight? TextHighlightColor : TextNormalColor, TextThickness);
        }


        // Draw the leg name near the flight path leg.
        private void FlightPath_LegName(ref Image<Bgr, byte> image, TardisModel? flightStep, bool highlight)
        {
            if ((flightStep == null) || (flightStep.YawDeg == UnknownValue))
                return;

            var thisPoint = DroneLocnMToPixelPoint(flightStep.DroneLocnM);
            var legname = (flightStep is FlightStep ? (flightStep as FlightStep).LegName : "" );

            // Using flightStep.YawDeg, decide where to draw the text relative to thisPoint.
            if (flightStep.YawDeg < 45)
                thisPoint.X += 10;
            else if (flightStep.YawDeg < 135)
                thisPoint.Y -= 10;
            else if (flightStep.YawDeg < 225)
                thisPoint.X -= 10;
            else if (flightStep.YawDeg < 315)
                thisPoint.Y += 10;
            else
                thisPoint.X += 10;

            DrawText(ref image, legname, thisPoint, highlight);
        }


        // Draw all flight path legs (straight lines) with a chevron in the middle.
        private void DrawFlightLegs(ref Image<Bgr, byte> image)
        {
            var flightSteps = BaseDrawScope.Drone.FlightSteps;
            var flightLegs = BaseDrawScope.Drone.FlightLegs;

            int firstRunStepId = BaseDrawScope.FirstRunStepId;
            int lastRunStepId = BaseDrawScope.LastRunStepId;
            bool runScopeSet = (firstRunStepId != UnknownValue) && (lastRunStepId != UnknownValue);

            foreach (var leg in flightLegs.Legs)
            {
                bool highlight = runScopeSet &&
                    (leg.MinStepId >= firstRunStepId) &&
                    (leg.MaxStepId <= lastRunStepId);

                var thisColor = highlight ? DroneColors.InScopeDroneColor : DroneColors.OutScopeDroneColor;
                var thisBgr = DroneColors.ColorToBgr(thisColor);
                var thisThickness = highlight ? HighlightThickness : NormalThickness;

                // Draw the leg as a straight line in black or blue.
                var startPoint = FlightPath_DroneLocnMToPixelPoint(flightSteps, leg.MinStepId);
                var endPoint = FlightPath_DroneLocnMToPixelPoint(flightSteps, leg.MaxStepId);
                Line(ref image, startPoint, endPoint, thisBgr, thisThickness);

                // Draw the leg Name near the start of the leg.
                int earlyStepId = (2 * leg.MinStepId + leg.MaxStepId) / 3;
                var earlyStep = flightSteps.StepIdToNearestFlightStep(earlyStepId);
                FlightPath_LegName(ref image, earlyStep, highlight);

                // Draw the Chevron in the middle of the leg.
                int middleStepId = (leg.MinStepId + leg.MaxStepId) / 2;
                var middleStep = flightSteps.StepIdToNearestFlightStep(middleStepId);
                FlightPath_Chevron(ref image, middleStep, thisBgr, thisThickness);

                // If part (but not all) of the leg is in the run scope, highlight part of the leg in blue.
                if (highlight ||
                    (firstRunStepId > leg.MaxStepId) ||
                    (lastRunStepId < leg.MinStepId))
                    continue;
                var startStepId = Math.Max(firstRunStepId, leg.MinStepId);
                var endStepId = Math.Min(lastRunStepId, leg.MaxStepId);
                if (startStepId >= endStepId)
                    continue;

                startPoint = FlightPath_DroneLocnMToPixelPoint(flightSteps, startStepId);
                endPoint = FlightPath_DroneLocnMToPixelPoint(flightSteps, endStepId);
                Line(ref image, startPoint, endPoint, DroneColors.ColorToBgr(DroneColors.InScopeDroneColor), HighlightThickness);
            }
        }


        // Draw the flight path steps
        public void DrawFlightSteps(ref Image<Bgr, byte> image)
        {
            var hasLegs = ((BaseDrawScope.Drone != null) && BaseDrawScope.Drone.HasFlightLegs);

            int firstRunStepId = BaseDrawScope.FirstRunStepId;
            int lastRunStepId = BaseDrawScope.LastRunStepId;
            bool runScopeSet = (firstRunStepId != UnknownValue) && (lastRunStepId != UnknownValue);

            Point prevPoint = new(UnknownValue, UnknownValue);
            int prevLegId = UnknownValue;

            int maxStepId = BaseDrawScope.TardisSummary.GetTardisMaxKey();
            for(int theStepId = 0; theStepId <= maxStepId; theStepId++)
            {
                var step = BaseDrawScope.TardisSummary.GetTardisModel(theStepId);
                if(step == null)
                    continue;

                var thisPoint = FlightPath_DroneLocnMToPixelPoint(step);
                int thisStepId = step.TardisId;
                int thisLegId = (step is FlightStep ? (step as FlightStep).LegId : UnknownValue);

                bool highlight = runScopeSet &&
                    (thisStepId >= firstRunStepId) &&
                    (thisStepId <= lastRunStepId);
                var thisColor = DroneColors.ColorToBgr(highlight ? DroneColors.InScopeDroneColor : DroneColors.OutScopeDroneColor);

                if (!DrawLegs)
                {
                    // If drone moved less than 10M between steps, draw a line.
                    // Useful when drawing several flights in same graph.
                    if((prevPoint.X != UnknownValue) &&
                        Math.Abs(prevPoint.X - thisPoint.X) < DrawLineMaxGapM &&
                        Math.Abs(prevPoint.Y - thisPoint.Y) < DrawLineMaxGapM)
                        Line(ref image, prevPoint, thisPoint, thisColor);

                    if (thisStepId % 1000 == 0)
                        FlightPath_Chevron(ref image, step, thisColor);
                }
                else
                {
                    // If the step has a leg then dont draw this (short) line.
                    // Rely on the above code to draw a long line for the leg.
                    // Exception: For the first step of the leg we need to connect
                    // up the previous non-leg step.
                    if (((thisLegId <= 0) || (prevLegId <= 0)) &&
                        (thisStepId > 0) && (prevPoint.X != UnknownValue))
                        Line(ref image, prevPoint, thisPoint, thisColor);

                    // Draw chevrons along the path to path every so often to show direction.
                    if ((!hasLegs) && (thisStepId % 50 == 0))
                        FlightPath_Chevron(ref image, step, thisColor);
                }

                prevPoint = thisPoint;
                prevLegId = thisLegId;
            }
        }




        // Draw the ground or surface elevations or "seen" as background of shades of brown or green
        public void DrawGroundModel(
            ref Image<Bgr, byte> image, 
            ref Rectangle maxLocation,
            GroundModel groundModel, 
            Color highColor, Color lowColor, 
            double minValue, double maxValue)
        {
            // Generate a list of shades (of green, brown, or blue) to use for the background.
            List<Color> theShades = GetColorShades(lowColor, highColor, NumShades);

            var pixelsPerMeter = (int)TransformMToPixels.Scale;
            double maxElevationM = -9999;

            for (int row = 1; row < groundModel.NumRows + 1; row++)
            {
                for (int col = 1; col < groundModel.NumCols + 1; col++)
                {
                    var droneLocnM = new DroneLocation(
                        row - GroundModel.GroundBufferM + LocationOffset.NorthingM, 
                        col - GroundModel.GroundBufferM + LocationOffset.EastingM);


                    var locationRect = DroneLocnMToPixelSquare(droneLocnM, pixelsPerMeter);

                    // Add pixels to the right and bottom of square to avoid drawing gaps in image caused by:
                    // - int rounding
                    // - edge between data from two ASC files (as shown by vertical gap in DJI_0094 in large mode (popup window))
                    // Most of this extension will be overdrawn by other (successive) squares.
                    locationRect.Width += 5;
                    locationRect.Height += 5;

                    // Check that the datum is within the image
                    if ((locationRect.X + locationRect.Width > 0) &&
                        (locationRect.Y + locationRect.Height > 0) &&
                        (locationRect.X < image.Width) &&
                        (locationRect.Y < image.Height))
                    {
                        var elevationM = groundModel.GetElevationMByGridIndex(row, col);

                        // Calculate the shade of the square based on its elevation
                        int shadeIndex = (int)Math.Max(0, Math.Min(NumShades - 1,
                            1.0f * NumShades * (elevationM - minValue) / (maxValue - minValue)));
                        if((maxValue==1) && (minValue==0))
                            shadeIndex = (elevationM > 0 ? NumShades - 1 : 0);

                        image.Draw(locationRect, DroneColors.ColorToBgr(theShades[shadeIndex]),
                            -1); // If thickness is less than 1, the rectangle is filled up

                        if((elevationM > 0) && (elevationM > maxElevationM))
                        {
                            maxElevationM = elevationM;
                            maxLocation = locationRect;
                        }
                    }
                }
            }
        }


        // Draw the ground or surface elevations or "seen" as background of shades of brown or green
        private void DrawElevationOrSwathe(ref Image<Bgr, byte> image, GroundType backgroundType)
        {
            if((BaseDrawScope.Drone == null) || (BaseDrawScope.Drone.GroundData == null))
                return;

            // Are we drawing surface or ground elevations or seen?
            GroundModel groundModel = BaseDrawScope.Drone.GroundData.DsmModel;
            Color highColor = DroneColors.SurfaceHighColor;
            Color lowColor = DroneColors.SurfaceLowColor;
            if (backgroundType == GroundType.DemElevations)
            {
                groundModel = BaseDrawScope.Drone.GroundData.DemModel;
                highColor = DroneColors.GroundHighColor;
                lowColor = DroneColors.GroundLowColor;
            }
            else if (backgroundType == GroundType.SwatheSeen)
            {
                groundModel = BaseDrawScope.Drone.GroundData.SwatheModel;
                highColor = Color.White;
                lowColor = Color.LightGray;
            }

            if ((groundModel == null) || !groundModel.HasElevationData())
                return;

            Rectangle maxLocation = new(0, 0, 0, 0);
            
            // Calculate the range of surface elevations
            (double minValue, double maxValue) = groundModel.GetMinMaxElevationM();
            if (backgroundType == GroundType.SwatheSeen)
            {
                minValue = 0;
                maxValue = 1;
            }

            DrawGroundModel(
                ref image,
                ref maxLocation,
                groundModel,
                highColor, lowColor,
                minValue, maxValue);


            // Overdraw the highest elevation with a white triangle
            if((maxLocation.Width > 0) && (backgroundType != GroundType.SwatheSeen))
            {
                var where = new Point(maxLocation.X + maxLocation.Width / 2, maxLocation.Y + maxLocation.Height / 2);

                // Move the triangle away from the image edge enough so that it can be fully drawn.
                // Makes the location less accurate but is visibly better.
                where.X = Math.Max(where.X, UpTriangleLen);
                where.Y = Math.Max(where.Y, UpTriangleLen);
                where.X = Math.Min(where.X, image.Width - UpTriangleLen);
                where.Y = Math.Min(where.Y, image.Height - UpTriangleLen);

                UpTriangle(ref image, where, UpTriangleLen, DroneColors.WhiteBgr, HighlightThickness);
            }
        }


        // Draw the NorthingM, EastingM range of the drone's movement as text
        private void DrawDroneRange(ref Image<Bgr, byte> image, Size size, DroneLocation minLocation, DroneLocation maxLocation)
        {
            var minPoint = DroneLocnMToPixelPoint(minLocation);
            var maxPoint = DroneLocnMToPixelPoint(maxLocation);
            int inset = 15;

            DrawText(ref image, ((int)(minLocation.NorthingM)).ToString(), new Point(minPoint.X + inset, minPoint.Y + inset));
            DrawText(ref image, ((int)(maxLocation.NorthingM)).ToString(), new Point(minPoint.X + inset, maxPoint.Y - 5 * inset));
            DrawText(ref image, ((int)(minLocation.EastingM)).ToString(), new Point(minPoint.X + inset, minPoint.Y ));
            DrawText(ref image, ((int)(maxLocation.EastingM)).ToString(), new Point(maxPoint.X - 5 * inset, minPoint.Y));
        }


        // Draw drone flight path based on Drone/GroundSpace data
        // Use the FlightStep data EastM and NorthM, and the cummulative Min/MaxNorthSumM and Min/MaxEastSumM data.
        // Also use FlightLeg data to draw straight lines.
        public void Initialise(Size size, DroneLocation? processObjectLocation, GroundType groundType)
        {
            try
            {
                TransformMToPixels = new();
                TranslateM = new();

                Image<Bgr, byte> image = NewImage(size, BackgroundColor);

                // Do we want to just show a 1m by 1m area of the flight path?
                bool tightFocus = (processObjectLocation != null);
                var tightFocusM = 0.5f;

                if (tightFocus)
                {
                    // Draw vertical and horizontal axis
                    Line(ref image, new PointF(0, 0), new PointF(0, size.Height - 1), DroneColors.BlackBgr, 1);
                    Line(ref image, new PointF(0, size.Height - 1), new PointF(size.Width, size.Height - 1), DroneColors.BlackBgr, 1);
                }


                if (BaseDrawScope.TardisSummary == null)
                    NoDataText(ref image, new Point(50, (int)(size.Height * 0.15)));
                else
                {
                    // Drone video image covers an area to either side of the drone flight path.
                    var drone = BaseDrawScope.Drone; // Maybe null
                    var tardisSummary = BaseDrawScope.TardisSummary;
                    float pathImageWidthM = 
                        (tightFocus ? 0 : 
                            (drone != null ? drone.FlightSteps.MaxImageWidthM() : 
                                2 * GroundModel.GroundBufferM));

                    if (pathImageWidthM > 2 * GroundModel.GroundBufferM)
                        // We store DEM/DSM data up to GroundBufferM beyond the flight path in each direction.
                        // A drone flying high above ground gives an image width beyond the DEM/DSM coverage.
                        // To avoid gray boundaries on image, we reduce the pathImageWidthM.
                        pathImageWidthM = 2 * GroundModel.GroundBufferM;

                    DroneLocation minLocation = new();
                    DroneLocation maxLocation = new();
                    if (tightFocus)
                    {
                        minLocation = processObjectLocation.Translate(new DroneLocation(-tightFocusM, -tightFocusM));
                        maxLocation = processObjectLocation.Translate(new DroneLocation(tightFocusM, tightFocusM));
                    }
                    else
                    {
                        minLocation = BaseDrawScope.MinDroneLocnM;
                        maxLocation = BaseDrawScope.MaxDroneLocnM;
                    }

                    // The Min/MaxNorthing/EastingSumM values represent the range of locations the drone flew over.
                    if (maxLocation?.NorthingM > 1 && maxLocation?.EastingM > 1)
                    {
                        var neededHorzM = pathImageWidthM
                            + maxLocation.EastingM
                            - minLocation.EastingM;
                        var neededVertM = pathImageWidthM
                            + maxLocation.NorthingM
                            - minLocation.NorthingM;

                        // Calculate the best scale in Pixels / M for each axis independently.
                        var scaleHorzPxsPerM = size.Width / neededHorzM;
                        var scaleVertPxsPerM = size.Height / neededVertM;

                        // Must use the same axis on both axises.
                        var scalePxsPerM = Math.Min(scaleHorzPxsPerM, scaleVertPxsPerM);
                        TransformMToPixels.Scale = scalePxsPerM;

                        TransformMToPixels.YMargin = size.Height;
                        TransformMToPixels.XMargin = 0;

                        // Translate image to bring the focused portion into the visible graph area.
                        TranslateM = new(
                            pathImageWidthM / 2 - minLocation.NorthingM,
                            pathImageWidthM / 2 - minLocation.EastingM);

                        // Ensure image is top aligned. 
                        var spareVertPxs = (size.Height - neededVertM * scalePxsPerM);
                        if (spareVertPxs > 2)
                            TransformMToPixels.YMargin -= spareVertPxs;

                        // Draw the ground or surface elevations as background of shades of brown or green
                        if (!tightFocus)
                            DrawElevationOrSwathe(ref image, groundType);

                        // Draw all flight path legs (as straight lines)
                        if (DrawLegs) 
                            DrawFlightLegs(ref image);

                        // Draw the flight path steps
                        if(DrawSteps)
                            DrawFlightSteps(ref image);

                        // Draw the NorthingM, EastingM range of the drone's movement as text
                        if (DrawRange)
                            DrawDroneRange(ref image, size, minLocation, maxLocation);
                    }
                }

                BaseImage = image.Clone();
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawPath.Initialise", ex);
            }
        }


        public override void Initialise(Size size)
        {
            Initialise(size, null, GroundType.DsmElevations);
        }


        // Draw drone flight path based on Drone/GroundSpace data
        public override Image<Bgr, byte> CurrImage()
        {
            try
            {
                var image = BaseImage.Clone();

                if (TransformMToPixels.Scale > 0)
                {
                    var activeBgr = DroneColors.ColorToBgr(DroneColors.ActiveDroneColor);
                    var inScopeBgr = DroneColors.ColorToBgr(DroneColors.InScopeDroneColor);
                    var outScopeBgr = DroneColors.ColorToBgr(DroneColors.OutScopeDroneColor);

                    var flightStep = BaseDrawScope.CurrRunFlightStep;
                    if (flightStep != null)
                    {
                        // Draw a circle to show current drone location.
                        var dronePoint = DroneLocnMToPixelPoint(flightStep.DroneLocnM);
                        Circle(ref image, dronePoint, activeBgr);

                        // PQR ToDo Draw the Block (instead of the FlightStep) InputImageSizeM
                        // for greater accuracy (as there are 2 or 3 blocks per FlightStep)
                        if (flightStep.InputImageSizeM != null)
                        {
                            // Draw lines to show the image area seen by the drone.
                            var (topLeftLocn, topRightLocn, bottomRightLocn, bottomLeftLocn) =
                                flightStep.Calculate_InputImageArea_Corners();

                            var topLeftPoint = DroneLocnMToPixelPoint(topLeftLocn);
                            var topRightPoint = DroneLocnMToPixelPoint(topRightLocn);
                            var bottomRightPoint = DroneLocnMToPixelPoint(bottomRightLocn);
                            var bottomLeftPoint = DroneLocnMToPixelPoint(bottomLeftLocn);

                            Line(ref image, topLeftPoint, topRightPoint, activeBgr, NormalThickness);
                            Line(ref image, topRightPoint, bottomRightPoint, activeBgr, NormalThickness);
                            Line(ref image, bottomRightPoint, bottomLeftPoint, activeBgr, NormalThickness);
                            Line(ref image, bottomLeftPoint, topLeftPoint, activeBgr, NormalThickness);

                            // If camera is vertically down then lines from drone to image area
                            // are unnecessary & look ugly. Suppress them for small angles.
                            var degsToVertical = Math.Abs(flightStep.CameraToVerticalForwardDeg);
                            if (degsToVertical < 15)
                            {
                                // Draw lines from drone to image area to "connect" the drone location to the image area.
                                // Todo: Make line dotted.
                                Line(ref image, dronePoint, topLeftPoint, activeBgr);
                                Line(ref image, dronePoint, bottomLeftPoint, activeBgr);
                            }

                            // Draw a cross at the center of the image.
                            // If CameraDownDeg is 90, the drone circle and leg cross will overlap.
                            // Otherwise the cross location helps visualises the impact of CameraDownDeg.
                            Point imageCenter = new(
                                (topLeftPoint.X + bottomRightPoint.X) / 2,
                                (topLeftPoint.Y + bottomRightPoint.Y) / 2);
                            int thickness = (degsToVertical < 5 ? NormalThickness : HighlightThickness);
                            Draw.Cross(ref image, imageCenter, activeBgr, thickness);
                        }
                    }
                }

                return image.Clone();
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawPath.CurrImage", ex);
            }
        }


        // Draw a red cross at the location of the drone flight on the country map 
        public void DrawCountryGraphLocationCross(CountryLocation currCountryLocn, ref Bitmap countryGraphBitmap)
        {
            if (currCountryLocn == null)
                return;

            var currCountryN = currCountryLocn.NorthingM;
            var currCountryE = currCountryLocn.EastingM;

            (var minCountryN, var minCountryE) = NztmProjection.WgsToNztm(-47.5, 166);
            (var maxCountryN, var maxCountryE) = NztmProjection.WgsToNztm(-34.0, 179);

            var crossXFraction = (currCountryE - minCountryE) / (maxCountryE - minCountryE);
            var crossYFraction = (currCountryN - minCountryN) / (maxCountryN - minCountryN);

            var countryGraphImage = countryGraphBitmap.ToImage<Bgr, byte>();

            Point crossCenter = new Point(
                (int)(crossXFraction * countryGraphImage.Width),
                countryGraphImage.Height - (int)(crossYFraction * countryGraphImage.Height));

            Draw.Cross(ref countryGraphImage, crossCenter, DroneColors.ErrorBgr, 3, 20);

            countryGraphBitmap = countryGraphImage.ToBitmap();
        }


        // In the picturebox draw the legend as a scale of colours from startColor to endColor
        public static Image<Bgr, byte> DrawContourLegend(Size size, Color startColor, Color endColor)
        {
            var theShades = GetColorShades(startColor, endColor, NumShades);

            var inc = 1.0f * size.Height / NumShades;

            var image = NewLightGrayImage(size);

            for (int i = 0; i < NumShades; i++)
            {
                Rectangle thisRect = new Rectangle(0, 0, size.Width, (int)inc);
                thisRect.Y = (int)(i * inc);
                image.Draw(thisRect, DroneColors.ColorToBgr(theShades[i]),
                    -1); // If thickness is less than 1, the rectangle is filled up
            }

            // Draw an up triangle centered near the top.
            var center = new Point(size.Width / 2 - 1, size.Width / 2);
            UpTriangle(ref image, center, UpTriangleLen, DroneColors.WhiteBgr, HighlightThickness);

            return image.Clone();
        }
    }
}

