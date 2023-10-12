using Emgu.CV.Structure;
using SkyCombGround.CommonSpace;
using System.Drawing;


namespace SkyCombDrone.CommonSpace
{
    public class DroneColors : GroundColors
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
        public static Color LegNameColor = InScopeDroneColor;


        // Hot object and feature colours are red, orange & yellow
        public static Color InScopeObjectColor = Color.Red;
        public static Color OutScopeObjectColor = Color.DarkGray;
        public static Color RealFeatureColor = Color.Orange;
        public static Color UnrealFeatureColor = Color.Yellow; // Note Color.White means no-color
        public static Color PixelColor = Color.Green; // Note Color.White means no-color


        // Some libraries need BGR colors
        static public Bgr ColorToBgr(Color theColor) { return new Bgr(theColor.B, theColor.G, theColor.R); }
        public static Bgr ActiveDroneBgr { get { return ColorToBgr(ActiveDroneColor); } }
        public static Bgr InScopeDroneBgr { get { return ColorToBgr(InScopeDroneColor); } }
        public static Bgr OutScopeDroneBgr { get { return ColorToBgr(OutScopeDroneColor); } }
        public static Bgr LegNameBgr { get { return ColorToBgr(LegNameColor); } }
        public static Bgr InScopeObjectBgr { get { return ColorToBgr(InScopeObjectColor); } }
        public static Bgr OutScopeObjectBgr { get { return ColorToBgr(OutScopeObjectColor); } }
        public static Bgr RealFeatureBgr { get { return ColorToBgr(RealFeatureColor); } }
        public static Bgr UnrealFeatureBgr { get { return ColorToBgr(UnrealFeatureColor); } }
        public static Bgr PixelBgr { get { return ColorToBgr(PixelColor); } }


        static public Bgr WhiteBgr { get; } = new Bgr(255, 255, 255);
        static public Bgr GrayBgr { get; } = new Bgr(240, 240, 240);
        static public Bgr DarkGrayBgr { get; } = new Bgr(106, 102, 99);
        static public Bgr BlackBgr { get; } = new Bgr(0, 0, 0);
        static public Bgr GreenBgr { get; } = new Bgr(0, 128, 0);
        static public Bgr ErrorBgr { get; } = new Bgr(0, 0, 255);
    }
}
