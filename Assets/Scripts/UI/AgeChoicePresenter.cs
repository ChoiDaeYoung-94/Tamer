using AD.Advertising;
using TMPro;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace AD
{
    /// <summary>Uses the existing settings font and owns only its modal and input lease.</summary>
    public sealed class AgeChoicePresenter : MonoBehaviour
    {
        private GoogleAdMobManager _ads;
        private PopupManager _popups;
        private GameObject _modal;
        private TMP_Text _message;
        private float _previousTimeScale;
        private int _scene;
        private bool _open;

        public void Bind(GameObject settings, PopupManager popups, GoogleAdMobManager ads)
        {
            _popups = popups;
            _ads = ads;
            if (_ads == null || settings == null) return;
            var template = settings.GetComponentInChildren<TMP_Text>(true);
            var font = template != null ? template.font : null;
            var root = DeletionView.Rect("AgeChoiceCanvas", transform);
            _modal = root.gameObject;
            _modal.SetActive(false);
            var canvas = _modal.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _modal.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            _modal.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var background = _modal.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.055f, .065f, .085f, 1);
            var content = DeletionView.Rect("Content", root);
            content.anchorMin = new Vector2(.08f, .12f);
            content.anchorMax = new Vector2(.92f, .88f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Label(content, "연령대 확인", font, 52, 100);
            _message = Label(content, "연령에 맞는 광고 및 개인정보 설정을 위해 연령대를 선택해 주세요.\n연령 구간만 이 기기에 저장합니다. 생년월일은 저장하지 않습니다.\n설정에서 언제든 변경할 수 있습니다.", font, 34, 320);
            Choice(content, font, "13세 미만", AgeChoice.Under13);
            Choice(content, font, "13~15세", AgeChoice.From13To15);
            Choice(content, font, "16~17세", AgeChoice.From16To17);
            Choice(content, font, "18세 이상", AgeChoice.Adult);
            Choice(content, font, "응답하지 않음", AgeChoice.Declined);
        }

        private void Choice(Transform parent, TMP_FontAsset font, string caption, AgeChoice choice)
        {
            var button = DeletionView.Button(choice.ToString(), parent, caption, font);
            button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 110;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                if (_ads.AgeSelection.Select(choice)) Close();
                else _message.text = "기기에 저장하지 못했습니다. 다시 선택해 주세요. 광고는 시작하지 않습니다.";
            });
        }

        private static TMP_Text Label(Transform parent, string value, TMP_FontAsset font, int size, float height)
        {
            var text = DeletionView.Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.richText = false;
            var layout = text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = height;
            return text;
        }

        private void Update()
        {
            if (_ads == null || _modal == null) return;
            var scene = UnitySceneManager.GetActiveScene();
            if (_open && scene.handle != _scene) Close();
            if (!_open && (scene.name == "Main" || scene.name == "Game") &&
                Player.Instance != null && Managers.SceneM != null && !Managers.SceneM.IsTransitioning &&
                _ads.AgeSelection.NeedsQuestion) Open();
        }

        public void Open()
        {
            if (_open || _ads == null || _modal == null || !_ads.CanChangeAge) return;
            _ads.AgeSelection.BeginEdit();
            _previousTimeScale = Time.timeScale;
            _scene = UnitySceneManager.GetActiveScene().handle;
            _open = true;
            _popups.RegisterBlocker(_modal, true);
            Time.timeScale = 0;
            _modal.SetActive(true);
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            if (_popups != null) _popups.UnregisterPopup(_modal);
            if (_modal != null) _modal.SetActive(false);
            if (UnitySceneManager.GetActiveScene().handle == _scene) Time.timeScale = _previousTimeScale;
        }

        private void OnDisable() => Close();
    }
}
