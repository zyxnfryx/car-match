using System;
using System.Collections;

namespace CarMatch
{
    // The only ad seam. A real SDK can replace StubAdService without touching the fail flow.
    public interface IAdService
    {
        IEnumerator ShowRewarded(string placement, Action<bool> onDone);
        // False until a rewarded ad is actually loaded. The lose-popup continue button greys out.
        bool RewardedReady { get; }
    }
}
