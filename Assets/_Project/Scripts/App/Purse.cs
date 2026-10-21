using UnityEngine;

namespace CarMatch
{
    // Persists the shared Economy wallet. Rules for pay, prices, and regen live in Economy.
    public static class Purse
    {
        const string PrefCoins = "lotloop.coins";
        const string PrefLives = "lotloop.lives";
        const string PrefNext = "lotloop.nextlife";
        const string PrefLevel = "lotloop.level";

        public static Wallet Wallet { get; private set; }
        public static int LevelIndex { get; private set; }

        // Keys were carmatch.* through build 3; copy them over once so testers keep progress.
        static void Migrate()
        {
            string[] keys = { "coins", "lives", "nextlife", "level" };
            for (int i = 0; i < keys.Length; i++)
            {
                string old = "carmatch." + keys[i];
                string now = "lotloop." + keys[i];
                if (!PlayerPrefs.HasKey(old) || PlayerPrefs.HasKey(now)) continue;
                if (keys[i] == "nextlife") PlayerPrefs.SetString(now, PlayerPrefs.GetString(old, "0"));
                else PlayerPrefs.SetInt(now, PlayerPrefs.GetInt(old, 0));
                PlayerPrefs.DeleteKey(old);
            }
        }

        public static void Boot()
        {
            Migrate();
            Wallet = new Wallet
            {
                Coins = PlayerPrefs.GetInt(PrefCoins, Economy.StartCoins),
                Lives = PlayerPrefs.GetInt(PrefLives, Economy.MaxLives),
                NextLifeUnix = ReadLong(PrefNext)
            };
            LevelIndex = PlayerPrefs.GetInt(PrefLevel, 0);
            if (LevelIndex < 0) LevelIndex = 0;
            Tick();
        }

        public static long Now()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static void Tick()
        {
            var wallet = Wallet;
            int before = wallet.Lives;
            long next = wallet.NextLifeUnix;
            Economy.Tick(ref wallet, Now());
            Wallet = wallet;
            if (wallet.Lives != before || wallet.NextLifeUnix != next)
                Save();
        }

        public static bool TrySpend(int cost)
        {
            var wallet = Wallet;
            if (!Economy.TrySpend(ref wallet, cost)) return false;
            Wallet = wallet;
            Save();
            return true;
        }

        public static void Grant(int coins)
        {
            var wallet = Wallet;
            Economy.Grant(ref wallet, coins);
            Wallet = wallet;
            Save();
        }

        public static bool TrySpendLife()
        {
            var wallet = Wallet;
            if (!Economy.TrySpendLife(ref wallet, Now())) return false;
            Wallet = wallet;
            Save();
            return true;
        }

        public static void SetLevel(int index)
        {
            if (index < 0) index = 0;
            LevelIndex = index;
            Save();
        }

        public static void Advance(int count)
        {
            if (count < 1) return;
            if (LevelIndex < count - 1) LevelIndex += 1;
            Save();
        }

        static void Save()
        {
            PlayerPrefs.SetInt(PrefCoins, Wallet.Coins);
            PlayerPrefs.SetInt(PrefLives, Wallet.Lives);
            PlayerPrefs.SetString(PrefNext, Wallet.NextLifeUnix.ToString());
            PlayerPrefs.SetInt(PrefLevel, LevelIndex);
            PlayerPrefs.Save();
        }

        static long ReadLong(string key)
        {
            long value;
            if (!long.TryParse(PlayerPrefs.GetString(key, "0"), out value)) return 0;
            return value;
        }
    }
}
