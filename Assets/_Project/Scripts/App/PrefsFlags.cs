using UnityEngine;

namespace CarMatch
{
    public sealed class PrefsFlags : IFlagStore
    {
        public static readonly PrefsFlags Shared = new PrefsFlags();
        public bool Get(string key) { return PlayerPrefs.GetInt(key, 0) == 1; }
        public void Set(string key) { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); }
    }
}
