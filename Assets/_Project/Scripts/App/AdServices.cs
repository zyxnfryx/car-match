namespace CarMatch
{
    // Shared helper: the only place that decides which IAdService the game gets.
    // Editor, desktop, or placeholder keys -> StubAdService. iOS/Android device with keys -> LevelPlayAdService.
    public static class AdServices
    {
        public static IAdService Create()
        {
            var cfg = AdConfig.Load();
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            if (cfg.HasKeys) return new LevelPlayAdService(cfg);
            UnityEngine.Debug.LogWarning("Lot Loop: LevelPlay keys are placeholders in Resources/AdConfig.asset. Using the stub ad.");
#endif
            return cfg.StubWhenUnavailable ? (IAdService)new StubAdService() : new StubAdService { WillGrant = false };
        }
    }
}
