using System;
using TMPro;
using UnityEngine;

namespace AD
{
    /// <summary>First-login presentation only; callbacks own all account decisions.</summary>
    public sealed class InitialAccountChoiceView : MonoBehaviour
    {
        public UnityEngine.UI.Button GoogleButton { get; private set; }
        public UnityEngine.UI.Button GuestButton { get; private set; }
        private TMP_Text _status;

        public void Build(TMP_FontAsset font, Action google, Action guest)
        {
            if (GoogleButton != null) return;
            gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(18, 39, 36, 255);
            var content = DeletionView.Rect("Content", transform);
            content.anchorMin = new Vector2(.08f, .12f);
            content.anchorMax = new Vector2(.92f, .88f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Label("Title", "게임 시작", font, 48, 100, content);
            _status = DeletionView.Label("Status", content, "", font, 30, 130);
            _status.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 0;
            _status.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 0;
            _status.gameObject.SetActive(false);
            Label("Message", "Google 없이도 게임을 이용할 수 있습니다.\n"
                + "손님 선택 시 서버 저장용 게임 계정이 생성됩니다.\n"
                + "앱 삭제·기기 변경 뒤에는 복구가 제한될 수 있습니다.\n\n"
                + "기존 Google 계정은 ‘Google로 계속’을 선택하세요.", font, 32, 450, content);
            GoogleButton = Button("ContinueGoogle", "Google로 계속", font, content, google);
            GuestButton = Button("StartGuest", "Google 없이 시작", font, content, guest);
        }

        public void SetChoices(Action google, Action guest)
        {
            GoogleButton.onClick.RemoveAllListeners();
            GuestButton.onClick.RemoveAllListeners();
            GoogleButton.onClick.AddListener(() => google?.Invoke());
            GuestButton.onClick.AddListener(() => guest?.Invoke());
        }

        public void SetStatus(string message)
        {
            _status.text = message ?? "";
            _status.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void Label(string name, string text, TMP_FontAsset font, int size, int height, Transform parent)
        {
            var label = DeletionView.Label(name, parent, text, font, size, height);
            var element = label.GetComponent<UnityEngine.UI.LayoutElement>();
            element.minWidth = element.preferredWidth = 0;
        }

        private static UnityEngine.UI.Button Button(string name, string text, TMP_FontAsset font,
            Transform parent, Action action)
        {
            var button = DeletionView.Button(name, parent, text, font);
            var size = button.GetComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = size.preferredHeight = 110;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }
    }
}
