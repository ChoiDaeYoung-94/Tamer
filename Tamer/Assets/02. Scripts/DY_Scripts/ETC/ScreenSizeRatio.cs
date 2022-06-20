#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DY
{
    [ExecuteInEditMode]
    public class ScreenSizeRatio : MonoBehaviour
    {
        [Header("--- 세팅 ---")]
        [SerializeField, Tooltip("타이틀 부모 Canvas RectTransform")]
        RectTransform _RTR_canvas;
        [SerializeField, Tooltip("타이틀 Image RectTransform")]
        RectTransform _RTR_title;
        [SerializeField, Tooltip("타이틀 Image original 해상도 X값")]
        float _width = 0;
        [SerializeField, Tooltip("타이틀 Image original 해상도 Y값")]
        float _height = 0;

        [Header("--- 적용 여부 ---")]
        [SerializeField, Tooltip("Resize 적용 여부")]
        bool isOn = false;

        private void Update()
        {
            if (isOn)
                SetSize();
        }

        void SetSize()
        {
            float ratio = 0;

            if (_RTR_canvas.sizeDelta.y - _height > _RTR_canvas.sizeDelta.x - _width)
            {
                ratio = _RTR_canvas.sizeDelta.y / _height;

                _RTR_title.sizeDelta = new Vector2(_width * ratio, _RTR_canvas.sizeDelta.y);
            }
            else if (_RTR_canvas.sizeDelta.y - _height < _RTR_canvas.sizeDelta.x - _width)
            {
                ratio = _RTR_canvas.sizeDelta.x / _width;

                _RTR_title.sizeDelta = new Vector2(_RTR_canvas.sizeDelta.x, _height * ratio);
            }
            else
            {
                _RTR_title.sizeDelta = new Vector2(_RTR_canvas.sizeDelta.x, _RTR_canvas.sizeDelta.y);
            }
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(ScreenSizeRatio))]
        public class customEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.HelpBox("기기 해상도에 따른 타이틀 사이즈 대응\n" +
                    "* blur 이미지의 가로, 세로의 비율과 부모 Canvas의 비율을 비교하여 한쪽 비율로 맞추기 위함", MessageType.Info);

                base.OnInspectorGUI();
            }
        }
#endif
    }
}