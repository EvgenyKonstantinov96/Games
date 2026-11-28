using UnityEngine;

namespace ZombieSurvivalRun
{
    internal sealed class ZSRCampaignStyle
    {
        internal readonly string Title, SkyResource, Landmark, RoadSign;
        internal readonly Color Accent, Fog, Key, Fill;
        internal readonly float KeyIntensity, FogDensity;

        static readonly ZSRCampaignStyle[] Chapters =
        {
            new ZSRCampaignStyle("Руины мегаполиса", "Horizon", "ruins", "EAST VIADUCT",
                new Color(1f, .69f, .30f), new Color(.24f, .29f, .38f),
                new Color(1f, .68f, .44f), new Color(.53f, .70f, 1f), 1.36f, .0125f),
            new ZSRCampaignStyle("Огненный город", "Horizon_Industrial", "industrial", "PIPEWORKS",
                new Color(1f, .39f, .24f), new Color(.48f, .27f, .22f),
                new Color(1f, .53f, .31f), new Color(.56f, .48f, .70f), 1.57f, .0136f),
            new ZSRCampaignStyle("Эвакуационная зона", "Horizon_Evacuation", "evacuation", "EVAC 03",
                new Color(.37f, .91f, .91f), new Color(.22f, .29f, .38f),
                new Color(1f, .74f, .52f), new Color(.44f, .67f, .92f), 1.42f, .0115f)
        };

        ZSRCampaignStyle(string title, string sky, string landmark, string roadSign, Color accent, Color fog,
            Color key, Color fill, float keyIntensity, float fogDensity)
        {
            Title = title;
            SkyResource = "ProductionArt/" + sky;
            Landmark = landmark;
            RoadSign = roadSign;
            Accent = accent;
            Fog = fog;
            Key = key;
            Fill = fill;
            KeyIntensity = keyIntensity;
            FogDensity = fogDensity;
        }

        internal static ZSRCampaignStyle For(int chapter) => Chapters[Mathf.Clamp(chapter, 0, Chapters.Length - 1)];
    }
}
