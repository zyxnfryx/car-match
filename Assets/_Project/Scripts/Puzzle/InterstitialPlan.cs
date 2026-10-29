namespace CarMatch
{
    // When the Next button may show an interstitial. Empty unit ids and VIP never qualify.
    // The first eligible win is level 10. Every third win after that, and never within
    // 90 seconds of a rewarded ad or during the level-1 tutorial.
    public static class InterstitialPlan
    {
        public const int FirstLevel = 10;
        public const int EveryWins = 3;
        public const float RewardedCooldown = 90f;

        public static bool Due(int levelNumber, int winsSinceAd, bool vip, bool hasUnitId, bool tutorial, float secondsSinceRewarded)
        {
            if (vip || !hasUnitId || tutorial) return false;
            if (levelNumber < FirstLevel) return false;
            if (winsSinceAd < EveryWins || winsSinceAd % EveryWins != 0) return false;
            if (secondsSinceRewarded >= 0f && secondsSinceRewarded < RewardedCooldown) return false;
            return true;
        }
    }
}
