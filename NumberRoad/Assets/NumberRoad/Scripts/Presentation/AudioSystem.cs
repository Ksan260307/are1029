using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Presentation
{
    public enum Sfx
    {
        Shoot,
        Hit,
        EnemyDie,
        SoldierDie,
        GateGood,
        GateBad,
        PowerUp,
        CrateBreak,
        Smash,
        Coin,
        Click,
        Win,
        Lose,
        Start,
        Combo,
        ClassUp,
        Recruit,
        Warning,
        Explosion,
        Crit,
        Chest,
        Revive,
        Skill,
        Banner,
        Gem,
    }

    /// <summary>
    /// All sound is synthesised at startup (no audio files in the repo). Clips
    /// are created non-streamed with their full data, which WebGL supports.
    /// </summary>
    public sealed class AudioSystem : MonoBehaviour
    {
        const int Voices = 10;
        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        AudioSource[] _voices;
        AudioSource _music;
        AudioSource _bossMusic;
        int _nextVoice;
        bool _sound = true;
        bool _musicOn = true;
        bool _boss;
        const float MusicVolume = 0.42f;

        public void Init(bool sound, bool music)
        {
            _sound = sound;
            _musicOn = music;
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = MusicVolume;
            _bossMusic = gameObject.AddComponent<AudioSource>();
            _bossMusic.playOnAwake = false;
            _bossMusic.loop = true;
            _bossMusic.volume = 0f;

            try
            {
                foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
                {
                    _clips[sfx] = Synth.CreateClip(sfx.ToString(), Synth.Render(sfx));
                }
                StartCoroutine(BuildMusic());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NumberRoad] Audio disabled: {e.Message}");
                _clips.Clear();
            }
        }

        IEnumerator BuildMusic()
        {
            yield return null;
            float[] data = null;
            IEnumerator job = Synth.RenderMusic(Synth.MainSong, result => data = result);
            while (job.MoveNext()) yield return null;
            if (data == null) yield break;
            _music.clip = Synth.CreateClip("Music", data);
            if (_musicOn) _music.Play();

            yield return null;
            data = null;
            job = Synth.RenderMusic(Synth.BossSong, result => data = result);
            while (job.MoveNext()) yield return null;
            if (data != null) _bossMusic.clip = Synth.CreateClip("BossMusic", data);
        }

        /// <summary>Crossfades to the faster boss track (and back).</summary>
        public void SetBossMusic(bool boss)
        {
            _boss = boss;
            if (boss && _musicOn && _bossMusic.clip != null && !_bossMusic.isPlaying) _bossMusic.Play();
        }

        void Update()
        {
            if (_music == null) return;
            float dt = Time.unscaledDeltaTime;
            float main = _musicOn && !_boss ? MusicVolume : 0f;
            float boss = _musicOn && _boss ? MusicVolume : 0f;
            _music.volume = Mathf.MoveTowards(_music.volume, main, dt * 0.8f);
            _bossMusic.volume = Mathf.MoveTowards(_bossMusic.volume, boss, dt * 0.8f);
            if (_bossMusic.isPlaying && _bossMusic.volume <= 0f && !_boss) _bossMusic.Stop();
        }

        public bool SoundOn
        {
            get => _sound;
            set => _sound = value;
        }

        public bool MusicOn
        {
            get => _musicOn;
            set
            {
                _musicOn = value;
                if (_music == null || _music.clip == null) return;
                if (value && !_music.isPlaying) _music.Play();
                else if (!value)
                {
                    _music.Stop();
                    _bossMusic.Stop();
                }
            }
        }

        /// <summary>Plays a one-shot. <paramref name="minInterval"/> rate-limits very frequent sounds.</summary>
        public void Play(Sfx sfx, float volume = 1f, float pitchJitter = 0.06f, float minInterval = 0.03f)
        {
            if (!_sound || !_clips.TryGetValue(sfx, out AudioClip clip)) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < minInterval) return;
            _lastPlayed[sfx] = now;

            AudioSource source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }

        /// <summary>Plays at an exact pitch (combo and coin sounds climb as they repeat).</summary>
        public void PlayPitched(Sfx sfx, float volume, float pitch, float minInterval = 0.03f)
        {
            if (!_sound || !_clips.TryGetValue(sfx, out AudioClip clip)) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < minInterval) return;
            _lastPlayed[sfx] = now;
            AudioSource source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            source.pitch = Mathf.Clamp(pitch, 0.5f, 2.5f);
            source.PlayOneShot(clip, volume);
        }
    }

    /// <summary>Tiny additive synthesiser for the sound effects and the music loop.</summary>
    public static class Synth
    {
        public const int Rate = 22050;

        public static AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, data.Length), 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(int i)
        {
            uint x = (uint)i * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0xFFFF) / 32767.5f - 1f;
        }

        static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Triangle(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);
        static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);

        delegate float Voice(float t, int i);

        static float[] Build(float seconds, Voice voice)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float fade = Mathf.Clamp01((n - i) / (Rate * 0.01f));
                data[i] = Mathf.Clamp(voice(t, i) * fade, -1f, 1f);
            }
            return data;
        }

        /// <summary>Phase of a linear frequency sweep from f0 to f1 over d seconds.</summary>
        static float Sweep(float t, float f0, float f1, float d)
        {
            t = Mathf.Min(t, d);
            return f0 * t + (f1 - f0) * t * t / (2f * d);
        }

        static float Arpeggio(float t, float[] notes, float step, Func<float, float> wave, float decay)
        {
            int index = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - index * step;
            return wave(Midi(notes[index]) * t) * Mathf.Exp(-local * decay);
        }

        public static float[] Render(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Shoot:
                    return Build(0.07f, (t, i) =>
                        (Noise(i) - Noise(i - 1)) * 0.12f * Mathf.Exp(-t * 70f) +
                        Sine(Sweep(t, 1500f, 700f, 0.07f)) * 0.16f * Mathf.Exp(-t * 45f));
                case Sfx.Hit:
                    return Build(0.06f, (t, i) =>
                        Square(Sweep(t, 260f, 120f, 0.06f)) * 0.12f * Mathf.Exp(-t * 55f) +
                        Noise(i) * 0.12f * Mathf.Exp(-t * 90f));
                case Sfx.EnemyDie:
                    return Build(0.18f, (t, i) =>
                        Square(Sweep(t, 560f, 110f, 0.18f)) * 0.16f * Mathf.Exp(-t * 18f) +
                        Noise(i) * 0.1f * Mathf.Exp(-t * 30f));
                case Sfx.SoldierDie:
                    return Build(0.16f, (t, i) => Triangle(Sweep(t, 760f, 240f, 0.16f)) * 0.3f * Mathf.Exp(-t * 16f));
                case Sfx.GateGood:
                    return Build(0.42f, (t, i) => Arpeggio(t, new[] { 72f, 76f, 79f, 84f }, 0.075f, Triangle, 7f) * 0.32f);
                case Sfx.GateBad:
                    return Build(0.42f, (t, i) => Arpeggio(t, new[] { 67f, 63f, 60f }, 0.1f, p => Square(p) * 0.6f, 6f) * 0.25f);
                case Sfx.PowerUp:
                    return Build(0.55f, (t, i) =>
                        Arpeggio(t, new[] { 67f, 71f, 74f, 79f, 83f, 86f }, 0.06f, Sine, 5f) * 0.3f +
                        Arpeggio(t, new[] { 79f, 83f, 86f, 91f, 95f, 98f }, 0.06f, Triangle, 9f) * 0.1f);
                case Sfx.CrateBreak:
                    return Build(0.3f, (t, i) =>
                        Noise(i) * 0.35f * Mathf.Exp(-t * 16f) +
                        Square(Sweep(t, 140f, 60f, 0.3f)) * 0.18f * Mathf.Exp(-t * 14f));
                case Sfx.Smash:
                    return Build(0.5f, (t, i) =>
                        Sine(Sweep(t, 110f, 38f, 0.5f)) * 0.7f * Mathf.Exp(-t * 6f) +
                        Noise(i) * 0.3f * Mathf.Exp(-t * 11f));
                case Sfx.Coin:
                    return Build(0.2f, (t, i) => Sine(Midi(t < 0.06f ? 88f : 93f) * t) * 0.28f * Mathf.Exp(-(t < 0.06f ? t : t - 0.06f) * 14f));
                case Sfx.Click:
                    return Build(0.05f, (t, i) => Sine(900f * t) * 0.3f * Mathf.Exp(-t * 70f));
                case Sfx.Win:
                    return Build(1.3f, (t, i) =>
                        Arpeggio(t, new[] { 72f, 76f, 79f, 84f, 84f, 88f }, 0.16f, Triangle, 3f) * 0.3f +
                        Arpeggio(t, new[] { 60f, 64f, 67f, 72f, 72f, 76f }, 0.16f, Sine, 2.5f) * 0.25f);
                case Sfx.Lose:
                    return Build(1.1f, (t, i) =>
                        Arpeggio(t, new[] { 67f, 66f, 65f, 64f, 64f }, 0.2f, Triangle, 3f) * 0.3f +
                        Arpeggio(t, new[] { 55f, 54f, 53f, 52f, 52f }, 0.2f, Sine, 3f) * 0.2f);
                case Sfx.Start:
                    return Build(0.35f, (t, i) =>
                        Noise(i) * 0.15f * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) +
                        Triangle(Sweep(t, 300f, 900f, 0.35f)) * 0.15f * Mathf.Exp(-t * 4f));
                case Sfx.Combo:
                    return Build(0.09f, (t, i) => Sine(Midi(84f) * t) * 0.25f * Mathf.Exp(-t * 30f) + Sine(Midi(91f) * t) * 0.1f * Mathf.Exp(-t * 40f));
                case Sfx.ClassUp:
                    return Build(0.9f, (t, i) =>
                        Arpeggio(t, new[] { 60f, 64f, 67f, 72f, 76f, 79f, 84f }, 0.07f, Triangle, 3f) * 0.28f +
                        Sine(Midi(96f) * t) * 0.06f * Mathf.Sin(t * 30f) * Mathf.Exp(-t * 2f));
                case Sfx.Recruit:
                    return Build(0.35f, (t, i) => Arpeggio(t, new[] { 67f, 72f, 76f }, 0.08f, p => Square(p) * 0.5f, 8f) * 0.22f);
                case Sfx.Warning:
                    return Build(0.8f, (t, i) => Square(t < 0.4f ? 520f * t : 520f * 0.4f + 390f * (t - 0.4f)) * 0.12f * (1f - t / 0.8f));
                case Sfx.Explosion:
                    return Build(0.6f, (t, i) =>
                        Noise(i) * 0.45f * Mathf.Exp(-t * 7f) +
                        Sine(Sweep(t, 90f, 30f, 0.6f)) * 0.55f * Mathf.Exp(-t * 5f));
                case Sfx.Crit:
                    return Build(0.12f, (t, i) => Square(Sweep(t, 1200f, 600f, 0.12f)) * 0.12f * Mathf.Exp(-t * 25f) + Noise(i) * 0.08f * Mathf.Exp(-t * 60f));
                case Sfx.Chest:
                    return Build(0.8f, (t, i) =>
                        Noise(i) * 0.2f * Mathf.Exp(-t * 20f) +
                        Arpeggio(t, new[] { 76f, 79f, 84f, 88f, 91f }, 0.09f, Sine, 4f) * 0.3f);
                case Sfx.Revive:
                    return Build(0.9f, (t, i) => Arpeggio(t, new[] { 64f, 67f, 71f, 76f, 79f, 83f }, 0.12f, Sine, 2.5f) * 0.3f);
                case Sfx.Skill:
                    return Build(0.9f, (t, i) =>
                        (Noise(i) - Noise(i - 1)) * 0.25f * Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI) +
                        Triangle(Sweep(t, 200f, 1200f, 0.9f)) * 0.12f * Mathf.Exp(-t * 2f));
                case Sfx.Banner:
                    return Build(0.3f, (t, i) => Arpeggio(t, new[] { 79f, 84f }, 0.1f, Triangle, 6f) * 0.25f);
                case Sfx.Gem:
                    return Build(0.35f, (t, i) => Sine(Midi(96f) * t) * 0.22f * Mathf.Exp(-t * 9f) + Sine(Midi(103f) * t) * 0.12f * Mathf.Exp(-t * 12f));
                default:
                    return new float[1];
            }
        }

        // --- Music -------------------------------------------------------------------

        /// <summary>An 8-bar loop: tempo, chord root and quality per bar, and one melody line per bar.</summary>
        public sealed class Song
        {
            public float Bpm;
            public int[] Roots;
            public int[] Minor;
            // one string per bar, 8 eighth notes; "-" holds the previous note, "." is a rest
            public string[] Melody;
        }

        public static readonly Song MainSong = new Song
        {
            Bpm = 124f,
            Roots = new[] { 60, 55, 57, 53, 60, 55, 53, 55 },
            Minor = new[] { 0, 0, 1, 0, 0, 0, 0, 0 },
            Melody = new[]
            {
                "E5 - G5 - A5 G5 E5 -",
                "D5 - G5 - B5 A5 G5 -",
                "C5 - E5 - A5 G5 E5 -",
                "F5 - A5 - C6 - A5 .",
                "E5 G5 C6 - B5 A5 G5 -",
                "D5 G5 B5 - A5 G5 D5 -",
                "C5 F5 A5 - G5 F5 E5 -",
                "D5 - G5 - B5 - D6 .",
            },
        };

        /// <summary>Faster minor-key loop for boss fights.</summary>
        public static readonly Song BossSong = new Song
        {
            Bpm = 150f,
            Roots = new[] { 57, 53, 55, 52, 57, 53, 50, 52 },
            Minor = new[] { 1, 0, 0, 0, 1, 0, 1, 0 },
            Melody = new[]
            {
                "A5 A5 C6 A5 E6 - D6 C6",
                "A5 - F5 - A5 C6 A5 -",
                "G5 G5 B5 G5 D6 - C6 B5",
                "G#5 - E5 - G#5 B5 E6 -",
                "A5 A5 C6 A5 E6 - G6 E6",
                "F6 - E6 D6 C6 - A5 -",
                "D6 C6 A5 F5 D5 - F5 A5",
                "E5 G#5 B5 E6 G#6 - E6 .",
            },
        };

        static float ParseNote(string token)
        {
            int[] steps = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
            int letter = char.ToUpperInvariant(token[0]) - 'A';
            int octave = token[token.Length - 1] - '0';
            int semis = steps[letter];
            if (token.Length == 3 && token[1] == '#') semis++;
            return 12 * (octave + 1) + semis;
        }

        /// <summary>Renders a seamless 8-bar loop, yielding between voices to keep frames short.</summary>
        public static IEnumerator RenderMusic(Song song, Action<float[]> done)
        {
            const int Bars = 8;
            int[] Roots = song.Roots;
            int[] Minor = song.Minor;
            string[] Melody = song.Melody;
            float beat = 60f / song.Bpm;
            float eighth = beat / 2f;
            int length = Mathf.RoundToInt(Bars * 4 * beat * Rate);
            var mix = new float[length];

            // drums: kick on 1 & 3, snare on 2 & 4, hats on eighths
            for (int b = 0; b < Bars * 4; b++)
            {
                float start = b * beat;
                if (b % 2 == 0) AddNote(mix, start, 0.22f, (t, i) => Sine(Sweep(t, 150f, 45f, 0.22f)) * Mathf.Exp(-t * 14f) * 0.55f);
                else AddNote(mix, start, 0.16f, (t, i) => Noise(i) * Mathf.Exp(-t * 22f) * 0.2f);
            }
            for (int e = 0; e < Bars * 8; e++)
            {
                AddNote(mix, e * eighth, 0.04f, (t, i) => (Noise(i) - Noise(i - 1)) * Mathf.Exp(-t * 90f) * (e % 2 == 1 ? 0.07f : 0.04f));
            }
            yield return null;

            // bass: bouncing root / fifth eighths
            for (int bar = 0; bar < Bars; bar++)
            {
                for (int e = 0; e < 8; e++)
                {
                    float note = Roots[bar] - 24 + (e % 4 == 3 ? 7 : 0);
                    float f = Midi(note);
                    AddNote(mix, (bar * 8 + e) * eighth, eighth * 0.9f, (t, i) => Triangle(f * t) * Mathf.Exp(-t * 5f) * 0.22f);
                }
            }
            yield return null;

            // soft arpeggio pad on sixteenths
            for (int bar = 0; bar < Bars; bar++)
            {
                int third = Minor[bar] == 1 ? 3 : 4;
                int[] chord = { 0, third, 7, 12 };
                for (int s = 0; s < 16; s++)
                {
                    float f = Midi(Roots[bar] + chord[s % 4]);
                    AddNote(mix, (bar * 16 + s) * eighth * 0.5f, eighth * 0.5f, (t, i) => Square(f * t) * Mathf.Exp(-t * 18f) * 0.035f);
                }
            }
            yield return null;

            // melody
            for (int bar = 0; bar < Bars; bar++)
            {
                string[] tokens = Melody[bar].Split(' ');
                for (int e = 0; e < tokens.Length; e++)
                {
                    string token = tokens[e];
                    if (token == "-" || token == ".") continue;
                    int hold = 1;
                    while (e + hold < tokens.Length && tokens[e + hold] == "-") hold++;
                    float f = Midi(ParseNote(token));
                    float dur = hold * eighth * 0.95f;
                    AddNote(mix, (bar * 8 + e) * eighth, dur, (t, i) =>
                        (Triangle(f * t) * 0.7f + Square(f * t) * 0.12f) * Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 2.2f) * 0.16f);
                }
            }
            yield return null;

            float peak = 0.0001f;
            for (int i = 0; i < length; i++) peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
            float gain = 0.7f / peak;
            for (int i = 0; i < length; i++) mix[i] *= gain;
            done(mix);
        }

        static void AddNote(float[] buffer, float start, float duration, Voice voice)
        {
            int s = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(duration * Rate);
            int release = Mathf.Max(1, Mathf.RoundToInt(0.008f * Rate));
            for (int k = 0; k < n; k++)
            {
                int index = (s + k) % buffer.Length;
                float fade = k > n - release ? (n - k) / (float)release : 1f;
                buffer[index] += voice(k / (float)Rate, s + k) * fade;
            }
        }
    }
}
