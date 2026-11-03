using System;
using System.Collections.Generic;

namespace DeepDrill.Game
{
    public enum Wave { Square, Triangle, Sine, Noise }

    /// <summary>
    /// One voice of a sound effect: a waveform that slides in pitch, steps through an arpeggio,
    /// and fades on its own envelope. Noise voices use the pitch as how often the noise changes,
    /// and the low-pass to darken it.
    /// </summary>
    public sealed class SfxVoice
    {
        public Wave Wave = Wave.Square;
        public double Start;            // seconds into the effect
        public double Seconds = 0.1;
        public double FromHz = 440, ToHz = 440; // slides exponentially, so octaves feel even
        public double Duty = 0.5;
        public double Attack = 0.002;
        public double Curve = 2;        // how fast the tail falls: 1 linear, higher drops sooner
        public double Volume = 0.5;
        public double VibratoHz, VibratoDepth; // depth as a share of the pitch
        public int[] Arp;               // semitone steps, played in turn
        public double ArpSeconds = 0.05;
        public double LowPassFrom = 1, LowPassTo = 1; // 1 open, towards 0 darker
        public double TremoloHz;        // amplitude flutter, for rumble and sizzle
    }

    /// <summary>
    /// Renders the game's sound effects from voices, in the manner of the old sound chips: square,
    /// triangle and sine tones with noise for impacts. Engine-free, so tests and previews can run
    /// it outside Unity. Every effect is a short list of voices, summed and gently limited.
    /// </summary>
    public static class SfxSynth
    {
        public const int SampleRate = 22050;

        public static float[] Render(IReadOnlyList<SfxVoice> voices, double peak = 0.8, int seed = 1)
        {
            double length = 0;
            foreach (var v in voices) length = Math.Max(length, v.Start + v.Seconds);
            var mix = new double[(int)Math.Ceiling(length * SampleRate) + 1];
            var rng = new Random(seed);
            foreach (var v in voices) AddVoice(mix, v, rng);

            double max = 1e-6;
            for (int i = 0; i < mix.Length; i++) max = Math.Max(max, Math.Abs(mix[i]));
            double gain = max > peak ? peak / max : 1;
            var output = new float[mix.Length];
            for (int i = 0; i < mix.Length; i++) output[i] = (float)Math.Tanh(mix[i] * gain * 1.1);
            // a few samples of fade at both ends so nothing clicks
            int fade = Math.Min(32, output.Length / 4);
            for (int i = 0; i < fade; i++) output[output.Length - 1 - i] *= i / (float)fade;
            return output;
        }

        private static void AddVoice(double[] mix, SfxVoice v, Random rng)
        {
            int start = (int)(v.Start * SampleRate);
            int count = (int)(v.Seconds * SampleRate);
            double phase = 0, held = 0, low = 0, nextChange = 0;
            for (int i = 0; i < count && start + i < mix.Length; i++)
            {
                double t = i / (double)SampleRate;
                double u = i / (double)count;
                double hz = v.FromHz * Math.Pow(v.ToHz / v.FromHz, u);
                if (v.Arp != null && v.Arp.Length > 0)
                    hz *= Math.Pow(2, v.Arp[Math.Min(v.Arp.Length - 1, (int)(t / v.ArpSeconds))] / 12.0);
                if (v.VibratoHz > 0) hz *= 1 + Math.Sin(t * 2 * Math.PI * v.VibratoHz) * v.VibratoDepth;
                phase += hz / SampleRate;

                double s;
                switch (v.Wave)
                {
                    case Wave.Square: s = phase % 1 < v.Duty ? 1 : -1; break;
                    case Wave.Triangle: { double p = phase % 1; s = p < 0.5 ? p * 4 - 1 : 3 - p * 4; break; }
                    case Wave.Sine: s = Math.Sin(phase * 2 * Math.PI); break;
                    default:
                        if (phase >= nextChange)
                        {
                            held = rng.NextDouble() * 2 - 1;
                            nextChange = phase + 1;
                        }
                        s = held;
                        break;
                }

                double cut = v.LowPassFrom + (v.LowPassTo - v.LowPassFrom) * u;
                if (cut < 1)
                {
                    low += (s - low) * Math.Max(0.005, cut * cut);
                    s = low;
                }

                double env = t < v.Attack ? t / v.Attack : Math.Pow(1 - u, v.Curve);
                if (v.TremoloHz > 0) env *= 0.6 + 0.4 * Math.Sin(t * 2 * Math.PI * v.TremoloHz);
                mix[start + i] += s * env * v.Volume;
            }
        }

        // ------------------------------------------------------------------------------------
        // The effects. Notes are tuned to C major so they sit with the music.
        // ------------------------------------------------------------------------------------
        private const double C4 = 261.63, E4 = 329.63, G4 = 392.0, A4 = 440.0;
        private const double C5 = 523.25, E5 = 659.26, G5 = 783.99, B5 = 987.77;
        private const double C6 = 1046.5, E6 = 1318.5, G6 = 1568.0, C7 = 2093.0;

        private static SfxVoice Tone(Wave w, double start, double seconds, double hz, double volume, double curve = 2) =>
            new SfxVoice { Wave = w, Start = start, Seconds = seconds, FromHz = hz, ToHz = hz, Volume = volume, Curve = curve };

        private static SfxVoice Slide(Wave w, double start, double seconds, double from, double to, double volume, double curve = 2) =>
            new SfxVoice { Wave = w, Start = start, Seconds = seconds, FromHz = from, ToHz = to, Volume = volume, Curve = curve };

        private static SfxVoice Noise(double start, double seconds, double hz, double lowPass, double volume, double curve = 2) =>
            new SfxVoice { Wave = Wave.Noise, Start = start, Seconds = seconds, FromHz = hz, ToHz = hz, LowPassFrom = lowPass, LowPassTo = lowPass, Volume = volume, Curve = curve };

        /// <summary>Every effect by name, as lists of voices. Names ending in a number are per layer.</summary>
        public static Dictionary<string, SfxVoice[]> Library()
        {
            var lib = new Dictionary<string, SfxVoice[]>();

            // ---- interface: short and soft, they are heard hundreds of times
            lib["click"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.035, FromHz = 1760, ToHz = 1320, Volume = 0.25, Curve = 3 },
                Noise(0, 0.012, 9000, 0.9, 0.08),
            };
            lib["tab"] = new[] { Slide(Wave.Triangle, 0, 0.07, G5, C6, 0.5, 1.5) };

            // ---- digging: one break sound per layer, so each layer's ground sounds like itself
            lib["break_0"] = new[] { Noise(0, 0.08, 3000, 0.25, 0.6), Slide(Wave.Triangle, 0, 0.07, 150, 70, 0.6) };                  // soil
            lib["break_1"] = new[] { Noise(0, 0.16, 6000, 0.55, 0.35, 1.2), Slide(Wave.Triangle, 0, 0.05, 120, 70, 0.4) };            // sand trickle
            lib["break_2"] = new[] { Noise(0, 0.02, 9000, 0.9, 0.3), Tone(Wave.Sine, 0, 0.28, C7, 0.22, 3), Tone(Wave.Sine, 0.015, 0.22, G6 * 1.5, 0.12, 3) }; // crystal chime
            lib["break_3"] = new[] { Slide(Wave.Sine, 0, 0.07, 380, 950, 0.5, 1.5), Noise(0.02, 0.12, 5000, 0.35, 0.3) };           // water plop
            lib["break_4"] = new[] { Slide(Wave.Square, 0, 0.05, 330, 220, 0.25, 3), Noise(0, 0.06, 4000, 0.5, 0.5) };               // ruin stone clack
            lib["break_5"] = new[] { Tone(Wave.Square, 0, 0.12, 880, 0.18, 3), Tone(Wave.Square, 0, 0.1, 1342, 0.12, 3), Noise(0, 0.05, 8000, 0.7, 0.35) }; // clank
            lib["break_6"] = new[] { Noise(0, 0.12, 8000, 0.85, 0.3, 1.5), Slide(Wave.Square, 0, 0.06, 600, 380, 0.15, 3) };          // pressure hiss
            lib["break_7"] = new[] { new SfxVoice { Wave = Wave.Noise, Seconds = 0.22, FromHz = 7000, ToHz = 7000, LowPassFrom = 0.8, LowPassTo = 0.6, Volume = 0.3, TremoloHz = 40 }, Slide(Wave.Triangle, 0, 0.08, 110, 55, 0.5) }; // magma sizzle
            lib["break_8"] = new[] { new SfxVoice { Wave = Wave.Triangle, Seconds = 0.16, FromHz = C6, ToHz = C6, Arp = new[] { 0, 7, 12 }, ArpSeconds = 0.035, Volume = 0.35, Curve = 1.5 }, Noise(0, 0.03, 9000, 0.8, 0.25) }; // phase glass
            lib["break_9"] = new[] { Slide(Wave.Triangle, 0, 0.16, 95, 45, 0.6, 1.5), Noise(0, 0.14, 1500, 0.15, 0.5) };             // convection rumble
            lib["break_10"] = new[] { Slide(Wave.Sine, 0, 0.06, 300, 700, 0.45, 1.5), Slide(Wave.Sine, 0.07, 0.05, 420, 900, 0.3, 1.5) }; // liquid bubbles
            lib["break_11"] = new[] { new SfxVoice { Wave = Wave.Noise, Seconds = 0.18, FromHz = 9000, ToHz = 9000, LowPassFrom = 0.95, LowPassTo = 0.7, Volume = 0.3, TremoloHz = 55 }, Noise(0, 0.03, 5000, 0.6, 0.4), Slide(Wave.Square, 0, 0.04, 1200, 900, 0.1) }; // white heat crackle
            lib["hard"] = new[] { Tone(Wave.Square, 0, 0.1, 740, 0.15, 3), Tone(Wave.Square, 0, 0.08, 1110, 0.1, 3), Noise(0, 0.04, 6000, 0.6, 0.3) };
            lib["coin"] = new[] { Tone(Wave.Square, 0, 0.05, B5, 0.22, 0.5), Tone(Wave.Square, 0.05, 0.12, E6, 0.22, 1.5) };
            lib["chest"] = new[] { new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.32, FromHz = C6, ToHz = C6, Arp = new[] { 0, 4, 7, 12, 16 }, ArpSeconds = 0.05, Volume = 0.22, Curve = 1 }, Noise(0, 0.05, 3000, 0.4, 0.4) };
            lib["land"] = new[] { Slide(Wave.Sine, 0, 0.14, 90, 38, 0.7, 1.5), Noise(0, 0.08, 2000, 0.2, 0.4) };

            // ---- explosions and layer events
            lib["blast"] = new[]
            {
                new SfxVoice { Wave = Wave.Noise, Seconds = 0.6, FromHz = 6000, ToHz = 1500, LowPassFrom = 0.7, LowPassTo = 0.08, Volume = 0.8, Curve = 1.6 },
                Slide(Wave.Sine, 0, 0.45, 90, 30, 0.8, 1.4),
            };
            lib["storm"] = new[]
            {
                Noise(0, 0.07, 11000, 0.95, 0.7, 1),
                new SfxVoice { Wave = Wave.Noise, Start = 0.05, Seconds = 1.3, FromHz = 900, ToHz = 300, LowPassFrom = 0.18, LowPassTo = 0.06, Volume = 0.9, Curve = 1.3, TremoloHz = 7 },
                Slide(Wave.Sine, 0.03, 0.6, 70, 35, 0.4, 1.2),
            };
            lib["overheat"] = new[]
            {
                new SfxVoice { Wave = Wave.Noise, Seconds = 0.7, FromHz = 10000, ToHz = 10000, LowPassFrom = 0.95, LowPassTo = 0.5, Volume = 0.35, Curve = 1.2, Attack = 0.05 },
                new SfxVoice { Wave = Wave.Sine, Seconds = 0.35, FromHz = 1900, ToHz = 2100, Volume = 0.12, VibratoHz = 12, VibratoDepth = 0.02, Attack = 0.03 },
            };
            lib["frenzy"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.4, FromHz = 220, ToHz = 880, Volume = 0.25, Curve = 0.8 },
                new SfxVoice { Wave = Wave.Square, Duty = 0.125, Start = 0.3, Seconds = 0.3, FromHz = C6, ToHz = C6, Arp = new[] { 0, 4, 7, 12 }, ArpSeconds = 0.04, Volume = 0.2, Curve = 1.2 },
            };
            lib["layer"] = new[]
            {
                new SfxVoice { Wave = Wave.Noise, Seconds = 0.6, FromHz = 8000, ToHz = 8000, LowPassFrom = 0.1, LowPassTo = 0.5, Volume = 0.25, Curve = 1.5, Attack = 0.25 },
                Tone(Wave.Triangle, 0.2, 0.5, C5, 0.25, 1.5), Tone(Wave.Triangle, 0.2, 0.5, G5, 0.2, 1.5), Tone(Wave.Triangle, 0.28, 0.45, E6, 0.15, 1.5),
            };

            // ---- growth and rewards
            lib["merge"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.3, FromHz = C5, ToHz = C5, Arp = new[] { 0, 4, 7, 12 }, ArpSeconds = 0.05, Volume = 0.22, Curve = 1 },
                Tone(Wave.Triangle, 0.2, 0.3, C7, 0.12, 2),
            };
            lib["merge_all"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.5, FromHz = C5, ToHz = C5, Arp = new[] { 0, 4, 7, 12, 16, 19, 24 }, ArpSeconds = 0.045, Volume = 0.22, Curve = 0.8 },
                Tone(Wave.Triangle, 0.3, 0.4, C4, 0.3, 1.5), Tone(Wave.Triangle, 0.3, 0.4, G4, 0.2, 1.5),
                Noise(0.3, 0.3, 12000, 0.95, 0.06, 1.5),
            };
            lib["upgrade"] = new[] { Tone(Wave.Triangle, 0, 0.1, G5, 0.35), Tone(Wave.Triangle, 0.06, 0.25, C6, 0.35, 1.5), Tone(Wave.Sine, 0.06, 0.25, G6, 0.1, 2) };
            lib["chime"] = new[] { Tone(Wave.Sine, 0, 0.3, E6, 0.3, 2.5), Tone(Wave.Sine, 0.08, 0.35, C7, 0.22, 2.5) };
            lib["rare"] = new[]
            {
                new SfxVoice { Wave = Wave.Sine, Seconds = 0.45, FromHz = E6, ToHz = E6, Arp = new[] { 0, 5, 12, 17, 24 }, ArpSeconds = 0.06, Volume = 0.3, Curve = 1.2 },
                new SfxVoice { Wave = Wave.Sine, Start = 0.12, Seconds = 0.45, FromHz = E6, ToHz = E6, Arp = new[] { 0, 5, 12, 17, 24 }, ArpSeconds = 0.06, Volume = 0.1, Curve = 1.2 }, // echo
            };
            lib["achievement"] = new[]
            {
                Tone(Wave.Square, 0, 0.08, C6, 0.18, 1), Tone(Wave.Square, 0.08, 0.08, E6, 0.18, 1), Tone(Wave.Square, 0.16, 0.08, G6, 0.18, 1),
                new SfxVoice { Wave = Wave.Square, Start = 0.24, Seconds = 0.45, FromHz = C7, ToHz = C7, Volume = 0.18, Curve = 1.5, VibratoHz = 6, VibratoDepth = 0.01 },
                Tone(Wave.Triangle, 0, 0.2, C4, 0.3, 1), Tone(Wave.Triangle, 0.24, 0.45, C5, 0.3, 1.5),
                Noise(0.24, 0.35, 14000, 0.95, 0.05, 2),
            };
            lib["milestone"] = new[] { Tone(Wave.Square, 0, 0.07, G5, 0.15, 1), Tone(Wave.Square, 0.07, 0.25, C6, 0.15, 1.5), Tone(Wave.Triangle, 0.07, 0.25, E5, 0.25, 1.5) };
            lib["event"] = new[] { Tone(Wave.Sine, 0, 0.12, A4 * 2, 0.3, 2), Tone(Wave.Sine, 0.13, 0.2, A4 * 3, 0.3, 2) };
            lib["claim"] = new[] { new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.25, FromHz = G5, ToHz = G5, Arp = new[] { 0, 5, 9, 12 }, ArpSeconds = 0.05, Volume = 0.2, Curve = 1 } };
            lib["warp"] = new[]
            {
                new SfxVoice { Wave = Wave.Sine, Seconds = 1.0, FromHz = 150, ToHz = 2400, Volume = 0.35, Curve = 1, Attack = 0.1, VibratoHz = 9, VibratoDepth = 0.03 },
                new SfxVoice { Wave = Wave.Noise, Seconds = 1.0, FromHz = 8000, ToHz = 8000, LowPassFrom = 0.05, LowPassTo = 0.6, Volume = 0.3, Curve = 1.5, Attack = 0.4 },
            };
            lib["rebirth"] = new[]
            {
                Slide(Wave.Triangle, 0, 1.0, 55, 220, 0.5, 1), Slide(Wave.Triangle, 0, 1.0, 82.5, 330, 0.35, 1),
                new SfxVoice { Wave = Wave.Square, Duty = 0.125, Start = 0.7, Seconds = 0.6, FromHz = C5, ToHz = C5, Arp = new[] { 0, 7, 12, 19, 24 }, ArpSeconds = 0.07, Volume = 0.18, Curve = 1 },
            };

            // ---- guardians
            lib["boss_start"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.3, Seconds = 0.9, FromHz = 120, ToHz = 70, Volume = 0.3, Curve = 1.2, VibratoHz = 14, VibratoDepth = 0.08, Attack = 0.06 },
                new SfxVoice { Wave = Wave.Noise, Seconds = 0.9, FromHz = 2500, ToHz = 800, LowPassFrom = 0.3, LowPassTo = 0.15, Volume = 0.35, Curve = 1.2, Attack = 0.06 },
                Slide(Wave.Sine, 0, 0.25, 80, 40, 0.7, 2), Slide(Wave.Sine, 0.3, 0.25, 80, 40, 0.7, 2), Slide(Wave.Sine, 0.6, 0.35, 80, 35, 0.8, 2),
            };
            lib["boss_warn"] = new[] { new SfxVoice { Wave = Wave.Square, Duty = 0.5, Seconds = 0.5, FromHz = 880, ToHz = 880, Arp = new[] { 0, -5, 0, -5, 0, -5 }, ArpSeconds = 0.083, Volume = 0.16, Curve = 0.6 } };
            lib["boss_strike"] = new[]
            {
                Noise(0, 0.4, 3000, 0.4, 0.7, 1.5),
                Slide(Wave.Sine, 0, 0.35, 75, 32, 0.8, 1.4),
            };
            lib["boss_armor"] = new[]
            {
                Noise(0, 0.3, 12000, 0.9, 0.5, 1.6),
                Tone(Wave.Triangle, 0, 0.3, 2350, 0.18, 3), Tone(Wave.Triangle, 0.03, 0.25, 3170, 0.14, 3), Tone(Wave.Triangle, 0.07, 0.22, 1890, 0.14, 3),
            };
            lib["boss_defeat"] = new[]
            {
                new SfxVoice { Wave = Wave.Noise, Seconds = 0.7, FromHz = 6000, ToHz = 1200, LowPassFrom = 0.7, LowPassTo = 0.06, Volume = 0.8, Curve = 1.5 },
                Slide(Wave.Sine, 0, 0.5, 90, 28, 0.8, 1.3),
                Tone(Wave.Square, 0.45, 0.1, C5, 0.18, 0.5), Tone(Wave.Square, 0.55, 0.1, E5, 0.18, 0.5), Tone(Wave.Square, 0.65, 0.1, G5, 0.18, 0.5),
                new SfxVoice { Wave = Wave.Square, Start = 0.75, Seconds = 0.7, FromHz = C6, ToHz = C6, Volume = 0.18, Curve = 1.5, VibratoHz = 6, VibratoDepth = 0.012 },
                Tone(Wave.Triangle, 0.75, 0.7, C4, 0.35, 1.5), Tone(Wave.Triangle, 0.75, 0.7, E5, 0.15, 1.5), Tone(Wave.Triangle, 0.75, 0.7, G4, 0.2, 1.5),
            };
            lib["boss_fail"] = new[]
            {
                new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 0.7, FromHz = G4, ToHz = G4, Arp = new[] { 0, -1, -2, -5 }, ArpSeconds = 0.16, Volume = 0.2, Curve = 0.8, VibratoHz = 5, VibratoDepth = 0.015 },
                Tone(Wave.Triangle, 0, 0.7, C4 / 2, 0.3, 1),
            };
            return lib;
        }

        /// <summary>The break effect for a layer; planets reuse the twelve by index.</summary>
        public static string BreakFor(int layer) => "break_" + (Math.Max(0, layer) % 12);
    }
}
