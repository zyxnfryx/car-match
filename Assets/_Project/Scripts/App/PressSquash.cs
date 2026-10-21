using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarMatch
{
    // 3D button press: sinks onto its baked lip and squashes on press, springs back with a little
    // overshoot on release. Port of Flock Five's DrawDiscPress sink, as a uGUI component.
    public sealed class PressSquash : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float Sink = 10f;
        Vector2 _rest;
        bool _restSet;
        float _press;
        float _target;
        float _vel;
        RectTransform _rt;
        Selectable _sel;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _sel = GetComponent<Selectable>();
        }

        void LateRest()
        {
            if (_restSet) return;
            _rest = _rt.anchoredPosition;
            _restSet = true;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_sel != null && !_sel.IsInteractable()) return;
            LateRest();
            _target = 1f;
        }

        public void OnPointerUp(PointerEventData e) { _target = 0f; }
        public void OnPointerExit(PointerEventData e) { _target = 0f; }

        void Update()
        {
            if (!_restSet) return;
            // Critically-underdamped spring for a springy release.
            float k = 900f, d = 26f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _vel += (k * (_target - _press) - d * _vel) * dt;
            _press += _vel * dt;
            float sx = 1f + 0.05f * _press;
            float sy = 1f - 0.08f * _press;
            var pulse = GetComponent<Pulse>();
            float extra = pulse != null ? pulse.Scale : 1f;
            _rt.localScale = new Vector3(sx * extra, sy * extra, 1f);
            _rt.anchoredPosition = _rest + new Vector2(0f, -Sink * _press);
        }
    }

    // Gentle breathing scale for the primary call to action (pass 3 drives it; default 1).
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.035f;
        public float Speed = 2.4f;
        public float Scale { get; private set; } = 1f;
        float _phase;

        void Update()
        {
            _phase += Time.unscaledDeltaTime * Speed;
            Scale = 1f + Amount * (0.5f + 0.5f * Mathf.Sin(_phase));
            if (GetComponent<PressSquash>() == null) transform.localScale = new Vector3(Scale, Scale, 1f);
        }
    }
}
