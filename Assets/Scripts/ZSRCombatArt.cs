using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Shared material for the reference field kit and white quadcopter.</summary>
    static class ZSRCombatArt
    {
        static Material surface;
        internal static Material Surface
        {
            get
            {
                if (surface != null) return surface;
                surface = ZSRVisuals.MatWithTexture(Color.white,"ProductionArt/CombatAtlas",.4f);
                var packed = Resources.Load<Texture2D>("ProductionArt/CombatAtlas_surface");
                if (packed != null && surface.HasProperty("_MetallicGlossMap"))
                {
                    surface.SetTexture("_MetallicGlossMap",packed);
                    surface.SetFloat("_Smoothness",1f);
                    surface.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                return surface;
            }
        }
    }
}
