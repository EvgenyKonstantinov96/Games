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
        readonly SpitSlot[] spits = new SpitSlot[6];
        const float SpitSeconds = .48f;
        struct SpitSlot
        {
            public LineRenderer Line, Mark;
            public Vector3 From, Landing, Head;
            public float Age, Scroll;
            public int LaunchFrame;
        }
        int nextTrace;
        Material lineMaterial, particleMaterial;
        ParticleSystem particles;
        GameObject medkit;
        float medkitLeft;
        readonly LineRenderer[] shieldEdges = new LineRenderer[2];
        LineRenderer bossWarning;
        float shieldFlash;

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
            var flow = particles.velocityOverLifetime;
            flow.enabled = true;
            flow.space = ParticleSystemSimulationSpace.World;
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

        public void CoinPickup(Vector3 at)
            => Burst(at, new Color(1f, .77f, .30f, .78f), 3, .035f, .72f, .21f);

        public void BonusPickup(Vector3 feet, Color tint)
        {
            tint.a = .72f;
            for (int side = -1; side <= 1; side += 2)
                Burst(feet + new Vector3(side * .52f, .12f, .32f), tint, 5, .048f, 1.25f, .44f);
        }

        public void Footstep(Vector3 ground)
            => Burst(ground, new Color(.43f, .37f, .32f, .26f), 2, .074f, .54f, .26f);

        public void ShieldImpact(Vector3 feet)
        {
            shieldFlash = .22f;
            BonusPickup(feet, new Color(.36f, .64f, 1f));
        }

        public void UpdateShield(float dt, float seconds, Vector3 feet)
        {
            shieldFlash = Mathf.Max(0f, shieldFlash - dt);
            for (int i = 0; i < shieldEdges.Length; i++)
            {
                if (seconds <= 0f)
                {
                    if (shieldEdges[i] != null) shieldEdges[i].enabled = false;
                    continue;
                }
                if (lineMaterial == null) continue;
                if (shieldEdges[i] == null) shieldEdges[i] = MakeLine("Shield flank arc", 6);
                var line = shieldEdges[i];
                line.enabled = true;
                float side = i == 0 ? -1f : 1f;
                for (int j = 0; j < line.positionCount; j++)
                {
                    float t = j / (line.positionCount - 1f);
                    line.SetPosition(j, feet + new Vector3(side * (.65f + .23f * Mathf.Sin(t * Mathf.PI)),
                        .08f + t * .92f, .15f + t * .73f));
                }
                float fade = Mathf.Clamp01(seconds * 1.7f);
                line.startWidth = .038f + shieldFlash * .12f;
                line.endWidth = .012f;
                line.startColor = new Color(.30f, .68f, 1f, fade * (.65f + shieldFlash));
                line.endColor = new Color(.47f, .77f, 1f, .08f * fade);
            }
        }

        public void WarnBossStrike(float center, float depth, float halfWidth, float charge)
        {
            if (lineMaterial == null) return;
            if (bossWarning == null)
            {
                bossWarning = MakeLine("Boss strike ground boundary", 4);
                bossWarning.loop = true;
            }
            bossWarning.enabled = true;
            bossWarning.SetPosition(0, new Vector3(center - halfWidth, .026f, -.6f));
            bossWarning.SetPosition(1, new Vector3(center - halfWidth, .026f, depth - .45f));
            bossWarning.SetPosition(2, new Vector3(center + halfWidth, .026f, depth - .45f));
            bossWarning.SetPosition(3, new Vector3(center + halfWidth, .026f, -.6f));
            bossWarning.widthMultiplier = Mathf.Lerp(.032f, .076f, charge);
            bossWarning.startColor = bossWarning.endColor = new Color(1f, .27f, .09f, Mathf.Lerp(.45f, .92f, charge));
        }

        public void ClearBossWarning()
        {
            if (bossWarning != null) bossWarning.enabled = false;
        }

        public void BossStrike(Vector3 at)
        {
            ClearBossWarning();
            Burst(at, new Color(1f, .46f, .18f, .8f), 14, .080f, 4.6f, .55f);
            Burst(at, new Color(.26f, .20f, .18f, .52f), 10, .48f, 2.3f, .70f);
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
            Burst(body + new Vector3(-.39f, -.18f, .16f), new Color(.39f, .91f, .68f, .50f), 7, .055f, 1.25f, .61f);
            Burst(body + new Vector3(.38f, -.27f, .12f), new Color(.64f, 1f, .78f, .42f), 6, .047f, 1.16f, .51f);
        }

        public LineRenderer GrenadeTrail(Transform grenade)
        {
            if (lineMaterial == null) return null;
            var trail = grenade.gameObject.AddComponent<LineRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.positionCount = 6;
            trail.useWorldSpace = true;
            trail.startWidth = .011f;
            trail.endWidth = .085f;
            trail.startColor = new Color(.76f, .24f, .07f, 0f);
            trail.endColor = new Color(1f, .73f, .32f, .83f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.enabled = false;
            return trail;
        }

        public void LaunchSpit(Vector3 from, float lane, float speed)
        {
            if (lineMaterial == null) return;
            for (int i = 0; i < spits.Length; i++)
            {
                ref var slot = ref spits[i];
                if (slot.Line != null && slot.Line.enabled) continue;
                if (slot.Line == null)
                {
                    slot.Line = MakeLine("Toxic spit", 2);
                    slot.Line.startWidth = .026f;
                    slot.Line.endWidth = .19f;
                    slot.Line.startColor = new Color(.49f, .60f, .16f, .12f);
                    slot.Line.endColor = new Color(.77f, .96f, .21f, .92f);
                    slot.Line.numCapVertices = 3;
                }
                if (slot.Mark == null)
                {
                    slot.Mark = MakeLine("Toxic ground footprint",4);
                    slot.Mark.loop = true;
                    slot.Mark.widthMultiplier = .038f;
                    slot.Mark.startColor = slot.Mark.endColor = new Color(.70f,.91f,.19f,.65f);
                }
                slot.From = slot.Head = from;
                slot.Landing = new Vector3(lane, .16f, SpitSeconds * speed);
                slot.Age = slot.Scroll = 0f;
                SetSpitMark(slot);
                slot.Mark.enabled = true;
                slot.LaunchFrame = Time.frameCount;
                slot.Line.SetPosition(0, from);
                slot.Line.SetPosition(1, from);
                slot.Line.enabled = true;
                return;
            }
        }

        static void SetSpitMark(SpitSlot shot)
        {
            Vector3 ground = shot.Landing + Vector3.back*shot.Scroll;
            ground.y = .031f;
            shot.Mark.SetPosition(0,ground+new Vector3(-.83f,0f,-.95f));
            shot.Mark.SetPosition(1,ground+new Vector3(-.83f,0f,.95f));
            shot.Mark.SetPosition(2,ground+new Vector3(.83f,0f,.95f));
            shot.Mark.SetPosition(3,ground+new Vector3(.83f,0f,-.95f));
        }

        public void TickSpits(float dt, float speed)
        {
            for (int i = 0; i < spits.Length; i++)
            {
                ref var shot = ref spits[i];
                if (shot.Line == null || !shot.Line.enabled || shot.LaunchFrame == Time.frameCount) continue;
                float step = Mathf.Min(dt, SpitSeconds - shot.Age);
                shot.Age += step;
                shot.Scroll += speed * step;
                float t = shot.Age / SpitSeconds;
                SetSpitMark(shot);
                Vector3 previous = shot.Head;
                shot.Head = Vector3.Lerp(shot.From, shot.Landing, t) + Vector3.back * shot.Scroll;
                shot.Head.y += 2.8f * t * (1f - t);
                Vector3 movement = shot.Head - previous;
                shot.Line.SetPosition(0, shot.Head - Vector3.ClampMagnitude(movement * (.055f / Mathf.Max(.001f, step)), .95f));
                shot.Line.SetPosition(1, shot.Head);
                if (shot.Age < SpitSeconds) continue;

                shot.Line.enabled = false;
                shot.Mark.enabled = false;
                ToxicBurst(shot.Head);
                var game = ZSRGame.Instance;
                Vector3 offset = shot.Head - game.Hero.transform.position;
                if (Mathf.Abs(offset.x) <= .83f && Mathf.Abs(offset.z) <= .95f) game.HurtPlayer(8, ZSRDamageKind.Acid, shot.Head.x);
            }
        }

        public void PlayerHit(Vector3 feet, float sourceX, bool acid)
        {
            Color color = acid ? new Color(.67f, .91f, .21f, .8f) : new Color(.88f, .18f, .10f, .76f);
            float side = sourceX < feet.x ? -1f : 1f;
            Vector3 shoulder = feet + new Vector3(side * .3f, 1.4f, .16f);
            Burst(shoulder, color, acid ? 10 : 5, .06f, 1.5f, .33f);
            if (!acid) Streak(shoulder + new Vector3(side * .25f, .12f, .12f),
                shoulder + new Vector3(-side * .16f, -.14f, -.04f), color, .045f);
        }

        public void ToxicBurst(Vector3 at)
        {
            Burst(at, new Color(.60f, .81f, .20f, .70f), 8, .068f, 2.6f, .42f);
            Burst(at, new Color(.42f, .53f, .16f, .32f), 5, .27f, 1.2f, .56f);
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
            if (particles != null)
            {
                // Give dust, fragments and impact smoke the same road-relative motion as their source.
                var flow = particles.velocityOverLifetime;
                flow.z = -speed;
            }
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
            shieldFlash = 0f;
            UpdateShield(0f, 0f, Vector3.zero);
            ClearBossWarning();
            foreach (var trace in traces) if (trace.Line != null) trace.Line.enabled = false;
            foreach (var shot in spits)
            {
                if (shot.Line != null) shot.Line.enabled = false;
                if (shot.Mark != null) shot.Mark.enabled = false;
            }
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
