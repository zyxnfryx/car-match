using UnityEngine;

namespace CarMatch
{
    // Non-consumable VIP. Product id is the one to create in App Store Connect and Play Console.
    // Flock Five's VIP is the no-ads purchase (com.zfxgames.flockfive.noads). Lot Loop uses its own id.
    // Owned skips rewarded continue and any interstitial. Price is the store's non-consumable tier
    // Flock Five uses (store-localized; the game never hardcodes a dollar amount).
    public static class Vip
    {
        public const string ProductId = "com.zfxgames.lotloop.vip";
        const string Pref = "lotloop.vip";

        public static bool Owned
        {
            get { return PlayerPrefs.GetInt(Pref, 0) == 1; }
        }

        public static void Grant()
        {
            PlayerPrefs.SetInt(Pref, 1);
            PlayerPrefs.Save();
        }
    }
}
