using System;
using System.Collections.Generic;

namespace DeepDrill.Trailer
{
    /// <summary>
    /// The trailer's own track, harder than anything in the game: E minor at the timeline's tempo,
    /// four-on-the-floor kick, offbeat open hats, an octave-jumping bass and a doubled pulse lead,
    /// with everything but the drums ducking under the kick. Sections come from timeline.json:
    /// intro (a build and a snare roll), drop and systems (the full groove and the hook), climax
    /// (the hook an octave up with sixteenth hats) and logo (one big chord left ringing).
    /// </summary>
    public sealed class TrailerMusic
    {
        public const int Rate = 44100;
        private readonly double bpm;
        private readonly double[] mix;
        private readonly double[] tonal;   // ducked under the kick
        private readonly double[] duck;    // 1 at the kick, falling back to 0
        private readonly Random random = new Random(140);

        // chords one per bar, as roots: Em C G D
        private static readonly int[] ChordRoots = { 40, 36, 43, 38 };
        private static readonly int[][] ChordTones = { new[] { 0, 3, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 } };

        // the hook over four bars of sixteenths: a MIDI note, -1 holds, 0 rests
        private static readonly int[] Hook =
        {
            76, -1, 79, -1, 83, -1, -1, 81, 79, -1, 76, -1, 79, 81, 83, -1,
            84, -1, -1, 83, 81, -1, 79, -1, 76, -1, 79, -1, 81, -1, -1, -1,
            79, -1, 83, -1, 86, -1, -1, 83, 81, -1, 79, -1, 83, -1, 86, -1,
            88, -1, -1, 86, 83, -1, 81, -1, 78, -1, 81, -1, 83, -1, -1, 0,
        };

        public TrailerMusic(double bpm, double seconds)
        {
            this.bpm = bpm;
            int n = (int)(seconds * Rate);
            mix = new double[n];
            tonal = new double[n];
            duck = new double[n];
        }

        private double Beat => 60 / bpm;
        private double BarSeconds => 4 * Beat;
        private static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);

        /// <summary>Writes one section of `bars` bars starting at bar `start`.</summary>
        public void Section(string kind, int start, int bars)
        {
            for (int b = 0; b < bars; b++)
            {
                int bar = start + b;
                double t0 = bar * BarSeconds;
                bool last = b == bars - 1;
                switch (kind)
                {
                    case "intro":
                        // bar one: a filtered pulse and a rising sweep; bar two: kicks and a snare roll that tightens
                        Riser(t0, BarSeconds * bars, 0.35);
                        if (b == 0) for (int i = 0; i < 4; i++) Bass(t0 + i * Beat, ChordRoots[0], Beat * 0.9, 0.5, 0.25);
                        else
                        {
                            for (int i = 0; i < 4; i++) Kick(t0 + i * Beat, 0.7);
                            for (int i = 0; i < 16; i++)
                            {
                                double step = i < 8 ? 2 : 1;
                                if (i % (int)step != 0 && i < 8) continue;
                                Snare(t0 + i * Beat / 4, 0.25 + 0.5 * i / 16.0);
                            }
                        }
                        break;
                    case "logo":
                        if (b == 0) Logo(t0);
                        break;
                    default:
                        bool climax = kind == "climax";
                        if (b == 0) Crash(t0, 0.7);
                        else if (climax && bar % 2 == 0) Crash(t0, 0.45);
                        Groove(t0, bar, climax, kind == "drop");
                        // the last bar before the logo rolls into it
                        if (climax && last) for (int i = 8; i < 16; i++) Snare(t0 + i * Beat / 4, 0.4 + 0.05 * (i - 8));
                        break;
                }
            }
        }

        private void Groove(double t0, int bar, bool climax, bool drop)
        {
            int chord = bar % 4;
            int root = ChordRoots[chord];
            for (int i = 0; i < 4; i++)
            {
                Kick(t0 + i * Beat, 0.95);
                if (i % 2 == 1) Snare(t0 + i * Beat, 0.65);
                Hat(t0 + i * Beat + Beat / 2, 0.22, open: true);
            }
            int hats = climax ? 16 : 8;
            for (int i = 0; i < hats; i++) Hat(t0 + i * BarSeconds / hats, i % 2 == 0 ? 0.12 : 0.07, open: false);

            // bass: eighths, root and its octave
            for (int i = 0; i < 8; i++) Bass(t0 + i * Beat / 2, root + (i % 2 == 1 ? 12 : 0), Beat / 2 * 0.85, 0.42, 0.5);
            // a pad under it all
            foreach (int tone in ChordTones[chord]) Pad(t0, root + 24 + tone, BarSeconds, 0.05);
            // arpeggio sparkle
            if (!drop)
                for (int i = 0; i < 16; i++)
                {
                    int tone = ChordTones[chord][i % 3] + (i / 3 % 2) * 12;
                    Tone(t0 + i * Beat / 4, root + 36 + tone, Beat / 4 * 0.8, 0.045, 0.125, 18);
                }

            // the hook; in the drop only its first half bar, as a stab per bar
            int phrase = bar % 4;
            int steps = drop ? 8 : 16;
            for (int s = 0; s < steps; s++)
            {
                int note = Hook[phrase * 16 + s];
                if (note <= 0) continue;
                int len = 1;
                while (s + len < 16 && Hook[phrase * 16 + s + len] == -1) len++;
                double dur = len * Beat / 4 * 0.95;
                double at = t0 + s * Beat / 4;
                int shift = climax ? 12 : 0;
                Lead(at, note + shift, dur, climax ? 0.13 : 0.15);
                if (climax) Lead(at, note + shift - 12, dur, 0.08);
            }
        }

        private void Logo(double t0)
        {
            Crash(t0, 0.9);
            Kick(t0, 1.2);
            foreach (int note in new[] { 28, 40, 47, 52, 56, 59, 64 }) // E major, low to high
                Pad(t0, note, 4.5, note < 45 ? 0.16 : 0.07, swell: false);
            Lead(t0, 88, 2.5, 0.12);
            Lead(t0, 76, 2.5, 0.09);
        }

        // ----------------------------------------------------------------- voices

        private void Kick(double t, double vol)
        {
            int start = (int)(t * Rate);
            double phase = 0;
            for (int i = 0; i < Rate * 0.35 && start + i < mix.Length; i++)
            {
                double u = i / (double)Rate;
                double hz = 45 + 120 * Math.Exp(-u * 35);
                phase += hz / Rate;
                double env = Math.Exp(-u * 9);
                double click = u < 0.004 ? (random.NextDouble() * 2 - 1) * (1 - u / 0.004) * 0.4 : 0;
                mix[start + i] += (Math.Sin(phase * 2 * Math.PI) * env + click) * vol * 0.55;
                duck[start + i] = Math.Max(duck[start + i], Math.Exp(-u * 7));
            }
        }

        private void Snare(double t, double vol)
        {
            int start = (int)(t * Rate);
            double prev = 0, phase = 0;
            for (int i = 0; i < Rate * 0.22 && start + i < mix.Length; i++)
            {
                double u = i / (double)Rate;
                double white = random.NextDouble() * 2 - 1;
                double high = white - prev; // a crude high-pass for the rattle
                prev = white;
                phase += 185 / (double)Rate;
                double body = Math.Sin(phase * 2 * Math.PI) * Math.Exp(-u * 30);
                mix[start + i] += (high * 0.5 * Math.Exp(-u * 16) + body * 0.5) * vol * 0.5;
            }
        }

        private void Hat(double t, double vol, bool open)
        {
            int start = (int)(t * Rate);
            double prev = 0, decay = open ? 14 : 60;
            for (int i = 0; i < Rate * (open ? 0.2 : 0.05) && start + i < mix.Length; i++)
            {
                double u = i / (double)Rate;
                double white = random.NextDouble() * 2 - 1;
                double high = white - prev;
                prev = white;
                mix[start + i] += high * Math.Exp(-u * decay) * vol * 0.6;
            }
        }

        private void Crash(double t, double vol)
        {
            int start = (int)(t * Rate);
            double prev = 0;
            for (int i = 0; i < Rate * 1.8 && start + i < mix.Length; i++)
            {
                double u = i / (double)Rate;
                double white = random.NextDouble() * 2 - 1;
                double high = white - prev * 0.6;
                prev = white;
                mix[start + i] += high * Math.Exp(-u * 2.2) * vol * 0.35;
            }
        }

        private void Riser(double t, double seconds, double vol)
        {
            int start = (int)(t * Rate);
            double low = 0, phase = 0;
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n && start + i < mix.Length; i++)
            {
                double u = i / (double)n;
                double white = random.NextDouble() * 2 - 1;
                double cut = 0.01 + 0.5 * u * u;
                low += (white - low) * cut;
                phase += (200 + 1800 * u * u) / Rate;
                double sweep = Math.Sin(phase * 2 * Math.PI) * 0.25;
                mix[start + i] += (low * 1.5 + sweep) * u * u * vol;
            }
        }

        private void Bass(double t, int midi, double seconds, double vol, double duty)
        {
            int start = (int)(t * Rate);
            double phase = 0, low = 0, hz = Hz(midi);
            for (int i = 0; i < seconds * Rate && start + i < tonal.Length; i++)
            {
                double u = i / (seconds * Rate);
                phase += hz / Rate;
                double sq = phase % 1 < duty ? 1 : -1;
                low += (sq - low) * 0.18;
                double env = u < 0.9 ? 1 - u * 0.3 : (1 - u) / 0.1 * 0.7;
                tonal[start + i] += low * env * vol * 0.45;
            }
        }

        private void Lead(double t, int midi, double seconds, double vol)
        {
            int start = (int)(t * Rate);
            double a = 0, b = 0, hz = Hz(midi);
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n && start + i < tonal.Length; i++)
            {
                double u = i / (double)Rate;
                double vib = u > 0.12 ? Math.Sin(u * 2 * Math.PI * 5.5) * 0.006 : 0;
                a += hz * (1.0035 + vib) / Rate;
                b += hz * (0.9965 + vib) / Rate;
                double s = (a % 1 < 0.5 ? 1 : -1) * 0.6 + (b % 1 < 0.25 ? 1 : -1) * 0.4;
                double attack = Math.Min(1, u / 0.006);
                double release = i > n - Rate * 0.03 ? (n - i) / (Rate * 0.03) : 1;
                double env = attack * release * (0.8 + 0.2 * Math.Exp(-u * 6));
                tonal[start + i] += s * env * vol;
            }
        }

        private void Pad(double t, int midi, double seconds, double vol, bool swell = true)
        {
            int start = (int)(t * Rate);
            double hz = Hz(midi);
            double[] phases = new double[3];
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n && start + i < tonal.Length; i++)
            {
                double u = i / (double)n;
                double s = 0;
                for (int k = 0; k < 3; k++)
                {
                    phases[k] += hz * (1 + (k - 1) * 0.004) / Rate;
                    double p = phases[k] % 1;
                    s += p < 0.5 ? p * 4 - 1 : 3 - p * 4;
                }
                double env = swell ? Math.Min(1, u / 0.15) * (u > 0.85 ? (1 - u) / 0.15 : 1) : Math.Pow(1 - u, 1.5);
                tonal[start + i] += s / 3 * env * vol;
            }
        }

        private void Tone(double t, int midi, double seconds, double vol, double duty, double decay)
        {
            int start = (int)(t * Rate);
            double phase = 0, hz = Hz(midi);
            for (int i = 0; i < seconds * Rate && start + i < tonal.Length; i++)
            {
                double u = i / (double)Rate;
                phase += hz / Rate;
                tonal[start + i] += (phase % 1 < duty ? 1 : -1) * Math.Exp(-u * decay) * vol;
            }
        }

        /// <summary>The finished track: tonal parts ducked under the kick, drums on top.</summary>
        public double[] Mixdown()
        {
            var output = new double[mix.Length];
            for (int i = 0; i < mix.Length; i++) output[i] = mix[i] + tonal[i] * (1 - 0.55 * duck[i]);
            return output;
        }
    }
}
