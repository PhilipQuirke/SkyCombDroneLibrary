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
        // In the ObjectCategoryForm we may draw on the optical image with extra scale
        public int TextExtraScale = 1; // Expand the object name text 
        public int BoxExtraScale = 1; // Expand the object bounding box
        public float expandX = 1.0f; // Change x coordinates proportionately
        public float expandY = 1.0f; // Change y coordinates proportionately
        public float LineThickness = 1.0f; // Allow change of line thickness

        // Optional optical alignment calibration applied after thermal->optical scaling.
        // Values near 1.0 keep geometry almost unchanged.
        public float OpticalCenterScaleX = 1.0f;
        public float OpticalCenterScaleY = 1.0f;
        public float OpticalBoxSizeScale = 1.0f; // proportionate growth/shrink around mapped box center
        public float OpticalOffsetXPct = 0.0f; // fraction of output width (+right, -left)
        public float OpticalOffsetYPct = 0.0f; // fraction of output height (+down, -up)

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
            answer.expandX = expandX;
            answer.expandY = expandY;
            answer.LineThickness = LineThickness;
            answer.OpticalCenterScaleX = OpticalCenterScaleX;
            answer.OpticalCenterScaleY = OpticalCenterScaleY;
            answer.OpticalBoxSizeScale = OpticalBoxSizeScale;
            answer.OpticalOffsetXPct = OpticalOffsetXPct;
            answer.OpticalOffsetYPct = OpticalOffsetYPct;

            return answer;
        }
    };

}