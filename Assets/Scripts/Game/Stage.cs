using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// The 180x320 pixel stage. Coordinates named "stage" are in art pixels from the top left;
    /// the whole stage is shown at an integer scale in the middle of the window.
    /// </summary>
    public static class Stage
    {
        public const int Width = 180;
        public const int Height = 320;
        public const float PixelsPerUnit = 16f;
        public const int TopBarHeight = 32;
        public const int BottomPanelTop = 224;

        public static int Scale => Mathf.Max(1, Mathf.Min(Screen.width / Width, Screen.height / Height));

        public static Vector2 ScreenToStage(Vector2 screen)
        {
            int s = Scale;
            float originX = (Screen.width - Width * s) / 2f;
            float originY = (Screen.height - Height * s) / 2f;
            return new Vector2((screen.x - originX) / s, Height - (screen.y - originY) / s);
        }

        public static Vector2 StageToWorld(Vector2 stage, Vector2 cameraPos) =>
            new Vector2(
                cameraPos.x + (stage.x - Width / 2f) / PixelsPerUnit,
                cameraPos.y + (Height / 2f - stage.y) / PixelsPerUnit);

        public static Vector2 WorldToStage(Vector2 world, Vector2 cameraPos) =>
            new Vector2(
                (world.x - cameraPos.x) * PixelsPerUnit + Width / 2f,
                Height / 2f - (world.y - cameraPos.y) * PixelsPerUnit);

        public static float SnapToPixel(float v) => Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;
    }
}
