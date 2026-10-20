namespace CarMatch
{
    // Shared by the purse (saved coins and lives) and the tests.
    // Wins and fails pay the same. A life is spent only when a fail is accepted.
    public struct Wallet
    {
        public int Coins;
        public int Lives;
        public long NextLifeUnix;
    }

    public static class Economy
    {
        public const int WinPay = 20;
        public const int FailPay = 20;
        public const int UndoCost = 300;
        public const int ShuffleCost = 300;
        public const int SuperUndoCost = 900;
        public const int MagnetCost = 600;
        public const int MaxLives = 5;
        public const int LifeRegenSeconds = 20 * 60;
        public const int StartCoins = 400;

        public static int Pay(bool won) => won ? WinPay : FailPay;

        public static Wallet NewWallet()
        {
            return new Wallet
            {
                Coins = StartCoins,
                Lives = MaxLives,
                NextLifeUnix = 0
            };
        }

        public static bool CanAfford(Wallet wallet, int cost)
        {
            return cost >= 0 && wallet.Coins >= cost;
        }

        public static bool TrySpend(ref Wallet wallet, int cost)
        {
            if (!CanAfford(wallet, cost)) return false;
            wallet.Coins -= cost;
            return true;
        }

        public static void Grant(ref Wallet wallet, int coins)
        {
            if (coins > 0) wallet.Coins += coins;
        }

        public static bool TrySpendLife(ref Wallet wallet, long nowUnix)
        {
            Tick(ref wallet, nowUnix);
            if (wallet.Lives <= 0) return false;
            wallet.Lives -= 1;
            if (wallet.Lives < MaxLives && wallet.NextLifeUnix == 0)
                wallet.NextLifeUnix = nowUnix + LifeRegenSeconds;
            return true;
        }

        public static void Tick(ref Wallet wallet, long nowUnix)
        {
            if (wallet.Lives >= MaxLives)
            {
                wallet.Lives = MaxLives;
                wallet.NextLifeUnix = 0;
                return;
            }
            if (wallet.NextLifeUnix == 0)
            {
                wallet.NextLifeUnix = nowUnix + LifeRegenSeconds;
                return;
            }
            int guard = 0;
            while (wallet.Lives < MaxLives && nowUnix >= wallet.NextLifeUnix && guard < MaxLives + 2)
            {
                guard++;
                wallet.Lives += 1;
                if (wallet.Lives >= MaxLives)
                {
                    wallet.Lives = MaxLives;
                    wallet.NextLifeUnix = 0;
                    return;
                }
                wallet.NextLifeUnix += LifeRegenSeconds;
            }
        }

        public static int SecondsUntilLife(Wallet wallet, long nowUnix)
        {
            if (wallet.Lives >= MaxLives || wallet.NextLifeUnix == 0) return 0;
            long left = wallet.NextLifeUnix - nowUnix;
            if (left < 0) left = 0;
            if (left > int.MaxValue) return int.MaxValue;
            return (int)left;
        }
    }
}
