using System;
using TMPro;
using UnityEngine;

namespace AD
{
    /// <summary>Runtime view owned by the existing settings popup; no account operations.</summary>
    public sealed class DeletionView : MonoBehaviour
    {
        public TMP_Text Message { get; private set; }
        public UnityEngine.UI.Button RequestButton { get; private set; }
        public UnityEngine.UI.Button ConfirmButton { get; private set; }
        public UnityEngine.UI.Button SessionConfirmButton { get; private set; }
        public UnityEngine.UI.Button RefreshButton { get; private set; }
        public UnityEngine.UI.Button CancelButton { get; private set; }
        public UnityEngine.UI.Button RetryButton { get; private set; }
        public UnityEngine.UI.Button ReauthenticateButton { get; private set; }
        public UnityEngine.UI.Button CloseButton { get; private set; }
        public UnityEngine.UI.Button ContactButton { get; private set; }
        public bool ExitOnClose { get; set; }

        public void Build(TMP_FontAsset font, Action close)
        {
            var background = gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(18, 39, 36, 255);
            var content = Rect("Content", transform);
            content.anchorMin = new Vector2(.1f, .08f);
            content.anchorMax = new Vector2(.9f, .92f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Label("Title", content, "계정 및 개인정보", font, 42, 70);
            Message = Label("Status", content, "", font, 30, 210);
            var space = Message.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
            space.flexibleHeight = 1;
            RequestButton = Button("Request", content, "계정 삭제 요청", font);
            ConfirmButton = Button("Confirm", content, "삭제 요청 확정", font);
            SessionConfirmButton = Button("SessionConfirm", content, "현재 게임 계정 확인", font);
            RefreshButton = Button("Refresh", content, "요청 상태 확인", font);
            CancelButton = Button("CancelRequest", content, "삭제 요청 취소", font);
            RetryButton = Button("Retry", content, "다시 시도", font);
            ReauthenticateButton = Button("Reauthenticate", content, "현재 세션 다시 확인", font);
            ContactButton = Button("ContactDeletionSupport", content, "계정 삭제 문의", font);
            ContactButton.onClick.AddListener(() => Application.OpenURL("mailto:" + CloudScriptDeletionClient.SupportEmail + "?subject=Monster%20Tamer%20account%20deletion"));
            CloseButton = Button("Close", content, "설정으로 돌아가기", font);
            CloseButton.onClick.AddListener(() =>
            {
                if (ExitOnClose) Application.Quit();
                else close();
            });
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        internal static UnityEngine.UI.Button Button(string name, Transform parent, string caption, TMP_FontAsset font)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color32(44, 77, 65, 255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = 64;
            size.preferredHeight = 76;
            var label = Label("Label", rect, caption, font, 28, 0);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 4);
            label.rectTransform.offsetMax = new Vector2(-12, -4);
            return button;
        }

        internal static TMP_Text Label(string name, Transform parent, string caption, TMP_FontAsset font, int size, float height)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = caption;
            label.fontSize = size;
            label.color = new Color32(246, 241, 221, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (height > 0)
            {
                var element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minHeight = height;
                element.preferredHeight = height;
            }
            return label;
        }
    }
}
