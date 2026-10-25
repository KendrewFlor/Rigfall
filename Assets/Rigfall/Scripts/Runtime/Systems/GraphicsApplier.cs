// Low / Medium / High graphics presets. Applied at boot and whenever Settings change.
//
//            Shadows        Shadow distance  MSAA  Particles  Decor   Pixel lights
//   Low      off            -                off   40%        half    0
//   Medium   hard           45 m             2x    70%        full    1
//   High     soft           70 m             4x    100%       full    2
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class GraphicsApplier
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };

        public static void Apply(GameSettings s)
        {
            int q = Mathf.Clamp(s.graphicsQuality, 0, 2);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = s.fpsCap;
            switch (q)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.pixelLightCount = 0;
                    Fx.SetDensity(0.4f);
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 45f;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.pixelLightCount = 1;
                    Fx.SetDensity(0.7f);
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 70f;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.antiAliasing = 4;
                    QualitySettings.pixelLightCount = 2;
                    Fx.SetDensity(1f);
                    break;
            }
            var run = Run.Current;
            if (run != null && run.Field != null && run.Field.Sun != null)
                run.Field.Sun.shadows = q == 0 ? LightShadows.None : q == 1 ? LightShadows.Hard : LightShadows.Soft;
        }
    }
}
