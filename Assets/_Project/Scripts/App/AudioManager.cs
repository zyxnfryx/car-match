using System.Collections.Generic;
using UnityEngine;

namespace CarMatch
{
    public enum Sfx
    {
        CarTap,
        CarSqueak,
        DriveOut,
        SlotArrive,
        MatchClear,
        Blocked,
        Click,
        BoosterUndo,
        BoosterShuffle,
        BoosterSuper,
        BoosterMagnet,
        Win,
        MascotCheer,
        Fail,
        Coin
    }

    // One pooled one-shot host + a music bed, after Flock Five's SfxHost/MixDesk (pool, duck),
    // minus the bird voices and weather. Clips load from Resources/Audio by name; a missing clip
    // is skipped quietly. Variants (_00.._02) are picked at random without repeating the last one,
    // with +/-3% pitch jitter. Mute is saved to PlayerPrefs.
    public sealed class AudioManager : MonoBehaviour
    {
        public const string MutePref = "lotloop.mute";
        public const string MusicPref = "lotloop.music";
        const int PoolSize = 10;
        const float MusicVolume = 0.4f;
        const float DuckVolume = 0.25f / 0.4f;

        struct Cue
        {
            public string Name;
            public int Variants;
            public float Volume;
            public Cue(string name, int variants, float volume) { Name = name; Variants = variants; Volume = volume; }
        }

        // Audio pack v2 README volumes (all SFX are loudness-matched at -14 LUFS).
        static readonly Dictionary<Sfx, Cue> Cues = new Dictionary<Sfx, Cue>
        {
            { Sfx.CarTap, new Cue("car_tap_rev", 5, 0.85f) },
            { Sfx.CarSqueak, new Cue("car_select_squeak", 3, 0.45f) },
            { Sfx.DriveOut, new Cue("drive_out_whoosh", 3, 0.65f) },
            { Sfx.SlotArrive, new Cue("slot_pop", 3, 0.7f) },
            { Sfx.MatchClear, new Cue("match3_clear", 0, 0.9f) },
            { Sfx.Blocked, new Cue("blocked_honk", 0, 0.7f) },
            { Sfx.Click, new Cue("button_click", 0, 0.5f) },
            { Sfx.BoosterUndo, new Cue("booster_undo", 0, 0.8f) },
            { Sfx.BoosterShuffle, new Cue("booster_shuffle", 0, 0.8f) },
            { Sfx.BoosterSuper, new Cue("booster_super", 0, 0.8f) },
            { Sfx.BoosterMagnet, new Cue("booster_magnet", 0, 0.8f) },
            { Sfx.Win, new Cue("level_win_fanfare", 0, 0.9f) },
            { Sfx.MascotCheer, new Cue("mascot_cheer", 0, 0.75f) },
            { Sfx.Fail, new Cue("level_fail", 0, 0.7f) },
            { Sfx.Coin, new Cue("coin_gain", 3, 0.6f) }
        };

        static float VariantTrim(string clip) { return 1f; }

        static AudioManager _live;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<Sfx, int> _last = new Dictionary<Sfx, int>();
        AudioSource[] _pool;
        AudioSource _music;
        AudioSource _musicB;
        float _fade = 1f;
        int _next;
        float _duck = 1f;
        float _duckUntil;

        public static AudioManager Live
        {
            get
            {
                if (_live != null) return _live;
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                _live = go.AddComponent<AudioManager>();
                return _live;
            }
        }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutePref, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(MutePref, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_live != null) _live.ApplyMute();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _live = null; }

        void Awake()
        {
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool[i] = src;
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _musicB = gameObject.AddComponent<AudioSource>();
            _musicB.loop = true;
            _musicB.playOnAwake = false;
            ApplyMute();
        }

        void ApplyMute()
        {
            bool m = Muted;
            if (_pool != null) for (int i = 0; i < _pool.Length; i++) _pool[i].mute = m;
            if (_music != null) _music.mute = m;
            if (_musicB != null) _musicB.mute = m;
        }

        public static void Play(Sfx sfx) { try { Live.PlayCue(sfx, 1f); } catch (System.Exception) { } }
        public static void Play(Sfx sfx, float volumeScale) { try { Live.PlayCue(sfx, volumeScale); } catch (System.Exception) { } }

        public const string HomeMusic = "music_home_loop";
        public const string GameMusic = "music_gameplay_loop";

        // Crossfades (~1 s) to a looping WAV bed; same track keeps playing.
        public static void Music(string track)
        {
            try
            {
                var live = Live;
                if (live._music.clip != null && live._music.clip.name == track && live._music.isPlaying) return;
                var clip = live.Load(track);
                if (clip == null) return;
                var old = live._music;
                live._music = live._musicB;
                live._musicB = old;
                live._music.clip = clip;
                live._music.volume = 0f;
                live._music.Play();
                live._fade = 0f;
            }
            catch (System.Exception) { }
        }

        public static void StartMusic() { Music(HomeMusic); }

        // Car tap: random rev plus the squeak layer 0-30 ms later.
        public static void CarTap()
        {
            Play(Sfx.CarTap);
            if (_live != null) _live.StartCoroutine(_live.Later(Sfx.CarSqueak, Random.Range(0f, 0.03f)));
        }

        System.Collections.IEnumerator Later(Sfx sfx, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayCue(sfx, 1f);
        }

        // Music drops to ~0.25 under the win and fail cues.
        public static void Duck(float seconds)
        {
            if (_live == null) return;
            _live._duckUntil = Mathf.Max(_live._duckUntil, Time.unscaledTime + seconds);
        }

        void Update()
        {
            float target = Time.unscaledTime < _duckUntil ? DuckVolume : 1f;
            _duck = Mathf.MoveTowards(_duck, target, Time.unscaledDeltaTime * 2.5f);
            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime);
            if (_music != null) _music.volume = MusicVolume * _duck * _fade;
            if (_musicB != null)
            {
                _musicB.volume = MusicVolume * _duck * (1f - _fade);
                if (_fade >= 1f && _musicB.isPlaying) _musicB.Stop();
            }
        }

        void PlayCue(Sfx sfx, float scale)
        {
            Cue cue;
            if (!Cues.TryGetValue(sfx, out cue)) return;
            string name = cue.Name;
            if (cue.Variants > 0)
            {
                int last;
                if (!_last.TryGetValue(sfx, out last)) last = -1;
                int pick = Random.Range(0, cue.Variants);
                if (cue.Variants > 1 && pick == last) pick = (pick + 1) % cue.Variants;
                _last[sfx] = pick;
                name = cue.Name + "_" + pick.ToString("00");
            }
            var clip = Load(name);
            if (clip == null) return;
            if (sfx == Sfx.Win || sfx == Sfx.Fail) Duck(clip.length + 0.4f);
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.pitch = Random.Range(0.97f, 1.03f);
            src.PlayOneShot(clip, cue.Volume * VariantTrim(name) * scale);
        }

        AudioClip Load(string name)
        {
            AudioClip clip;
            if (_clips.TryGetValue(name, out clip)) return clip;
            clip = Resources.Load<AudioClip>("Audio/" + name);
            _clips[name] = clip;
            return clip;
        }
    }
}
