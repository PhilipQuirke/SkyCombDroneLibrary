// Copyright SkyComb Limited 2023. All rights reserved.
using SkyCombDrone.CommonSpace;
using SkyCombGround.CommonSpace;
using System.Drawing;

namespace SkyCombDrone.DrawSpace
{

    // Configuration settings related to drawing images.
    public class DrawImageConfig : ConfigBase
    {
        // Color map type (aka color palette) to apply to background of video.
        // Takes values "None" and the 21 values defined in https://docs.opencv.org/3.4/d3/d50/group__imgproc__colormap.html e.g. Magma
        public string Palette { get; set; } = "bone";
        // Bounding rectangle padding in pixels. For example, 3
        public int AreaPadding { get; set; } = 2;
        // Bounding rectangle persistance in frames. For example, 1
        public int AreaPersistence { get; set; } = 10;
        public int FlowHotPixels { get; set; } = 100;


        public Color DrawPixelColor = DroneColors.PixelColor;
        public Color DrawRealFeatureColor = DroneColors.RealFeatureColor;
        public Color DrawUnrealFeatureColor = DroneColors.UnrealFeatureColor;



        // Converts Config.DrawColorMap to OpenCV's ColorMapType.
        public Emgu.CV.CvEnum.ColorMapType DrawPaletteToEnum()
        {
            return ConfigBase.CleanString(Palette) switch
            {
                "autumn" => Emgu.CV.CvEnum.ColorMapType.Autumn,
                "bone" => Emgu.CV.CvEnum.ColorMapType.Bone,
                "jet" => Emgu.CV.CvEnum.ColorMapType.Jet,
                "winter" => Emgu.CV.CvEnum.ColorMapType.Winter,
                "rainbow" => Emgu.CV.CvEnum.ColorMapType.Rainbow,
                "ocean" => Emgu.CV.CvEnum.ColorMapType.Ocean,
                "summer" => Emgu.CV.CvEnum.ColorMapType.Summer,
                "spring" => Emgu.CV.CvEnum.ColorMapType.Spring,
                "cool" => Emgu.CV.CvEnum.ColorMapType.Cool,
                "hsv" => Emgu.CV.CvEnum.ColorMapType.Hsv,
                "pink" => Emgu.CV.CvEnum.ColorMapType.Pink,
                "hot" => Emgu.CV.CvEnum.ColorMapType.Hot,
                "parula" => Emgu.CV.CvEnum.ColorMapType.Parula,
                "magma" => Emgu.CV.CvEnum.ColorMapType.Magma,
                "inferno" => Emgu.CV.CvEnum.ColorMapType.Inferno,
                "plasma" => Emgu.CV.CvEnum.ColorMapType.Plasma,
                "viridis" => Emgu.CV.CvEnum.ColorMapType.Viridis,
                "cividis" => Emgu.CV.CvEnum.ColorMapType.Cividis,
                "twilight" => Emgu.CV.CvEnum.ColorMapType.Twilight,
                "twilightshifted" => Emgu.CV.CvEnum.ColorMapType.TwilightShifted,
                "turbo" => Emgu.CV.CvEnum.ColorMapType.Turbo,
                _ => throw BaseConstants.ThrowException("DrawConfig.ColorMapType: Bad value: " + Palette),
            };
        }


        public DrawImageConfig Clone()
        {
            DrawImageConfig answer = new();

            answer.Palette = Palette;
            answer.AreaPadding = AreaPadding;
            answer.AreaPersistence = AreaPersistence;
            answer.FlowHotPixels = FlowHotPixels;

            answer.DrawPixelColor = DrawPixelColor;
            answer.DrawRealFeatureColor = DrawRealFeatureColor;
            answer.DrawUnrealFeatureColor = DrawUnrealFeatureColor;

            return answer;
        }


        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Draw Palette", Palette },
                { "Draw Area Padding", AreaPadding },
                { "Draw Area Persistence", AreaPersistence },
                { "Draw Flow Hot Pixels", FlowHotPixels },

            };
        }


        // Load this object's settings from strings (loaded from a spreadsheet)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            int i = 0;
            Palette = CleanString(settings[i++]);
            AreaPadding = StringToNonNegInt(settings[i++]);
            AreaPersistence = StringToNonNegInt(settings[i++]);
            FlowHotPixels = StringToNonNegInt(settings[i++]);
        }
    };

}