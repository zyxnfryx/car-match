using System;
using System.Collections;

namespace CarMatch
{
    // The only ad seam. A real SDK can replace StubAdService without touching the fail flow.
    public interface IAdService
    {
        IEnumerator ShowRewarded(string placement, Action<bool> onDone);
    }
}
