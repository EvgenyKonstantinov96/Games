using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieSurvivalRun
{
    /// <summary>Run-owned pooled tracer and particle effects. All timings use the run clock.</summary>
    sealed class ZSRCombatEffects : MonoBehaviour
    {
        const int TraceCount = 24;
        static readonly Vector3[] Circle = BuildCircle();
        readonly TraceSlot[] traces = new TraceSlot[TraceCount];
        readonly List<BlastRing> blasts = new();
        int nextTrace;
        Material lineMaterial, particleMaterial;
        ParticleSystem particles;
        GameObject medkit;
        float medkitLeft;

        struct TraceSlot
        {
            public LineRenderer Line;
            public Vector3 Start, End;
            public Color Color;
            public float Age, Width;
        }

        sealed class BlastRing
        {
            public LineRenderer Line;
            public float Age, Radius;
        }

        void Awake()
        {
            var shader = Resources.Load<Shader>("ProductionArt/ZSRCombatFX");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            lineMaterial = new Material(shader) { name = "Combat streaks and rings" };
            particleMaterial = new Material(shader) { name = "Combat particles" };
            var soft = Resources.Load<Texture2D>("ProductionArt/ParticleSoft");
            if (soft != null)
            {
                if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", soft);
                else particleMaterial.mainTexture = soft;
            }

            particles = ZSRVisuals.Pivot("Combat particles", transform, Vector3.zero).gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = 192;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .18f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.57f, .52f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        LineRenderer MakeLine(string name, int count)
        {
            var line = ZSRVisuals.Pivot(name, transform, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = count;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }

        public void Streak(Vector3 from, Vector3 to, Color color, float width)
        {
            if (lineMaterial == null || (to - from).sqrMagnitude < .001f) return;
            ref var slot = ref traces[nextTrace];
            nextTrace = (nextTrace + 1) % traces.Length;
            if (slot.Line == null) slot.Line = MakeLine("Pooled bullet tracer", 2);
            slot.Start = from;
            slot.End = to;
            slot.Width = width;
            slot.Color = color;
            slot.Age = 0;
            slot.Line.enabled = true;
            UpdateTrace(ref slot);
        }

        static void UpdateTrace(ref TraceSlot slot)
        {
            float t = slot.Age / .095f;
            if (t >= 1f) { slot.Line.enabled = false; return; }
            slot.Line.SetPosition(0, Vector3.Lerp(slot.Start, slot.End, t * .88f));
            slot.Line.SetPosition(1, Vector3.Lerp(slot.Start, slot.End, .36f + t * .69f));
            slot.Line.startColor = new Color(slot.Color.r, slot.Color.g, slot.Color.b, .1f * (1f - t));
            slot.Line.endColor = new Color(slot.Color.r, slot.Color.g, slot.Color.b, 1f - t);
            slot.Line.startWidth = slot.Width * .38f;
            slot.Line.endWidth = slot.Width;
        }

        public void Hit(Vector3 point, bool armored)
        {
            Burst(point, armored ? new Color(.45f, .92f, 1f, .91f) : new Color(1f, .26f, .095f, .90f),
                armored ? 6 : 3, .035f, 2.2f, .24f);
        }

        public void Explode(Vector3 at, float radius)
        {
            Burst(at + Vector3.up * .15f, new Color(1f, .43f, .09f, .92f), 16, .39f, 6.6f, .40f);
            Burst(at + Vector3.up * .25f, new Color(.32f, .27f, .25f, .62f), 22, .66f, 3.8f, .85f);
            Burst(at + Vector3.up * .32f, new Color(1f, .78f, .25f), 25, .072f, 10.5f, .53f);
            if (lineMaterial == null) return;
            var line = MakeLine("Grenade shockwave", Circle.Length);
            line.loop = true;
            line.useWorldSpace = false;
            line.SetPositions(Circle);
            line.transform.position = at + Vector3.up * .015f;
            line.transform.localScale = Vector3.one * .18f;
            line.widthMultiplier = .12f;
            line.startColor = line.endColor = new Color(1f, .64f, .19f, .86f);
            blasts.Add(new BlastRing { Line = line, Radius = radius });
        }

        public void Heal(Vector3 hand, Vector3 body, bool showKit)
        {
            if (showKit)
            {
                if (medkit == null)
                {
                    medkit = ZSRVisuals.MeshPart("combat_medkit", "Field medkit", transform,
                        Vector3.zero, Vector3.one, ZSRTrackArt.Surface);
                    var white = ZSRVisuals.White;
                    ZSRVisuals.Box("Medical cross", medkit.transform, new Vector3(0, .057f, 0), new Vector3(.11f, .005f, .032f), white);
                    ZSRVisuals.Box("Medical cross stem", medkit.transform, new Vector3(0, .058f, 0), new Vector3(.032f, .005f, .10f), white);
                }
                medkit.SetActive(true);
                medkitLeft = .76f;
                medkit.transform.SetPositionAndRotation(hand, Quaternion.Euler(53f, 0, -18f));
            }
            Burst(body, new Color(.28f, 1f, .60f, .80f), 20, .095f, 2.25f, .75f);
        }

        public TrailRenderer GrenadeTrail(Transform grenade)
        {
            if (lineMaterial == null) return null;
            var trail = grenade.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.time = .19f;
            trail.minVertexDistance = .08f;
            trail.startWidth = .17f;
            trail.endWidth = .025f;
            trail.startColor = new Color(1f, .70f, .28f, .95f);
            trail.endColor = new Color(1f, .28f, .06f, 0);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            return trail;
        }

        void Burst(Vector3 at, Color color, int count, float size, float speed, float lifetime)
        {
            if (particles == null) return;
            if (!particles.isPlaying) particles.Play();
            for (int i = 0; i < count; i++)
            {
                float fraction = (i + .5f) / count;
                float angle = i * 2.399963f;
                var radial = new Vector3(Mathf.Cos(angle), .28f + fraction * .85f, Mathf.Sin(angle));
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = at + radial * size * .16f,
                    velocity = radial * (speed * (.55f + fraction * .65f)),
                    startColor = color,
                    startSize = size * (.67f + fraction * .75f),
                    startLifetime = lifetime * (.63f + fraction * .47f),
                    rotation = i * 79f
                }, 1);
            }
        }

        public void Tick(float dt, float speed, Vector3 hand)
        {
            for (int i = 0; i < traces.Length; i++)
            {
                ref var slot = ref traces[i];
                if (slot.Line == null || !slot.Line.enabled) continue;
                slot.Age += dt;
                UpdateTrace(ref slot);
            }

            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                var blast = blasts[i];
                blast.Age += dt;
                float progress = blast.Age / .46f;
                if (progress >= 1f)
                {
                    Destroy(blast.Line.gameObject);
                    blasts.RemoveAt(i);
                    continue;
                }
                blast.Line.transform.position += Vector3.back * (speed * dt);
                blast.Line.transform.localScale = Vector3.one * Mathf.Lerp(.18f, blast.Radius, 1f - (1f - progress) * (1f - progress));
                blast.Line.widthMultiplier = Mathf.Lerp(.13f, .035f, progress);
                blast.Line.startColor = blast.Line.endColor = new Color(1f, .64f, .19f, (1f - progress) * .86f);
            }

            if (medkitLeft > 0f)
            {
                medkitLeft -= dt;
                medkit.transform.position = hand;
                if (medkitLeft <= 0f) medkit.SetActive(false);
            }
        }

        public void SetPaused(bool paused)
        {
            if (particles == null) return;
            if (paused) particles.Pause();
            else if (particles.isPaused) particles.Play();
        }

        public void Clear()
        {
            foreach (var trace in traces) if (trace.Line != null) trace.Line.enabled = false;
            foreach (var blast in blasts) if (blast.Line != null) Destroy(blast.Line.gameObject);
            blasts.Clear();
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (medkit != null) medkit.SetActive(false);
            medkitLeft = 0f;
        }

        static Vector3[] BuildCircle()
        {
            var points = new Vector3[36];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * (2f * Mathf.PI / points.Length);
                points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            }
            return points;
        }

        void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
