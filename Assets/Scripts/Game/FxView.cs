using System;
using System.Collections.Generic;
using DeepDrill.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeepDrill.Game
{
    /// <summary>Debris, ore pickups that fly to the top bar, and floating numbers.</summary>
    public sealed class FxView : MonoBehaviour
    {
        private const int DebrisCount = 160;
        private const float DebrisLife = 0.45f;
        private const float DebrisGravity = 26f;
        private const float FlySeconds = 0.5f;
        private const float FloatSeconds = 0.65f;
        private const float FloatRise = 18f;
        private const int MaxFlyers = 40;
        private const int MaxFloaters = 24;
        private const int BlastCount = 12;
        private const float BlastSeconds = 0.3f;

        private sealed class Bit
        {
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Life;
        }

        private sealed class Blast
        {
            public SpriteRenderer Sprite;
            public float Time;
        }

        private sealed class Flyer
        {
            public Image Icon;
            public Vector2 From;
            public Vector2 To;
            public float Time;
            public Action Landed;
        }

        private sealed class Floater
        {
            public Text Label;
            public Vector2 From;
            public float Time;
        }

        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Blast> blasts = new List<Blast>();
        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly List<Flyer> flyerPool = new List<Flyer>();
        private readonly List<Floater> floaters = new List<Floater>();
        private readonly List<Floater> floaterPool = new List<Floater>();

        private int nextBit;
        private RectTransform layer;
        private Font font;
        private Sprite whitePixel;
        private Sprite[] blastFrames;

        public void Init(ArtLibrary art, RectTransform fxLayer, Font uiFont)
        {
            layer = fxLayer;
            font = uiFont;
            whitePixel = art.Get("pixel");
            blastFrames = art.Strip("explosion", 32);
            for (int i = 0; i < BlastCount; i++)
            {
                var go = new GameObject("blast");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 7;
                sr.enabled = false;
                blasts.Add(new Blast { Sprite = sr });
            }
            for (int i = 0; i < DebrisCount; i++)
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

        /// <summary>Plays the explosion sized to cover a blast of the given tile radius.</summary>
        public void Explosion(Vector2 world, int radius)
        {
            foreach (var blast in blasts)
            {
                if (blast.Sprite.enabled) continue;
                blast.Time = 0;
                blast.Sprite.enabled = true;
                blast.Sprite.sprite = blastFrames[0];
                blast.Sprite.transform.position = world;
                blast.Sprite.transform.localScale = Vector3.one * (radius * 2 + 1) * 0.5f;
                return;
            }
        }

        public void Debris(Vector2 world, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var bit = bits[nextBit];
                nextBit = (nextBit + 1) % bits.Count;
                bit.Sprite.enabled = true;
                bit.Sprite.color = color;
                bit.Sprite.transform.position = world + UnityEngine.Random.insideUnitCircle * 0.4f;
                bit.Sprite.transform.localScale = Vector3.one * (UnityEngine.Random.value < 0.4f ? 2f : 1f);
                bit.Velocity = new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(2f, 9f));
                bit.Life = DebrisLife * UnityEngine.Random.Range(0.6f, 1f);
            }
        }

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
            f.To = stageTo;
            f.Time = 0;
            f.Landed = landed;
            flyers.Add(f);
            return true;
        }

        public void FloatText(string text, Vector2 stage, Color color, int size)
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
            floaters.Add(f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            foreach (var bit in bits)
            {
                if (!bit.Sprite.enabled) continue;
                bit.Life -= dt;
                if (bit.Life <= 0)
                {
                    bit.Sprite.enabled = false;
                    continue;
                }
                bit.Velocity.y -= DebrisGravity * dt;
                bit.Sprite.transform.position += (Vector3)(bit.Velocity * dt);
            }

            foreach (var blast in blasts)
            {
                if (!blast.Sprite.enabled) continue;
                blast.Time += dt / BlastSeconds;
                if (blast.Time >= 1)
                {
                    blast.Sprite.enabled = false;
                    continue;
                }
                blast.Sprite.sprite = blastFrames[(int)(blast.Time * blastFrames.Length)];
            }

            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.Time += dt / FlySeconds;
                float t = f.Time * f.Time; // starts slow, snaps into the bar
                f.Icon.rectTransform.anchoredPosition = StageAnchored(Vector2.Lerp(f.From, f.To, t) - new Vector2(4, 4));
                if (f.Time < 1) continue;

                f.Landed?.Invoke();
                f.Icon.gameObject.SetActive(false);
                flyers.RemoveAt(i);
                flyerPool.Add(f);
            }

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Time += dt / FloatSeconds;
                var pos = f.From + new Vector2(0, -FloatRise * (1 - (1 - f.Time) * (1 - f.Time)));
                f.Label.rectTransform.anchoredPosition = StageAnchored(pos - new Vector2(40, 7));
                var c = f.Label.color;
                c.a = Mathf.Clamp01((1 - f.Time) * 3f);
                f.Label.color = c;
                if (f.Time < 1) continue;

                f.Label.gameObject.SetActive(false);
                floaters.RemoveAt(i);
                floaterPool.Add(f);
            }
        }

        private static Vector2 StageAnchored(Vector2 stage) => new Vector2(stage.x, -stage.y);
    }
}
