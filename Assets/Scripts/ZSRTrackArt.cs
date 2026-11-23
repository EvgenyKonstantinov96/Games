using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>One shared material for the UV-mapped road hazards and gate pylons.</summary>
    public static class ZSRTrackArt
    {
        static Material paintedMetal;

        public static Material Surface
        {
            get
            {
                if (paintedMetal == null)
                {
                    paintedMetal = ZSRVisuals.MatWithTexture(Color.white, "ProductionArt/TrackPropAtlas", .31f,
                        missingTextureColor: new Color(.24f, .27f, .3f));
                    paintedMetal.name = "Track prop surface / shared";
                }
                return paintedMetal;
            }
        }
    }
}
