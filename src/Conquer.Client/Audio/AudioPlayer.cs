using Conquer.Presentation;
using NVorbis;
using Silk.NET.OpenAL;

namespace Conquer.Client.Audio;

/// <summary>
/// Plays the game's sounds and music with OpenAL. The sound effects are decoded once at start; the music streams from
/// its OGG file in half-second chunks, picks another track of the playlist when one ends, and fades out when the
/// playlist changes (war breaks out, peace returns, a new age). Without a sound device it stays silent.
/// </summary>
public sealed unsafe class AudioPlayer : IDisposable
{
    /// <summary>Sound effects that can ring at once.</summary>
    private const int Voices = 8;
    /// <summary>The same sound does not ring again within this many seconds (a battle starting on every front at once).</summary>
    private const double Repeat = 0.25;
    private const int MusicBuffers = 4;
    private const double FadeSeconds = 2;

    private static readonly Dictionary<SoundCue, string> Files = new()
    {
        [SoundCue.Click] = "click", [SoundCue.Confirm] = "confirm", [SoundCue.Alert] = "alert", [SoundCue.Battle] = "battle",
        [SoundCue.War] = "war", [SoundCue.Peace] = "peace", [SoundCue.Build] = "build", [SoundCue.Discovery] = "discovery",
        [SoundCue.CityFounded] = "city_founded", [SoundCue.Coins] = "coins", [SoundCue.Bell] = "bell",
    };

    private readonly ALContext? _alc;
    private readonly AL? _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly Dictionary<SoundCue, uint> _sounds = [];
    private readonly Dictionary<SoundCue, double> _lastPlayed = [];
    private readonly uint[] _voices = new uint[Voices];
    private readonly uint _music;
    private readonly uint[] _musicBuffers = new uint[MusicBuffers];
    private readonly Random _random = new();
    private double _time;

    private VorbisReader? _track;
    private string? _trackName;
    private bool _trackEnded;
    private IReadOnlyList<string> _playlist = [];
    /// <summary>1 while the music plays at its volume; it falls to 0 before changing to another playlist.</summary>
    private double _fade = 1;
    private bool _fadingOut;

    public bool Available { get; }

    public AudioPlayer()
    {
        try
        {
            _alc = ALContext.GetApi(soft: true);
            _al = AL.GetApi(soft: true);
            _device = _alc.OpenDevice("");
            if (_device == null) return;
            _context = _alc.CreateContext(_device, null);
            _alc.MakeContextCurrent(_context);
            foreach (var (cue, name) in Files)
                if (Open($"Audio/Sfx/{name}.ogg") is { } reader)
                    using (reader)
                        _sounds[cue] = Decode(reader);
            for (int i = 0; i < Voices; i++) _voices[i] = _al.GenSource();
            _music = _al.GenSource();
            for (int i = 0; i < MusicBuffers; i++) _musicBuffers[i] = _al.GenBuffer();
            Available = true;
        }
        catch (Exception e) when (e is DllNotFoundException or InvalidOperationException or EntryPointNotFoundException or FileNotFoundException)
        {
            // No OpenAL on this system: the game plays in silence.
        }
    }

    private static VorbisReader? Open(string resource) =>
        typeof(AudioPlayer).Assembly.GetManifestResourceStream(resource) is { } stream ? new VorbisReader(stream, closeOnDispose: true) : null;

    /// <summary>A whole effect into an OpenAL buffer.</summary>
    private uint Decode(VorbisReader reader)
    {
        var samples = new List<short>();
        var chunk = new float[4096];
        int read;
        while ((read = reader.ReadSamples(chunk, 0, chunk.Length)) > 0)
            for (int i = 0; i < read; i++) samples.Add(ToPcm(chunk[i]));
        uint buffer = _al!.GenBuffer();
        _al.BufferData(buffer, reader.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, samples.ToArray(), reader.SampleRate);
        return buffer;
    }

    private static short ToPcm(float sample) => (short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue);

    /// <summary>
    /// Once a frame: rings the sounds asked for, keeps the music streaming, and changes track or playlist when it is time.
    /// </summary>
    public void Update(double dt, IReadOnlyList<string> playlist, IEnumerable<SoundCue> sounds, AudioSettings settings)
    {
        if (!Available) return;
        _time += dt;
        foreach (var cue in sounds) Ring(cue, settings.Sounds);
        Stream(dt, playlist, settings.Music);
    }

    private void Ring(SoundCue cue, double volume)
    {
        if (volume <= 0 || !_sounds.TryGetValue(cue, out uint buffer)) return;
        if (_lastPlayed.TryGetValue(cue, out double last) && _time - last < Repeat) return;
        _lastPlayed[cue] = _time;
        foreach (uint voice in _voices)
        {
            _al!.GetSourceProperty(voice, GetSourceInteger.SourceState, out int state);
            if (state == (int)SourceState.Playing) continue;
            _al.SetSourceProperty(voice, SourceInteger.Buffer, (int)buffer);
            _al.SetSourceProperty(voice, SourceFloat.Gain, (float)volume);
            _al.SourcePlay(voice);
            return;
        }
    }

    private void Stream(double dt, IReadOnlyList<string> playlist, double volume)
    {
        var al = _al!;
        // A new playlist that no longer has the current track: fade it out first.
        if (!playlist.SequenceEqual(_playlist))
        {
            _playlist = playlist;
            if (_trackName != null && !playlist.Contains(_trackName)) _fadingOut = true;
        }
        if (_fadingOut)
        {
            _fade -= dt / FadeSeconds;
            if (_fade <= 0) StopTrack();
        }
        else _fade = Math.Min(1, _fade + dt / FadeSeconds);
        al.SetSourceProperty(_music, SourceFloat.Gain, (float)(volume * Math.Max(0, _fade)));

        if (_track == null)
        {
            if (volume <= 0 || _playlist.Count == 0) return;
            StartTrack();
            return;
        }

        // Refill the buffers already played, and keep playing if it ran dry.
        al.GetSourceProperty(_music, GetSourceInteger.BuffersProcessed, out int processed);
        for (int i = 0; i < processed; i++)
        {
            uint buffer = 0;
            al.SourceUnqueueBuffers(_music, 1, &buffer);
            if (!_trackEnded && Fill(buffer)) al.SourceQueueBuffers(_music, 1, &buffer);
        }
        al.GetSourceProperty(_music, GetSourceInteger.SourceState, out int state);
        al.GetSourceProperty(_music, GetSourceInteger.BuffersQueued, out int queued);
        if (state != (int)SourceState.Playing)
        {
            if (queued > 0) al.SourcePlay(_music);
            else if (_trackEnded) StopTrack(); // the next frame starts another track
        }
    }

    /// <summary>A random track of the playlist, not the one just heard if there are others.</summary>
    private void StartTrack()
    {
        var choices = _playlist.Where(t => t != _trackName || _playlist.Count == 1).ToList();
        string name = choices[_random.Next(choices.Count)];
        if (Open($"Audio/Music/{name}.ogg") is not { } reader) return;
        _track = reader;
        _trackName = name;
        _trackEnded = false;
        _fadingOut = false;
        _fade = 0;
        foreach (uint buffer in _musicBuffers)
        {
            uint b = buffer;
            if (Fill(b)) _al!.SourceQueueBuffers(_music, 1, &b);
        }
        _al!.SourcePlay(_music);
    }

    /// <summary>The next half second of the track into the buffer; false once the track is over.</summary>
    private bool Fill(uint buffer)
    {
        var track = _track!;
        var chunk = new float[track.SampleRate / 2 * track.Channels];
        int read = track.ReadSamples(chunk, 0, chunk.Length);
        if (read <= 0)
        {
            _trackEnded = true;
            return false;
        }
        var pcm = new short[read];
        for (int i = 0; i < read; i++) pcm[i] = ToPcm(chunk[i]);
        _al!.BufferData(buffer, track.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, pcm, track.SampleRate);
        return true;
    }

    private void StopTrack()
    {
        var al = _al!;
        al.SourceStop(_music);
        al.GetSourceProperty(_music, GetSourceInteger.BuffersQueued, out int queued);
        for (int i = 0; i < queued; i++)
        {
            uint buffer = 0;
            al.SourceUnqueueBuffers(_music, 1, &buffer);
        }
        _track?.Dispose();
        _track = null;
        _fadingOut = false;
    }

    public void Dispose()
    {
        if (!Available) return;
        StopTrack();
        var al = _al!;
        foreach (uint voice in _voices) al.DeleteSource(voice);
        al.DeleteSource(_music);
        foreach (uint buffer in _musicBuffers) al.DeleteBuffer(buffer);
        foreach (uint buffer in _sounds.Values) al.DeleteBuffer(buffer);
        _alc!.MakeContextCurrent(null);
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
    }
}
