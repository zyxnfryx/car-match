using System;
using System.Collections;
using UnityEngine;

namespace CarMatch
{
    // No ad SDK in v1. The stub waits a beat, then grants the reward.
    public sealed class StubAdService : IAdService
    {
        public bool WillGrant = true;

        public IEnumerator ShowRewarded(string placement, Action<bool> onDone)
        {
            yield return new WaitForSecondsRealtime(0.45f);
            if (onDone != null) onDone(WillGrant);
        }
    }
}
