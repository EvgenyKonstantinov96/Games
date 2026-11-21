using UnityEngine;

namespace ZombieSurvivalRun
{
    /// <summary>Real imported model placement. Gameplay, collisions, and road chunk scrolling stay unchanged.</summary>
    public static class ZSRPolyforkWorld
    {
        static ZSRProductionArtSettings Settings => Resources.Load<ZSRProductionArtSettings>("ProductionArtSettings");

        public static bool TryBuilding(Transform parent, int index, float width, float height)
        {
            var s = Settings;
            if (s == null) return false;
            bool high = (index % 3) == 0;
            GameObject prefab = high ? s.towerBuildingPrefab : s.houseBuildingPrefab;
            if (prefab == null) prefab = s.towerBuildingPrefab != null ? s.towerBuildingPrefab : s.houseBuildingPrefab;
            if (prefab == null) return false;
            // Polyfork: tower ~16 x 41 x 12m, brownstone ~8 x 17 x 8m.
            float baseWidth = prefab == s.towerBuildingPrefab ? 16f : 8f;
            float scale = Mathf.Clamp(width / baseWidth, .32f, 1.04f);
            var obj = Object.Instantiate(prefab, parent, false);
            if (obj.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Object.Destroy(obj);
                return false;
            }
            obj.name = "Polyfork / city facade " + index;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            obj.transform.localScale = Vector3.one * scale;
            // Align imported ground plane. Polyfork meshes may have their origin offset
            // from the first visible vertex; an unfixed origin makes floating buildings.
            var bounds = obj.GetComponentsInChildren<Renderer>(true);
            if (bounds.Length > 0)
            {
                float lowest = float.MaxValue;
                foreach (var r in bounds) lowest = Mathf.Min(lowest,r.bounds.min.y);
                obj.transform.position += Vector3.up * (parent.position.y-lowest);
            }
            return true;
        }

        public static bool TryObstacle(Transform parent, int kind)
        {
            var s = Settings;
            if (s == null) return false;
            GameObject prefab = kind == 0 ? s.townCarPrefab : kind == 1 ? s.concreteBarrierPrefab : null;
            if (prefab == null) return false;
            var obj = Object.Instantiate(prefab, parent, false);
            if (obj.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Object.Destroy(obj);
                return false;
            }
            obj.name = kind == 0 ? "Polyfork / abandoned town car" : "Polyfork / jersey barrier";
            // A sedan is intentionally narrowed to the existing lane-hitbox.
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            obj.transform.localScale = Vector3.one * (kind == 0 ? .65f : 1.23f);
            // Keep the collision silhouette centred in the chosen road lane.
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++) combined.Encapsulate(renderers[i].bounds);
                obj.transform.position += new Vector3(parent.position.x-combined.center.x,
                    parent.position.y-combined.min.y, parent.position.z-combined.center.z);
            }
            return true;
        }
    }
}
