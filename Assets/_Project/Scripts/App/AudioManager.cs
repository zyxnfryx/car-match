using System.Collections.Generic;
using UnityEngine;

namespace CarMatch
{
    public enum Sfx
    {
        CarTap,
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

        // README volumes from the Musical Producer pack; the three hot cues sit lower.
        static readonly Dictionary<Sfx, Cue> Cues = new Dictionary<Sfx, Cue>
        {
            { Sfx.CarTap, new Cue("car_tap_rev", 3, 0.8f) },
            { Sfx.DriveOut, new Cue("drive_out_whoosh", 3, 0.55f) },
            { Sfx.SlotArrive, new Cue("slot_pop", 3, 0.75f) },
            { Sfx.MatchClear, new Cue("match3_clear", 0, 0.9f) },
            { Sfx.Blocked, new Cue("blocked_honk", 0, 0.6f) },
            { Sfx.Click, new Cue("button_click", 0, 0.6f) },
            { Sfx.BoosterUndo, new Cue("booster_undo", 0, 0.85f) },
            { Sfx.BoosterShuffle, new Cue("booster_shuffle", 0, 0.85f) },
            { Sfx.BoosterSuper, new Cue("booster_super", 0, 0.85f) },
            { Sfx.BoosterMagnet, new Cue("booster_magnet", 0, 0.7f) },
            { Sfx.Win, new Cue("level_win_fanfare", 0, 0.9f) },
            { Sfx.MascotCheer, new Cue("mascot_cheer", 0, 0.8f) },
            { Sfx.Fail, new Cue("level_fail", 0, 0.75f) },
            { Sfx.Coin, new Cue("coin_gain", 3, 0.65f) }
        };

        // drive_out_whoosh_01 runs hot: trim that one variant further.
        static float VariantTrim(string clip) { return clip == "drive_out_whoosh_01" ? 0.8f : 1f; }

        static AudioManager _live;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<Sfx, int> _last = new Dictionary<Sfx, int>();
        AudioSource[] _pool;
        AudioSource _music;
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
            ApplyMute();
        }

        void ApplyMute()
        {
            bool m = Muted;
            if (_pool != null) for (int i = 0; i < _pool.Length; i++) _pool[i].mute = m;
            if (_music != null) _music.mute = m;
        }

        public static void Play(Sfx sfx) { try { Live.PlayCue(sfx, 1f); } catch (System.Exception) { } }
        public static void Play(Sfx sfx, float volumeScale) { try { Live.PlayCue(sfx, volumeScale); } catch (System.Exception) { } }

        public static void StartMusic()
        {
            try
            {
                var live = Live;
                if (live._music.isPlaying) return;
                var clip = live.Load("music_bed_loop");
                if (clip == null) return;
                live._music.clip = clip;
                live._music.volume = MusicVolume;
                live._music.Play();
            }
            catch (System.Exception) { }
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
            if (_music != null) _music.volume = MusicVolume * _duck;
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
