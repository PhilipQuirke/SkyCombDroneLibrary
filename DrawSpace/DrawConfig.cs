// Copyright SkyComb Limited 2024. All rights reserved.
using SkyCombDrone.CommonSpace;
using SkyCombGround.CommonSpace;
using System.Drawing;

namespace SkyCombDrone.DrawSpace
{

    // Configuration settings related to drawing images.
    public class DrawImageConfig : ConfigBase
    {
        // Bounding rectangle padding in pixels. For example, 3
        public int AreaPadding { get; set; } = 2;
        // Bounding rectangle persistance in frames. For example, 1
        public int AreaPersistence { get; set; } = 10;

        public Color DrawPixelColor = DroneColors.PixelColor;
        public Color DrawRealFeatureColor = DroneColors.RealFeatureColor;
        public Color DrawUnrealFeatureColor = DroneColors.UnrealFeatureColor;

        // In the ObjectCategoryForm we may expand the object name text and bounding box
        public int TextExtraScale = 1; // Expand the object name text 
        public int BoxExtraScale = 1; // Expand the object bounding box

        public DrawImageConfig Clone()
        {
            DrawImageConfig answer = new();

            answer.AreaPadding = AreaPadding;
            answer.AreaPersistence = AreaPersistence;

            answer.DrawPixelColor = DrawPixelColor;
            answer.DrawRealFeatureColor = DrawRealFeatureColor;
            answer.DrawUnrealFeatureColor = DrawUnrealFeatureColor;

            answer.TextExtraScale = TextExtraScale;
            answer.BoxExtraScale = BoxExtraScale;

            return answer;
        }


        public DataPairList GetSettings()
        {
            return new DataPairList
            {
                { "Draw Area Padding", AreaPadding },
                { "Draw Area Persistence", AreaPersistence },
            };
        }


        // Load this object's settings from strings (loaded from a spreadsheet)
        // This function must align to the above GetSettings function.
        public void LoadSettings(List<string> settings)
        {
            int i = 0;
            AreaPadding = StringToNonNegInt(settings[i++]);
            AreaPersistence = StringToNonNegInt(settings[i++]);
        }
    };

}