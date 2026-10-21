using System.Collections.Generic;

namespace CarMatch
{
    // One free use of each booster per player (Brandon, build 7). Stored as lotloop.free.<booster> = 1 once used.
    // A missing key means "still free", so existing players get their free uses too. After that, normal prices.
    public interface IFlagStore
    {
        bool Get(string key);
        void Set(string key);
    }

    public static class FreeBoosters
    {
        public static readonly string[] Names = { "Undo", "Shuffle", "Super", "Magnet" };

        public static string Key(string booster) { return "lotloop.free." + booster.ToLowerInvariant() + ".used"; }

        public static bool IsFree(IFlagStore store, string booster)
        {
            return store != null && !store.Get(Key(booster));
        }

        // Pays for one use: the free use first, else coins via trySpend. Returns false if neither works.
        public static bool Pay(IFlagStore store, string booster, int cost, System.Func<int, bool> trySpend)
        {
            if (IsFree(store, booster))
            {
                store.Set(Key(booster));
                return true;
            }
            return trySpend != null && trySpend(cost);
        }
    }

    public sealed class MemoryFlags : IFlagStore
    {
        readonly HashSet<string> _set = new HashSet<string>();
        public bool Get(string key) { return _set.Contains(key); }
        public void Set(string key) { _set.Add(key); }
    }
}
