using Emgu.CV.Structure;
using System.Drawing;


namespace SkyCombDrone.CommonSpace
{
    public class Colors
    {
        // Overall color approach is:
        // - Drone path/image area          Dull-Blue/Bright-Blue/Gray
        // - Ground (aka DEM elevation)     Brown shades
        // - Trees (DSM elevation)          Dull Green shades
        // - Objects/features/pixels        Red/Orange/Yellow/BrightGreen

        // Drone colors are shades of blue
        // Refer https://htmlcolorcodes.com/colors/shades-of-blue/
        public static Color ActiveDroneColor = Color.FromArgb(31, 81, 255);
        public static Color InScopeDroneColor = Color.FromArgb(126, 226, 255);
        public static Color OutScopeDroneColor = Color.DarkGray;

        // Drone leg text color is a shade of blue
        public static Color LegNameColor = InScopeDroneColor; //  Pink: Color.FromArgb(255, 174, 247);

        // Surface (aka Tree Top) color is used for the DSM elevation
        public static Color SurfaceLineColor = Color.DarkGreen; // Line on elevation graphs
        public static Color SurfaceHighColor = Color.FromArgb(198, 224, 197); // Light green. 
        public static Color SurfaceLowColor = Color.FromArgb(36, 62, 40); // Dark green. 

        // Ground color is used for the DEM elevation
        public static Color GroundLineColor = Color.Brown;
        public static Color GroundHighColor = Color.FromArgb(224, 210, 197); // Light brown
        public static Color GroundLowColor = Color.FromArgb(62, 40, 36); // Dark brown

        // Hot object and feature colours are red, orange & yellow
        public static Color InScopeObjectColor = Color.Red;
        public static Color OutScopeObjectColor = Color.DarkGray;
        public static Color RealFeatureColor = Color.Orange;
        public static Color UnrealFeatureColor = Color.Yellow; // Note Color.White means no-color
        public static Color PixelColor = Color.Green; // Note Color.White means no-color

        // Some libraries need BGR colors
        static public Bgr ColorToBgr(Color theColor) { return new Bgr(theColor.B, theColor.G, theColor.R); }
        static public Bgr BlackBgr { get; } = new Bgr(0, 0, 0);
        static public Bgr GreenBgr { get; } = new Bgr(0, 128, 0);
        static public Bgr ErrorBgr { get; } = new Bgr(0, 0, 255);
        static public Bgr WhiteBgr { get; } = new Bgr(255, 255, 255);


        public static Color MixColors(Color fromColor, float fromFraction, Color toColor)
        {
            var toFraction = 1.0f - fromFraction;

            return Color.FromArgb(
                (int)(fromColor.R * fromFraction + toColor.R * toFraction),
                (int)(fromColor.G * fromFraction + toColor.G * toFraction),
                (int)(fromColor.B * fromFraction + toColor.B * toFraction));
        }
    }
}
