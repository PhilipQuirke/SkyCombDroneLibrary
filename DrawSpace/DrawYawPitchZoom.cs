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

                // We want the small lines to be the same length for yaw and pitch
                int smallPerc = 2;
                int smallPixels = (int)Math.Min(
                    image.Width * smallPerc / 100,
                    image.Height * smallPerc / 100);

                // Draw the drone direction (yaw) at the bottom few % of the image  
                if (flightStep.YawDeg > -180)
                {
                    var middleDeg = (int) flightStep.YawDeg;
                    if (middleDeg < 0)
                        middleDeg += 360;

                    int leftPerc = 10;
                    int rightPerc = 90;

                    int topY = smallPixels;
                    int bottomY = 2 * smallPixels;
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
                    image.Draw(new LineSegment2D(topLeftPt, topRightPt), color, lineThick); // Long line

                    // Draw a two-sided triangle as the location indicator
                    image.Draw(new LineSegment2D(topMiddlePt, new Point(bottomMiddlePt.X - smallPixels, bottomY)), color, lineThick);
                    image.Draw(new LineSegment2D(topMiddlePt, new Point(bottomMiddlePt.X + smallPixels, bottomY)), color, lineThick);

                    // Text is drawn lower.
                    bottomMiddlePt.Y += 12 * fontScale;

                    // If they are in range then draw these compass directions
                    string[] compassDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N", "NE" };
                    int[] degrees = { 0, 45, 90, 135, 180, 225, 270, 315, 360, 405 };

                    var HFOVDeg = drone.InputVideo.HFOVDeg;
                    var leftDeg = middleDeg - HFOVDeg/2;
                    var rightDeg = middleDeg + HFOVDeg/2;

                    // We should draw the direction as "127"in the middle
                    // unless we are heading almost straight say north
                    // in which case we should draw "N" instead 
                    bool drawDirectionDigits = true;

                    // Evaluate whether each compass direction is in range & draw it
                    for (int i = 0; i < compassDirections.Length; i++)
                        if ((leftDeg <= degrees[i]) && (degrees[i] <= rightDeg))
                        {
                            var distDeg = degrees[i] - middleDeg;
                            drawDirectionDigits = drawDirectionDigits && (Math.Abs(distDeg) > 5);
                            var deltaX = distDeg * (topRightPt.X - topLeftPt.X) / HFOVDeg;

                            var pt = new Point(bottomMiddlePt.X + (int)deltaX, bottomMiddlePt.Y);
                            image.Draw(new LineSegment2D(new Point(pt.X, topY), new Point(pt.X, bottomY)), color, lineThick);

                            var textPt = new Point(pt.X - 5 * fontScale * compassDirections[i].Length, bottomMiddlePt.Y);
                            Text(ref image, compassDirections[i], textPt, fontScale, color, fontScale);
                        }

                    if (drawDirectionDigits)
                    {
                        var middleTextPt = new Point(bottomMiddlePt.X - 15 * fontScale, bottomMiddlePt.Y);
                        Text(ref image, middleDeg.ToString(), middleTextPt, fontScale, color, fontScale);
                    }
                }


                if (drone.Config.UseGimbalData && (flightStep.PitchDeg >= -100))
                {
                    // We show the true pitchdeg in text 
                    // but locate it based on a 0 to 90 range.
                    int pitchDeg = - (int)flightStep.PitchDeg;
                    string pitchStr = pitchDeg.ToString();
                    pitchDeg = Math.Max(0, Math.Min(90, pitchDeg));

                    int fromYperc = 10;
                    int toYperc = 90;

                    int fromY = (int)(image.Height * fromYperc / 100);
                    int toY = (int)(image.Height * toYperc / 100);
                    int fromX = smallPixels;
                    int toX = 2 * smallPixels;
                    int middleY = pitchDeg * (toY - fromY) / 90 + fromY;

                    var topLeftPt = new Point(fromX, fromY);
                    var topRightPt = new Point(toX, fromY);
                    var middleLeftPt = new Point(fromX, middleY);
                    var bottomRightPt = new Point(toX, toY);
                    var bottomLeftPt = new Point(fromX, toY);
                    var middleRightPt = new Point(toX, middleY);
                    image.Draw(new LineSegment2D(topLeftPt, topRightPt), color, 3);
                    image.Draw(new LineSegment2D(bottomLeftPt, bottomRightPt), color, 3);
                    image.Draw(new LineSegment2D(topLeftPt, bottomLeftPt), color, 2); // Long line

                    image.Draw(new LineSegment2D(middleLeftPt, new Point(middleRightPt.X, middleY - smallPixels)), color, 3);
                    image.Draw(new LineSegment2D(middleLeftPt, new Point(middleRightPt.X, middleY + smallPixels)), color, 3);

                    middleRightPt.Y += 10;
                    middleRightPt.X += 10;
                    Text(ref image, pitchStr, middleRightPt, 2, color, 2);
                }
            }
            catch (Exception ex)
            {
                throw ThrowException("DrawYawPitchZoom.Draw", ex);
            }
        }
    }
}

