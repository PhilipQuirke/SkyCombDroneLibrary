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

        // Drawing colors for various elements
        public Color DrawPixelColor = DroneColors.PixelColor; // Color.White suppresses drawing
        public Color DrawRealFeatureColor = DroneColors.RealFeatureColor; // Color.White suppresses drawing
        public Color DrawUnrealFeatureColor = DroneColors.UnrealFeatureColor; // Color.White suppresses drawing

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
    };

}