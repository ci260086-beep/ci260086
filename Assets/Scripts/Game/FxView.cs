using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>
    /// Every short-lived effect, all pooled: debris with a bounce, sparks, dust, layered explosions,
    /// ore that arcs out then flies to the corner, and floating numbers.
    /// </summary>
    public sealed class FxView : MonoBehaviour
    {
        private const int BitCount = 260;
        private const float BitLife = 0.55f;
        private const float Gravity = 26f;
        private const float BounceLoss = 0.4f;
        private const float PopSeconds = 0.28f;
        private const float HoverSeconds = 0.1f;
        private const float FlySeconds = 0.32f;
        private const float PopHeight = 16f;
        private const float FloatSeconds = 0.7f;
        private const float FloatRise = 20f;
        private const float FloatPopSeconds = 0.12f;
        private const float FloatPopScale = 1.6f;
        private const int MaxFlyers = 40;
        private const int MaxFloaters = 24;
        private const int AnimCount = 40;
        private const float AmbientPerSecond = 16f;

        public enum Text3 { Normal, Crit, Ore, Boss }

        private sealed class Bit
        {
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Floor;
            public bool Bounced;
            public bool Streak;
            public bool Drift;
        }

        private sealed class Anim
        {
            public SpriteRenderer Sprite;
            public Sprite[] Frames;
            public float Delay;
            public float Time;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public bool Fade;
        }

        private sealed class Flyer
        {
            public Image Icon;
            public Vector2 From;
            public Vector2 Peak;
            public Vector2 To;
            public float Side;
            public float Time;
            public Action Landed;
        }

        private sealed class Floater
        {
            public Text Label;
            public Vector2 From;
            public float Time;
            public float Jitter;
        }

        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Anim> anims = new List<Anim>();
        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly List<Flyer> flyerPool = new List<Flyer>();
        private readonly List<Floater> floaters = new List<Floater>();
        private readonly List<Floater> floaterPool = new List<Floater>();

        private int nextBit;
        private float ambientClock;
        private RectTransform layer;
        private Font font;
        private Sprite whitePixel;
        private Sprite[] fireFrames, smokeFrames, ringFrames, flashFrames;

        /// <summary>Share of effects to play: lowered by the setting and by very large swarms.</summary>
        public float Density { get; set; } = 1f;

        public void Init(ArtLibrary art, RectTransform fxLayer, Font uiFont)
        {
            layer = fxLayer;
            font = uiFont;
            whitePixel = art.Get("pixel");
            fireFrames = art.Strip("explosion", 32);
            smokeFrames = art.Strip("fx_smoke", 16);
            ringFrames = art.Strip("fx_ring", 32);
            flashFrames = new[] { art.Get("fx_circle") };

            for (int i = 0; i < AnimCount; i++)
            {
                var go = new GameObject("anim");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 7;
                sr.enabled = false;
                anims.Add(new Anim { Sprite = sr });
            }
            for (int i = 0; i < BitCount; i++)
            {
                var go = new GameObject("bit");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = whitePixel;
                sr.sortingOrder = 8;
                sr.enabled = false;
                bits.Add(new Bit { Sprite = sr });
            }
        }

        public bool Allow() => UnityEngine.Random.value < Density;

        /// <summary>White, then fire, then spreading smoke, with a shock ring over the top.</summary>
        public void Explosion(Vector2 world, int radius)
        {
            float size = radius * 2 + 1;
            Play(flashFrames, world, size * 0.7f, size * 0.9f, 0.05f, 0f, false);
            Play(fireFrames, world, size * 0.5f, size * 0.55f, 0.26f, 0.03f, false);
            Play(smokeFrames, world, size * 0.5f, size * 1.1f, 0.6f, 0.2f, true);
            Play(ringFrames, world, size * 0.5f, size * 0.8f, 0.3f, 0.02f, true);
        }

        public void Puff(Vector2 world)
        {
            if (Allow()) Play(smokeFrames, world, 0.6f, 1.1f, 0.28f, 0f, true);
        }

        /// <summary>A burst of light at a spot; stands in for a merge or a pickup flash.</summary>
        public void Flash(Vector2 world, float size) => Play(flashFrames, world, size * 0.6f, size, 0.1f, 0f, true);

        private void Play(Sprite[] frames, Vector2 world, float from, float to, float seconds, float delay, bool fade)
        {
            foreach (var a in anims)
            {
                if (a.Sprite.enabled || a.Frames != null) continue;
                a.Frames = frames;
                a.Delay = delay;
                a.Time = 0;
                a.Duration = seconds;
                a.StartScale = from;
                a.EndScale = to;
                a.Fade = fade;
                a.Sprite.transform.position = world;
                return;
            }
        }

        /// <summary>Environment motes for the layer on screen: dust, sand, embers, drips or glints.</summary>
        public void Ambient(int layer, Vector2 camera, float dt)
        {
            ambientClock += dt * AmbientPerSecond * GameSettings.ParticleScale;
            while (ambientClock >= 1)
            {
                ambientClock -= 1;
                var bit = bits[nextBit];
                nextBit = (nextBit + 1) % bits.Count;
                Vector2 at = camera + new Vector2(UnityEngine.Random.Range(-10f, 4f), UnityEngine.Random.Range(-5.5f, 5.5f));
                bit.Sprite.enabled = true;
                bit.Sprite.color = Palette.Bright(layer, 0.55f);
                bit.Sprite.transform.position = at;
                bit.Sprite.transform.rotation = Quaternion.identity;
                bit.Sprite.transform.localScale = Vector3.one;
                bit.Velocity = AmbientVelocity(layer);
                bit.MaxLife = bit.Life = UnityEngine.Random.Range(1.5f, 3.5f);
                bit.Floor = float.MinValue;
                bit.Bounced = true;
                bit.Streak = false;
                bit.Drift = true;
            }
        }

        private static Vector2 AmbientVelocity(int layer)
        {
            float x = UnityEngine.Random.Range(-0.3f, 0.3f);
            switch (layer)
            {
                case 1: return new Vector2(x, -2.2f);                 // falling sand
                case 3: return new Vector2(0, -3.5f);                 // drips
                case 6: case 7: case 9: return new Vector2(x, 1.6f);  // rising embers
                case 10: return new Vector2(2f, x);                   // molten metal flowing past
                case 11: return new Vector2(x, 0.8f);                 // heat shimmer
                default: return new Vector2(x, UnityEngine.Random.Range(-0.3f, 0.3f)); // drifting dust and glints
            }
        }

        /// <summary>One bright frame of light on a gem.</summary>
        public void Twinkle(Vector2 world) => Play(flashFrames, world, 0.25f, 0.5f, 0.1f, 0f, true);

        public void Debris(Vector2 world, Color color, int count) => Spray(world, color, count, false);

        /// <summary>Yellow-white streaks for rock that fights back.</summary>
        public void Sparks(Vector2 world, int count) => Spray(world, new Color(1f, 0.92f, 0.55f), count, true);

        private void Spray(Vector2 world, Color color, int count, bool streak)
        {
            int amount = Mathf.CeilToInt(count * Density);
            for (int i = 0; i < amount; i++)
            {
                var bit = bits[nextBit];
                nextBit = (nextBit + 1) % bits.Count;
                bit.Sprite.enabled = true;
                bit.Sprite.color = color;
                bit.Sprite.transform.position = world + UnityEngine.Random.insideUnitCircle * 0.4f;
                bit.Sprite.transform.rotation = Quaternion.identity;
                float size = UnityEngine.Random.value < 0.4f ? 2f : 1f;
                bit.Sprite.transform.localScale = new Vector3(size, size, 1);
                float speed = streak ? 1.7f : 1f;
                bit.Velocity = new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(2f, 9f)) * speed;
                bit.MaxLife = BitLife * UnityEngine.Random.Range(0.6f, 1f);
                bit.Life = bit.MaxLife;
                bit.Floor = world.y - 0.5f;
                bit.Bounced = false;
                bit.Streak = streak;
                bit.Drift = false;
            }
        }

        /// <summary>Pops out of the block in an arc, hangs a moment, then shoots to the target.</summary>
        public bool FlyIcon(Sprite sprite, Vector2 stageFrom, Vector2 stageTo, Action landed)
        {
            if (flyers.Count >= MaxFlyers) return false;

            Flyer f;
            if (flyerPool.Count > 0)
            {
                f = flyerPool[flyerPool.Count - 1];
                flyerPool.RemoveAt(flyerPool.Count - 1);
                f.Icon.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("flyer", typeof(RectTransform));
                go.transform.SetParent(layer, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                Ui.TopLeft(image.rectTransform, 0, 0, 8, 8);
                f = new Flyer { Icon = image };
            }
            f.Icon.sprite = sprite;
            f.From = stageFrom;
            f.Side = UnityEngine.Random.Range(-10f, 10f);
            f.Peak = stageFrom + new Vector2(f.Side, -PopHeight);
            f.To = stageTo;
            f.Time = 0;
            f.Landed = landed;
            flyers.Add(f);
            return true;
        }

        public void FloatText(string text, Vector2 stage, Color color, int size, Text3 style = Text3.Normal)
        {
            if (floaters.Count >= MaxFloaters) return; // a big swarm would otherwise bury the screen in numbers

            Floater f;
            if (floaterPool.Count > 0)
            {
                f = floaterPool[floaterPool.Count - 1];
                floaterPool.RemoveAt(floaterPool.Count - 1);
                f.Label.gameObject.SetActive(true);
            }
            else
            {
                var label = Ui.Label(layer, font, "", 0, 0, 80, 14, 8, TextAnchor.MiddleCenter);
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.07f, 0.06f);
                f = new Floater { Label = label };
            }
            f.Label.text = text;
            f.Label.color = color;
            f.Label.fontSize = size;
            f.From = stage;
            f.Time = 0;
            f.Jitter = style == Text3.Crit ? 1.5f : 0f;
            floaters.Add(f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            StepBits(dt);
            StepAnims(dt);
            StepFlyers(dt);
            StepFloaters(dt);
        }

        private void StepBits(float dt)
        {
            foreach (var bit in bits)
            {
                if (!bit.Sprite.enabled) continue;
                bit.Life -= dt;
                if (bit.Life <= 0)
                {
                    bit.Sprite.enabled = false;
                    continue;
                }
                if (!bit.Drift) bit.Velocity.y -= Gravity * dt;
                var t = bit.Sprite.transform;
                t.position += (Vector3)(bit.Velocity * dt);
                if (!bit.Bounced && t.position.y < bit.Floor)
                {
                    bit.Bounced = true;
                    bit.Velocity = new Vector2(bit.Velocity.x * BounceLoss, -bit.Velocity.y * BounceLoss);
                }
                if (bit.Streak)
                {
                    t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(bit.Velocity.y, bit.Velocity.x) * Mathf.Rad2Deg);
                    t.localScale = new Vector3(4, 1, 1);
                }
            }
        }

        private void StepAnims(float dt)
        {
            foreach (var a in anims)
            {
                if (a.Frames == null) continue;
                if (a.Delay > 0)
                {
                    a.Delay -= dt;
                    continue;
                }
                a.Sprite.enabled = true;
                a.Time += dt / a.Duration;
                if (a.Time >= 1)
                {
                    a.Sprite.enabled = false;
                    a.Frames = null;
                    continue;
                }
                a.Sprite.sprite = a.Frames[(int)(a.Time * a.Frames.Length)];
                a.Sprite.transform.localScale = Vector3.one * Mathf.Lerp(a.StartScale, a.EndScale, a.Time);
                a.Sprite.color = new Color(1, 1, 1, a.Fade ? 1 - a.Time : 1);
            }
        }

        private void StepFlyers(float dt)
        {
            float total = PopSeconds + HoverSeconds + FlySeconds;
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.Time += dt;
                Vector2 at;
                if (f.Time < PopSeconds)
                {
                    float t = f.Time / PopSeconds;
                    at = Vector2.Lerp(f.From, f.Peak, 1 - (1 - t) * (1 - t));
                }
                else if (f.Time < PopSeconds + HoverSeconds)
                {
                    at = f.Peak;
                }
                else
                {
                    float t = (f.Time - PopSeconds - HoverSeconds) / FlySeconds;
                    at = Vector2.Lerp(f.Peak, f.To, t * t);
                    if (Density > 0.5f && UnityEngine.Random.value < 0.5f) TrailAt(at);
                }
                f.Icon.rectTransform.anchoredPosition = new Vector2(at.x - 4, -(at.y - 4));
                if (f.Time < total) continue;

                f.Landed?.Invoke();
                f.Icon.gameObject.SetActive(false);
                flyers.RemoveAt(i);
                flyerPool.Add(f);
            }
        }

        // the trail is drawn in UI space, so it shares the floating-number pool's layer
        private void TrailAt(Vector2 stage)
        {
            FloatText("·", stage, new Color(1f, 0.9f, 0.6f, 0.8f), 7);
            if (floaters.Count > 0) floaters[floaters.Count - 1].Time = 0.6f;
        }

        private void StepFloaters(float dt)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Time += dt / FloatSeconds;
                var pos = f.From + new Vector2(0, -FloatRise * (1 - (1 - f.Time) * (1 - f.Time)));
                if (f.Jitter > 0) pos += new Vector2(UnityEngine.Random.Range(-f.Jitter, f.Jitter), 0);
                f.Label.rectTransform.anchoredPosition = new Vector2(pos.x - 40, -(pos.y - 7));

                float seconds = f.Time * FloatSeconds;
                float scale = seconds < FloatPopSeconds
                    ? Mathf.Lerp(FloatPopScale, 1f, seconds / FloatPopSeconds)
                    : 1f;
                f.Label.rectTransform.localScale = Vector3.one * scale;

                var c = f.Label.color;
                c.a = Mathf.Clamp01((1 - f.Time) * 3f);
                f.Label.color = c;
                if (f.Time < 1) continue;

                f.Label.gameObject.SetActive(false);
                floaters.RemoveAt(i);
                floaterPool.Add(f);
            }
        }
    }
}
