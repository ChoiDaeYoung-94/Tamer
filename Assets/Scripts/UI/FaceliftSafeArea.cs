using System;
using UnityEngine;

namespace AD
{
    /// <summary>Keeps existing HUD/character controls inside the device's usable screen.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FaceliftSafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastSize = new Vector2Int(-1, -1);

        public static Rect Normalize(Rect safe, float width, float height)
        {
            bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            if (!Finite(width) || !Finite(height) || width <= 0 || height <= 0 ||
                !Finite(safe.x) || !Finite(safe.y) || !Finite(safe.width) || !Finite(safe.height) ||
                safe.width <= 0 || safe.height <= 0) return new Rect(0, 0, 1, 1);
            float minX = Mathf.Clamp01(safe.xMin / width), minY = Mathf.Clamp01(safe.yMin / height);
            float maxX = Mathf.Clamp01(safe.xMax / width), maxY = Mathf.Clamp01(safe.yMax / height);
            return maxX > minX && maxY > minY ? Rect.MinMaxRect(minX, minY, maxX, maxY) : new Rect(0, 0, 1, 1);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _rect = (RectTransform)transform;
            var canvas = transform.parent != null ? transform.parent.GetComponent<Canvas>() : null;
            // The normalization applies to full-screen overlay Canvases only.
            // Do not reinterpret a world-space or nested panel's coordinates.
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.targetDisplay != 0)
            {
                enabled = false;
                return;
            }
            _lastSize = new Vector2Int(-1, -1);
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (_rect == null || (size == _lastSize && safe == _lastSafeArea)) return;
            Rect normalized = Normalize(safe, size.x, size.y);
            _rect.anchorMin = normalized.min;
            _rect.anchorMax = normalized.max;
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
            _lastSize = size;
            _lastSafeArea = safe;
#if DEVELOPMENT_BUILD
            Debug.Log("FACELIFT_SAFE_AREA " + gameObject.name + " screen=" + size + " anchors=" + normalized);
#endif
        }
    }
}
