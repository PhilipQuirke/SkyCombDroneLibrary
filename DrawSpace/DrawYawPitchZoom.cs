// Copyright SkyComb Limited 2023. All rights reserved.
using Emgu.CV;
using Emgu.CV.Structure;
using SkyCombDrone.CommonSpace;
using SkyCombDrone.DroneLogic;
using System.Drawing;



namespace SkyCombDrone.DrawSpace
{
    // Code to draw images related to drone flight yaw, pitch and zoom
    public class DrawYawPitchZoom : Draw
    {

        public static void Draw(ref Image<Bgr, byte> image, Drone drone, FlightStep flightStep)
        {
            try
            {
                if((image == null) || (flightStep==null) || (drone==null) || (drone.InputVideo==null))
                    return;

                var color = DroneColors.InScopeDroneBgr;
                var fontScale = drone.InputVideo.FontScale;
                var lineThick = 1 + fontScale;

                // Draw the drone direction (yaw) at the bottom few % of the image  
                if (flightStep.YawDeg > -180)
                {
                    var middleDeg = (int) flightStep.YawDeg;
                    if (middleDeg < 0)
                        middleDeg += 360;

                    int topPerc = 2;
                    int bottomPerc = 4;
                    int leftPerc = 10;
                    int rightPerc = 90;

                    int topY = (int)(image.Height * topPerc / 100);
                    int bottomY = (int)(image.Height * bottomPerc / 100);
                    int leftX = (int)(image.Width * leftPerc / 100);
                    int rightX = (int)(image.Width * rightPerc / 100);

                    // Draw the static "wide M" shape
                    var topLeftPt = new Point(leftX, topY);
                    var topRightPt = new Point(rightX, topY);
                    var topMiddlePt = new Point((leftX + rightX) / 2, topY);
                    var bottomRightPt = new Point(rightX, bottomY);
                    var bottomLeftPt = new Point(leftX, bottomY);
                    var bottomMiddlePt = new Point((leftX + rightX) / 2, bottomY);
                    image.Draw(new LineSegment2D(topLeftPt, bottomLeftPt), color, lineThick);
                    image.Draw(new LineSegment2D(topRightPt, bottomRightPt), color, lineThick);
                    image.Draw(new LineSegment2D(topMiddlePt, bottomMiddlePt), color, lineThick);
                    image.Draw(new LineSegment2D(topLeftPt, topRightPt), color, lineThick); // Long line

                    // Text is drawn lower.
                    bottomMiddlePt.Y += 12 * fontScale;
                    var middleTextPt = new Point(bottomMiddlePt.X - 15 * fontScale, bottomMiddlePt.Y);
                    Text(ref image, middleDeg.ToString(), middleTextPt, fontScale, color, fontScale);

                    // If they are in range then draw these compass directions
                    string[] compassDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N", "NE" };
                    int[] degrees = { 0, 45, 90, 135, 180, 225, 270, 315, 360, 405 };

                    var HFOVDeg = drone.InputVideo.HFOVDeg;
                    var leftDeg = middleDeg - HFOVDeg/2;
                    var rightDeg = middleDeg + HFOVDeg/2;

                    // Evaluate whether each compass direction is in range & draw it
                    for (int i = 0; i < compassDirections.Length; i++)
                        if ((leftDeg <= degrees[i]) && (degrees[i] <= rightDeg))
                        {
                            var distDeg = degrees[i] - middleDeg;
                            var deltaX = distDeg * (topRightPt.X - topLeftPt.X) / HFOVDeg;

                            var pt = new Point(bottomMiddlePt.X + (int)deltaX, bottomMiddlePt.Y);
                            image.Draw(new LineSegment2D(new Point(pt.X, topY), new Point(pt.X, bottomY)), color, lineThick);

                            var textPt = new Point(pt.X - 5 * fontScale * compassDirections[i].Length, bottomMiddlePt.Y);
                            Text(ref image, compassDirections[i], textPt, fontScale, color, fontScale);
                        }
                }

                if(drone.Config.UseGimbalData && (flightStep.PitchDeg >= -100))
                {
                    int fromYperc = 10;
                    int toYperc = 90;
                    int fromXperc = 2;
                    int toXperc = 4;

                    int fromY = (int)(image.Height * fromYperc / 100);
                    int toY = (int)(image.Height * toYperc / 100);
                    int fromX = (int)(image.Width * fromXperc / 100);
                    int toX = (int)(image.Width * toXperc / 100);

                    var topLeftPt = new Point(fromX, fromY);
                    var topRightPt = new Point(toX, fromY);
                    var middleLeftPt = new Point(fromX, (fromY + toY)/2);
                    var bottomRightPt = new Point(toX, toY);
                    var bottomLeftPt = new Point(fromX, toY);
                    var middleRightPt = new Point(toX, (fromY + toY) / 2);
                    image.Draw(new LineSegment2D(topLeftPt, topRightPt), color, 3);
                    image.Draw(new LineSegment2D(middleLeftPt, middleRightPt), color, 3);
                    image.Draw(new LineSegment2D(bottomLeftPt, bottomRightPt), color, 3);
                    image.Draw(new LineSegment2D(topLeftPt, bottomLeftPt), color, 2); // Long line

                    middleRightPt.Y += 10;
                    middleRightPt.X += 10;
                    Text(ref image, ((int)flightStep.PitchDeg).ToString(), middleRightPt, 2, color, 2);
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawYawPitchZoom.Draw", ex);
            }
        }
    }
}

