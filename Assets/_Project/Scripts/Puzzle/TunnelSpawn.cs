namespace CarMatch
{
    // Shared by taps and the magnet. Releasing the mouth car drops the
    // painted count; an empty queue removes the tunnel from the lane.
    public static class TunnelSpawn
    {
        public static bool TryRelease(Slot slot, out Car car)
        {
            car = default;
            if (slot == null || !slot.Tunnel || slot.Queue == null || slot.Queue.Count == 0)
                return false;
            car = slot.Queue[0];
            slot.Queue.RemoveAt(0);
            CloseIfEmpty(slot);
            return true;
        }

        public static void CloseIfEmpty(Slot slot)
        {
            if (slot == null || !slot.Tunnel) return;
            if (slot.Queue != null && slot.Queue.Count > 0) return;
            slot.Tunnel = false;
            slot.Empty = true;
            slot.Queue = null;
        }
    }
}
