using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DeepDrill.Game;

// The trailer's soundtrack: the trailer tune on the bar grid of timeline.json, a chord to land
// the logo on, and the game's own sound effects where the shots call for them.
var timeline = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
double bpm = timeline.GetProperty("bpm").GetDouble();
double bar = 4 * 60 / bpm;
int rate = SfxSynth.SampleRate;

var starts = new Dictionary<string, double>();
double at = 0;
foreach (var clip in timeline.GetProperty("clips").EnumerateArray())
{
    starts[clip.GetProperty("clip").GetString()] = at;
    at += clip.GetProperty("bars").GetDouble() * bar;
}
double total = at + 1.5;
var mix = new double[(int)(total * rate)];

void Add(float[] sound, double time, double volume, double pitch = 1)
{
    int start = (int)(time * rate);
    int length = (int)(sound.Length / pitch);
    for (int i = 0; i < length && start + i < mix.Length; i++)
    {
        double src = i * pitch;
        int a = (int)src;
        if (a + 1 >= sound.Length) break;
        mix[start + i] += (sound[a] + (sound[a + 1] - sound[a]) * (src - a)) * volume;
    }
}

// music: two bars of drums and bass, then the full tune, cut where the logo lands
var full = MusicSynth.Render(MusicSynth.Trailer(false));
var build = MusicSynth.Render(MusicSynth.Trailer(true));
int barSamples = full.Length / MusicSynth.Bars;
double logo = starts["logo"];
int musicEnd = (int)(logo * rate);
for (int i = 0; i < musicEnd && i < mix.Length; i++)
{
    double v = i < 2 * barSamples ? build[i % build.Length] : full[i % full.Length];
    double fade = Math.Min(1, (musicEnd - i) / (0.02 * rate));
    mix[i] += v * 0.75 * fade;
}

// the logo chord: A major over the A minor tune, so the trailer ends lifted
var sting = new List<SfxVoice>();
foreach (double hz in new[] { 110.0, 220.0, 277.18, 329.63, 440.0 })
    sting.Add(new SfxVoice { Wave = Wave.Triangle, Seconds = 4.5, FromHz = hz, ToHz = hz, Volume = 0.35, Curve = 1.6 });
foreach (double hz in new[] { 554.37, 659.26, 880.0 })
    sting.Add(new SfxVoice { Wave = Wave.Square, Duty = 0.25, Seconds = 3.0, FromHz = hz, ToHz = hz, Volume = 0.08, Curve = 1.8, VibratoHz = 5.5, VibratoDepth = 0.008 });
sting.Add(new SfxVoice { Wave = Wave.Noise, Seconds = 2.5, FromHz = 12000, ToHz = 12000, LowPassFrom = 0.9, LowPassTo = 0.4, Volume = 0.3, Curve = 2.5 });
sting.Add(new SfxVoice { Wave = Wave.Sine, Seconds = 0.6, FromHz = 90, ToHz = 35, Volume = 0.8, Curve = 1.5 });
Add(SfxSynth.Render(sting, 0.8, 7), logo, 0.9);

// effects
var library = SfxSynth.Library();
var rendered = new Dictionary<string, float[]>();
float[] Sound(string name)
{
    if (!rendered.TryGetValue(name, out var s)) rendered[name] = s = SfxSynth.Render(library[name], 0.8, name.GetHashCode());
    return s;
}
foreach (var cue in timeline.GetProperty("sfx").EnumerateArray())
{
    double offset = cue.TryGetProperty("clip", out var c) ? starts[c.GetString()] : 0;
    double first = cue.GetProperty("at").GetDouble();
    double vol = cue.TryGetProperty("vol", out var v) ? v.GetDouble() : 0.6;
    string name = cue.GetProperty("name").GetString();
    double every = cue.TryGetProperty("every", out var e) ? e.GetDouble() : 0;
    double until = cue.TryGetProperty("until", out var u) ? u.GetDouble() : first;
    if (name == "layer_breaks")
    {
        // one break sound per layer as the shot drops through them
        double beat = 7.5 / 9;
        for (int layer = 1; layer <= 9; layer++) Add(Sound(SfxSynth.BreakFor(layer)), offset + first + (layer - 1) * beat, vol);
        continue;
    }
    int n = 0;
    for (double t = first; t <= until + 1e-6; t += every > 0 ? every : 1e9, n++)
    {
        if (name == "coin_rise") { Add(Sound("coin"), offset + t, vol, 1 + n * 0.012); continue; }
        Add(Sound(name), offset + t, vol);
        if (every <= 0) break;
    }
}

// gentle limiter, then 16-bit mono
double peak = 1e-6;
foreach (double s in mix) peak = Math.Max(peak, Math.Abs(s));
double gain = 0.95 / Math.Max(1, peak * 0.9);
using var w = new BinaryWriter(File.Create(args[1]));
int count = mix.Length;
w.Write("RIFF"u8.ToArray()); w.Write(36 + count * 2); w.Write("WAVEfmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8.ToArray()); w.Write(count * 2);
foreach (double s in mix) w.Write((short)Math.Clamp(Math.Tanh(s * gain) * 32767, -32768, 32767));
Console.WriteLine($"trailer audio {total:0.0}s, logo at {logo:0.00}s, peak {peak:0.00}");
