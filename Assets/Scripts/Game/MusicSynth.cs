using System;

namespace DeepDrill.Game
{
    /// <summary>One loop of background music: key, mode, tempo and the voices' character.</summary>
    public sealed class Tune
    {
        public int Root;          // MIDI note of the tonic, around 60
        public bool Minor;
        public double Bpm;
        public int Seed;          // picks the melody and rhythm
        public double LeadDuty;   // pulse width of the lead, 0.125 to 0.5
        public double Brightness; // 0 dark to 1 bright: lead octave, hat level, arpeggio sparkle
        public bool Driving;      // boss fights: steadier kick, busier bass
        public double LeadSoftness; // 0 a bright pulse lead, 1 a round triangle lead
        public double Echo;         // a quiet repeat of the lead a dotted eighth later, for space
        public bool Calm;           // sparser melody rhythms, softer drums
    }

    /// <summary>
    /// Renders short, loopable chiptune tracks: a pulse-wave lead, a triangle bass, a pulse
    /// arpeggio and a noise drum kit, over a four-chord progression. Engine-free, so the same code
    /// can render previews outside Unity. Every tune is eight bars of 4/4 and loops seamlessly.
    /// </summary>
    public static class MusicSynth
    {
        public const int SampleRate = 22050;
        public const int Bars = 8;
        private const int StepsPerBar = 16;

        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] MinorScale = { 0, 2, 3, 5, 7, 8, 10 };

        // chord roots as scale degrees: I-V-vi-IV is the cheerful pop loop; i-VI-III-VII keeps a
        // minor key moving forward instead of brooding
        private static readonly int[] MajorProgression = { 0, 4, 5, 3 };
        private static readonly int[] MinorProgression = { 0, 5, 2, 6 };

        // where melody notes start inside a bar ('x' new note, '-' hold, '.' rest)
        private static readonly string[] Rhythms =
        {
            "x.x.x-x.x.x.x---",
            "x-x.x.x-x.x-x...",
            "x.xxx.x.x-x.xx..",
            "x--.x.x.x--.x.x.",
        };

        // fewer, longer notes: the melody breathes instead of chattering
        private static readonly string[] CalmRhythms =
        {
            "x---x-..x---x...",
            "x-----x.x-x-----",
            "x---..x-x---....",
            "x-x-x---x-----..",
        };

        public static float[] Render(Tune tune)
        {
            double stepSeconds = 60.0 / tune.Bpm / 4;
            int stepSamples = (int)Math.Round(stepSeconds * SampleRate);
            int steps = Bars * StepsPerBar;
            var mix = new float[steps * stepSamples];
            var leadOnly = new float[mix.Length];
            var rng = new Random(tune.Seed);
            int[] scale = tune.Minor ? MinorScale : MajorScale;
            int[] progression = tune.Minor ? MinorProgression : MajorProgression;

            var melody = ComposeMelody(rng, scale, progression, tune);
            double leadPhase = 0, bassPhase = 0, arpPhase = 0;
            uint noise = 0x1234567u;

            for (int step = 0; step < steps; step++)
            {
                int bar = step / StepsPerBar, inBar = step % StepsPerBar;
                int chord = progression[bar % progression.Length];
                int chordRoot = tune.Root + Degree(scale, chord);
                int third = tune.Root + Degree(scale, chord + 2), fifth = tune.Root + Degree(scale, chord + 4);

                var lead = melody[step];
                int bassNote = chordRoot - 24 + (inBar % 8 == 6 ? 7 : 0) + (tune.Driving && inBar % 2 == 1 ? 12 : 0);
                bool bassOn = tune.Driving ? true : inBar % 4 != 3;
                int[] arp = { chordRoot, third, fifth, chordRoot + 12 };
                int arpNote = arp[inBar % 4] + (tune.Brightness > 0.5 ? 12 : 0);

                bool kick = tune.Calm ? inBar == 0 || inBar == 10 : inBar % 4 == 0 || (tune.Driving && inBar % 4 == 2 && inBar != 2);
                bool snare = inBar == 4 || inBar == 12;
                bool hat = tune.Calm ? inBar % 4 == 2 : inBar % 2 == 1 || (tune.Brightness > 0.6 && inBar % 4 == 2);
                double drumLevel = tune.Calm ? 0.6 : 1;

                for (int s = 0; s < stepSamples; s++)
                {
                    double t = s / (double)SampleRate;
                    double stepT = s / (double)stepSamples;
                    float sample = 0;

                    // lead: pulse wave with a short vibrato once the note has settled
                    if (lead.Note > 0)
                    {
                        double age = (lead.Age + stepT) * stepSeconds;
                        double vib = age > 0.15 ? Math.Sin(age * 2 * Math.PI * 5.5) * 0.004 : 0;
                        leadPhase += Hz(lead.Note) * (1 + vib) / SampleRate;
                        double env = lead.Release && stepT > 0.6 ? (1 - stepT) / 0.4 : 1;
                        env *= 0.85 + 0.15 * Math.Exp(-age * 8);
                        double tone = Pulse(leadPhase, tune.LeadDuty) * (1 - tune.LeadSoftness) + Triangle(leadPhase) * tune.LeadSoftness * 1.6;
                        double leadOut = tone * (0.16 - 0.03 * tune.LeadSoftness) * env;
                        sample += (float)leadOut;
                        leadOnly[step * stepSamples + s] = (float)leadOut;
                    }

                    // bass: triangle, short gap at the end of each step so repeated notes still pluck
                    if (bassOn)
                    {
                        bassPhase += Hz(bassNote) / SampleRate;
                        double env = stepT < 0.85 ? 1 : (1 - stepT) / 0.15;
                        sample += (float)(Triangle(bassPhase) * 0.22 * env);
                    }

                    // arpeggio: quiet thin pulse with a quick decay, the sparkle on top
                    arpPhase += Hz(arpNote) / SampleRate;
                    sample += (float)(Pulse(arpPhase, 0.125) * 0.045 * (0.4 + 0.6 * tune.Brightness) * Math.Exp(-t * 18));

                    // drums
                    if (kick)
                    {
                        double kHz = 120 * Math.Exp(-t * 30) + 45;
                        sample += (float)(Math.Sin(2 * Math.PI * kHz * t) * 0.32 * drumLevel * Math.Exp(-t * 22));
                    }
                    if (snare || hat)
                    {
                        noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                        double white = (noise & 0xffff) / 32767.5 - 1;
                        if (snare) sample += (float)(white * 0.14 * drumLevel * Math.Exp(-t * (tune.Calm ? 30 : 20)));
                        if (hat) sample += (float)(white * 0.05 * drumLevel * (0.5 + 0.5 * tune.Brightness) * Math.Exp(-t * 90));
                    }

                    mix[step * stepSamples + s] = sample;
                }
            }

            // the echo wraps round the loop, so the seam stays seamless
            if (tune.Echo > 0)
            {
                int delay = stepSamples * 3;
                for (int i = 0; i < mix.Length; i++)
                    mix[i] += leadOnly[(i - delay + mix.Length) % mix.Length] * (float)tune.Echo;
            }
            // soft clip so stacked voices never crack
            for (int i = 0; i < mix.Length; i++) mix[i] = (float)Math.Tanh(mix[i] * 1.2);
            return mix;
        }

        private struct LeadStep
        {
            public int Note;     // MIDI, 0 for rest
            public int Age;      // steps since the note started
            public bool Release; // last step of the note
        }

        // A melody that answers itself: bars 1-4 state a phrase, 5-6 repeat it, 7-8 end on the
        // tonic so the loop lands home. Notes come from the chord under them, with passing steps.
        private static LeadStep[] ComposeMelody(Random rng, int[] scale, int[] progression, Tune tune)
        {
            int steps = Bars * StepsPerBar;
            var lead = new LeadStep[steps];
            int octave = tune.Brightness > 0.45 ? 12 : 0;
            var phrase = new int[4 * StepsPerBar];
            var pool = tune.Calm ? CalmRhythms : Rhythms;
            string[] rhythm = { pool[rng.Next(pool.Length)], pool[rng.Next(pool.Length)] };
            int degree = 4;

            for (int bar = 0; bar < 4; bar++)
            {
                string r = rhythm[bar % 2];
                int chord = progression[bar];
                for (int i = 0; i < StepsPerBar; i++)
                {
                    int at = bar * StepsPerBar + i;
                    if (r[i] == '.') { phrase[at] = -1; continue; }
                    if (r[i] == '-') { phrase[at] = -2; continue; }
                    // strong beats land on a chord tone, the rest wander a step up or down
                    if (i % 4 == 0)
                    {
                        int[] tones = { chord, chord + 2, chord + 4 };
                        int best = tones[0];
                        foreach (int c in tones)
                            for (int o = -7; o <= 7; o += 7)
                                if (Math.Abs(c + o - degree) < Math.Abs(best - degree)) best = c + o;
                        degree = best;
                    }
                    else degree += rng.Next(3) - 1 + (rng.NextDouble() < 0.25 ? (rng.Next(2) * 2 - 1) : 0);
                    degree = Math.Max(0, Math.Min(11, degree));
                    phrase[at] = degree;
                }
            }

            int[] full = new int[steps];
            for (int i = 0; i < steps; i++)
            {
                int bar = i / StepsPerBar;
                int source = bar < 4 ? i : bar < 6 ? i - 4 * StepsPerBar : i - 4 * StepsPerBar;
                full[i] = phrase[source];
            }
            // the last bar resolves: tonic on beat one, held, then a pickup back into the loop
            int last = 7 * StepsPerBar;
            for (int i = 0; i < StepsPerBar; i++) full[last + i] = i == 0 ? 7 : i < 8 ? -2 : i == 12 ? 4 : i == 14 ? 6 : i == 13 || i == 15 ? -2 : -1;

            int current = 0, age = 0;
            for (int i = 0; i < steps; i++)
            {
                if (full[i] >= 0) { current = tune.Root + octave + Degree(scale, full[i]); age = 0; }
                else if (full[i] == -1) current = 0;
                else age++;
                lead[i] = new LeadStep { Note = current, Age = age };
            }
            for (int i = 0; i < steps; i++)
                lead[i].Release = lead[i].Note > 0 && (lead[(i + 1) % steps].Note != lead[i].Note || lead[(i + 1) % steps].Age == 0);
            return lead;
        }

        private static int Degree(int[] scale, int degree)
        {
            int octave = Math.DivRem(degree, 7, out int index);
            if (index < 0) { index += 7; octave--; }
            return scale[index] + 12 * octave;
        }

        private static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

        private static double Pulse(double phase, double duty) => phase % 1 < duty ? 1 : -1;

        private static double Triangle(double phase)
        {
            double p = phase % 1;
            return p < 0.5 ? p * 4 - 1 : 3 - p * 4;
        }

        /// <summary>Which arrangement ForLayer uses: 0 the first bright one, 1 relaxed, 2 quiet.</summary>
        public static int Style = 2; // the user chose the quiet arrangement

        /// <summary>The tune for a layer (zero based) or its guardian fight. Deeper layers sit lower and get more minor.</summary>
        public static Tune ForLayer(int layer, bool boss) => ForLayer(layer, boss, Style);

        public static Tune ForLayer(int layer, bool boss, int style)
        {
            int[] roots = { 60, 62, 64, 65, 62, 60, 59, 57, 57, 55, 55, 53 };
            int root = roots[Math.Min(Math.Max(0, layer), roots.Length - 1)];
            if (style == 0)
            {
                if (boss)
                    return new Tune { Root = 57 + layer % 3, Minor = true, Bpm = 150, Seed = 900 + layer, LeadDuty = 0.25, Brightness = 0.55, Driving = true };
                return new Tune
                {
                    Root = root, Minor = layer >= 8 || layer == 5 || layer == 7, Bpm = 132 - layer * 1.5, Seed = 100 + layer * 7,
                    LeadDuty = layer < 4 ? 0.25 : layer < 8 ? 0.125 : 0.5, Brightness = Math.Max(0.15, 1 - layer * 0.08),
                };
            }
            bool quiet = style == 2;
            if (boss)
                return new Tune
                {
                    Root = 57 + layer % 3, Minor = true, Bpm = quiet ? 124 : 138, Seed = 900 + layer, LeadDuty = 0.25,
                    Brightness = 0.4, Driving = true, LeadSoftness = quiet ? 0.6 : 0.3, Echo = 0.2,
                };
            // a relaxed walk: unhurried tempo, a rounder lead with an echo, sparse drums; the
            // surface keeps a major key, the deep layers turn minor
            return new Tune
            {
                Root = root,
                Minor = layer >= 6 || layer == 3,
                Bpm = (quiet ? 92 : 108) - layer * 1.0,
                Seed = 100 + layer * 7,
                LeadDuty = 0.25,
                Brightness = Math.Max(0.1, (quiet ? 0.35 : 0.5) - layer * 0.03),
                LeadSoftness = quiet ? 1 : 0.6,
                Echo = quiet ? 0.35 : 0.25,
                Calm = true,
            };
        }
    }
}
