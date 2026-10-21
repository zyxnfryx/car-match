using UnityEngine;

namespace CarMatch
{
    // Keeps a full-screen RectTransform inside Screen.safeArea (notch, Dynamic Island, home bar).
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _size;

        void OnEnable() { Apply(); }
        void Update() { Apply(); }

        void Apply()
        {
            var safe = Screen.safeArea;
            if (safe == _applied && _size.x == Screen.width && _size.y == Screen.height) return;
            _applied = safe;
            _size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
