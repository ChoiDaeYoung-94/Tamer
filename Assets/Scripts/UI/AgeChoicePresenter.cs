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
        private TMP_InputField _ageInput;
        private const string Instructions = "연령에 맞는 광고 및 개인정보 설정을 위해 나이를 입력해 주세요.\n연령 구간만 이 기기에 저장합니다. 입력한 나이와 생년월일은 저장하지 않습니다.\n설정에서 언제든 변경할 수 있습니다.";
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
            // PopupManager lives on the existing root Canvas. A nested Canvas does
            // not resize itself to the screen, so stretch its RectTransform explicitly.
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            _modal = root.gameObject;
            _modal.SetActive(false);
            var canvas = _modal.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _modal.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            _modal.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var background = _modal.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(18, 39, 36, 255);
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
            _message = Label(content, Instructions, font, 34, 320);
            BuildAgeInput(content, font);
            var submit = ActionButton(content, font, "SubmitAge", "확인");
            submit.onClick.AddListener(SubmitAge);
            var decline = ActionButton(content, font, "Declined", "응답하지 않음");
            decline.onClick.AddListener(() => Select(AgeChoice.Declined));
        }

        private static UnityEngine.UI.Button ActionButton(Transform parent, TMP_FontAsset font,
            string name, string caption)
        {
            var button = DeletionView.Button(name, parent, caption, font);
            button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 110;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            return button;
        }

        private void BuildAgeInput(Transform parent, TMP_FontAsset font)
        {
            var rect = DeletionView.Rect("AgeInput", parent);
            rect.gameObject.SetActive(false);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(44, 77, 65, 255);
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = size.preferredHeight = 110;
            var viewport = DeletionView.Rect("Text Area", rect);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24, 8);
            viewport.offsetMax = new Vector2(-24, -8);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = DeletionView.Label("Text", viewport, "", font, 36, 0);
            var placeholder = DeletionView.Label("Placeholder", viewport, "나이 (만 나이)", font, 32, 0);
            foreach (var label in new[] { text, placeholder })
            {
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            _ageInput = rect.gameObject.AddComponent<TMP_InputField>();
            _ageInput.textViewport = viewport;
            _ageInput.textComponent = text;
            _ageInput.placeholder = placeholder;
            _ageInput.targetGraphic = background;
            _ageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _ageInput.characterValidation = TMP_InputField.CharacterValidation.Digit;
            _ageInput.keyboardType = TouchScreenKeyboardType.NumberPad;
            // Validate the complete entry; truncating a pasted birth year could turn it into an adult age.
            _ageInput.characterLimit = 0;
            _ageInput.lineType = TMP_InputField.LineType.SingleLine;
            _ageInput.richText = false;
            _ageInput.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            _ageInput.onSubmit.AddListener(_ => SubmitAge());
            _ageInput.SetTextWithoutNotify("");
            rect.gameObject.SetActive(true);
        }

        private void SubmitAge()
        {
            if (!LocalAgeChoice.TryParseAgeInput(_ageInput.text, out var choice))
            {
                _message.text = "나이를 숫자로 입력해 주세요.\n입력하지 않고도 ‘응답하지 않음’으로 계속할 수 있습니다.";
                return;
            }
            Select(choice);
        }

        private void Select(AgeChoice choice)
        {
            if (_ads.AgeSelection.Select(choice)) Close();
            else
            {
                ClearInput();
                _message.text = "기기에 저장하지 못했습니다. 다시 입력해 주세요. 광고는 시작하지 않습니다.";
            }
        }

        private void ClearInput()
        {
            if (_ageInput == null) return;
            _ageInput.DeactivateInputField();
            _ageInput.SetTextWithoutNotify("");
        }

        private static TMP_Text Label(Transform parent, string value, TMP_FontAsset font, int size, float height)
        {
            var text = DeletionView.Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = new Color32(246, 241, 221, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            var layout = text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            // Let the parent assign the available width instead of the TMP
            // preferred unwrapped line length expanding the label beyond it.
            layout.minWidth = layout.preferredWidth = 0;
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
            ClearInput();
            _message.text = Instructions;
            _previousTimeScale = Time.timeScale;
            _scene = UnitySceneManager.GetActiveScene().handle;
            _open = true;
            _popups.RegisterBlocker(_modal, true);
            Time.timeScale = 0;
            _modal.SetActive(true);
            // Apply sorting after the nested Canvas has joined its active parent.
            var canvas = _modal.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            ClearInput();
            if (_popups != null) _popups.UnregisterPopup(_modal);
            if (_modal != null) _modal.SetActive(false);
            if (UnitySceneManager.GetActiveScene().handle == _scene) Time.timeScale = _previousTimeScale;
        }

        private void OnDisable() => Close();
    }
}
