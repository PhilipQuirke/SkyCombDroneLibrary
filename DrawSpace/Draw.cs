// Copyright SkyComb Limited 2024. All rights reserved.
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using SkyCombDrone.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.DrawSpace
{
    // Code to draw stuff on images
    public class Draw : DataConstants
    {
        public const int NormalThickness = 1;
        public const int HighlightThickness = 2;
        public const int FocusThickness = 2;
        public const int FocusRadius = 6;

        // Standard length of an "up" Triangle side.
        public const int UpTriangleLen = 12;


        // Create a new rectangle        
        public static Image<Bgr, byte> NewImage(Size size, Bgr color)
        {
            return new Image<Bgr, byte>(size.Width, size.Height, color);
        }


        // Create a new lightgray rectangle        
        public static Image<Bgr, byte> NewLightGrayImage(Size size)
        {
            return NewImage(size, DroneColors.GrayBgr);
        }


        // Draw a line segment in specified color
        public static void Line(ref Image<Bgr, byte> image, PointF from, PointF to, Bgr color, int thickness = NormalThickness)
        {
            image.Draw(new LineSegment2DF(from, to), color, thickness);
        }


        // Draw a circle in specified color
        public static void Circle(ref Image<Bgr, byte> image, Point point, Bgr color, int lineThicknessScale = FocusThickness, int radius = FocusRadius)
        {
            CvInvoke.Circle(image, point, radius, color.MCvScalar, lineThicknessScale);
        }
        public static void Circle(DrawImageConfig config, ref Image<Bgr, byte> image, Point point)
        {
            if (config.DrawRealFeatureColor == Color.White)
                return;

            Circle(ref image, point, DroneColors.ColorToBgr(config.DrawRealFeatureColor));
        }


        // Draw a cross in specified color
        public static void Cross(ref Image<Bgr, byte> image, Point center, Bgr color, int lineThickness = NormalThickness, int crossWidth = 4)
        {
            Line(ref image, new(center.X - crossWidth, center.Y), new(center.X + crossWidth, center.Y), color, lineThickness);
            Line(ref image, new(center.X, center.Y - crossWidth), new(center.X, center.Y + crossWidth), color, lineThickness);
        }


        // Draw an "ip" triangle in specified color
        public static void UpTriangle(ref Image<Bgr, byte> image, Point center, int width, Bgr color, int lineThickness = NormalThickness)
        {
            int halfWidth = width / 2;

            var bottomLeft = new PointF(center.X - halfWidth, center.Y + halfWidth);
            var bottomRight = new PointF(center.X + halfWidth, center.Y + halfWidth);
            var top = new PointF(center.X, center.Y - halfWidth);

            Line(ref image, bottomLeft, bottomRight, color, lineThickness);
            Line(ref image, bottomRight, top, color, lineThickness);
            Line(ref image, top, bottomLeft, color, lineThickness);
        }


        // Draw text  
        // The available OpenCV fonts are described in https://codeyarns.com/tech/2015-03-11-fonts-in-opencv.html
        public static void Text(ref Image<Bgr, byte> image, string text, Point point, double fontScale, Bgr color, int thickness = 1)
        {
            image.Draw(text, point, FontFace.HersheySimplex, fontScale, color, thickness);
        }


        public static void NoDataText(ref Image<Bgr, byte> image, Point where)
        {
            Text(ref image, "No data", where, 1, DroneColors.BlackBgr);
        }


        // Return an image containing a text message
        public static Image<Bgr, byte> Message(Size size, string message, Bgr color, int fontScale = 1)
        {
            // Create a lightgray rectangle
            var image = NewLightGrayImage(size);

            Text(ref image, message, new Point(50, 50), fontScale, color);

            return image;
        }


        // Draw a bounding rectangle. If thickness is less than 1, the rectangle is filled up 
        public static void BoundingRectangle(DrawImageConfig config, ref Image<Bgr, byte> image, Rectangle boundingRect, Color color, int thickness, int inflate)
        {
            if (color == Color.White)
                return;

            if (inflate>0)
                boundingRect.Inflate(inflate, inflate);
            
            image.Draw(boundingRect, DroneColors.ColorToBgr(color), thickness);
        }


        // Return a range of colour shades 
        public static List<Color> GetColorShades(Color startColor, Color endColor, int numShades = 20)
        {
            var rDiff = (endColor.R - startColor.R) / (numShades - 1);
            var gDiff = (endColor.G - startColor.G) / (numShades - 1);
            var bDiff = (endColor.B - startColor.B) / (numShades - 1);
            List<Color> theShades = new List<Color>();
            for (int i = 0; i < numShades; i++)
            {
                var r = startColor.R + i * rDiff;
                var g = startColor.G + i * gDiff;
                var b = startColor.B + i * bDiff;
                theShades.Add(Color.FromArgb((byte)r, (byte)g, (byte)b));
            }
            return theShades;
        }
    }
}
