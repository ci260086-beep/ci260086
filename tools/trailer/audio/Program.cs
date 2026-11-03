using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DeepDrill.Game;

// The trailer's soundtrack: TrailerMusic laid out by the sections of timeline.json, with the
// game's own sound effects where the shots call for them.
// Run: dotnet run -- ../timeline.json out.wav
var timeline = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
double bpm = timeline.GetProperty("bpm").GetDouble();
double bar = 4 * 60 / bpm;
int rate = DeepDrill.Trailer.TrailerMusic.Rate;
double sfxPitch = SfxSynth.SampleRate / (double)rate; // effects are rendered at half the music's rate

var starts = new Dictionary<string, double>();
var sections = new List<(string kind, int start, int bars)>();
double at = 0;
int barAt = 0;
foreach (var clip in timeline.GetProperty("clips").EnumerateArray())
{
    starts[clip.GetProperty("clip").GetString()] = at;
    int bars = clip.GetProperty("bars").GetInt32();
    string kind = clip.TryGetProperty("section", out var k) ? k.GetString() : "systems";
    if (sections.Count > 0 && sections[^1].kind == kind) sections[^1] = (kind, sections[^1].start, sections[^1].bars + bars);
    else sections.Add((kind, barAt, bars));
    barAt += bars;
    at += bars * bar;
}
double total = at + 1.5;
var music = new DeepDrill.Trailer.TrailerMusic(bpm, total);
foreach (var (kind, start, bars) in sections) music.Section(kind, start, bars);
var mix = music.Mixdown();
double logo = starts["logo"];

void Add(float[] sound, double time, double volume, double pitch = 1)
{
    int start = (int)(time * rate);
    pitch *= sfxPitch;
    int length = (int)(sound.Length / pitch);
    for (int i = 0; i < length && start + i < mix.Length; i++)
    {
        double src = i * pitch;
        int a = (int)src;
        if (a + 1 >= sound.Length) break;
        mix[start + i] += (sound[a] + (sound[a + 1] - sound[a]) * (src - a)) * volume;
    }
}

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
        double beat = 2 * bar / 8;
        for (int layer = 1; layer <= 8; layer++) Add(Sound(SfxSynth.BreakFor(layer)), offset + first + (layer - 1) * beat, vol);
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
