using UnityEngine;
using UnityEngine.UI;

namespace CarMatch
{
    // Layouts are authored on a 1080x1920 canvas. On phones (taller than 9:16) the scaler keeps
    // its 0.5 blend as before. On wider screens such as iPad (4:3) it matches height instead, so
    // the full 1920 units of height stay visible and nothing at the top or bottom is cut off.
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AspectFit : MonoBehaviour
    {
        public CanvasScaler Scaler;
        public const float DesignAspect = 1080f / 1920f;

        public static float MatchFor(float width, float height)
        {
            if (height <= 0f) return 0.5f;
            return width / height > DesignAspect + 0.001f ? 1f : 0.5f;
        }

        void Awake() { Apply(); }
        void Update() { Apply(); }

        void Apply()
        {
            if (Scaler == null) Scaler = GetComponent<CanvasScaler>();
            float m = MatchFor(Screen.width, Screen.height);
            if (!Mathf.Approximately(Scaler.matchWidthOrHeight, m)) Scaler.matchWidthOrHeight = m;
        }

        // Canvas units of the bottom safe-area inset (home indicator) for a screen of 'canvasHeight' units.
        public static float BottomInset(float canvasHeight)
        {
            if (Screen.height <= 0) return 0f;
            return Screen.safeArea.yMin / Screen.height * canvasHeight;
        }
    }
}
