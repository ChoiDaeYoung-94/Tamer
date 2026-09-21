using TMPro;
using UnityEngine;

namespace AD
{
    public static class DeletionSettingsEntry
    {
        public static void Ensure(GameObject settings, PopupManager manager)
        {
            if (settings == null || settings.transform.Find("AccountPrivacyEntry") != null) return;
            var template = settings.GetComponentInChildren<TMP_Text>(true);
            var font = template != null ? template.font : null;
            var button = DeletionView.Button("AccountPrivacyEntry", settings.transform, "Account & privacy", font);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.15f, .5f);
            rect.anchorMax = new Vector2(.85f, .5f);
            rect.anchoredPosition = new Vector2(0, 260);
            rect.sizeDelta = new Vector2(0, 80);
            var panel = DeletionView.Rect("AccountPrivacy", settings.transform);
            panel.gameObject.SetActive(false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var view = panel.gameObject.AddComponent<DeletionView>();
            view.Build(font, () => manager.RequestClosePopup(panel.gameObject));
            var policy = DeletionView.Button("PrivacyPolicy", view.Message.transform.parent, "개인정보처리방침", font);
            policy.transform.SetSiblingIndex(view.Message.transform.GetSiblingIndex());
            policy.onClick.AddListener(() => Application.OpenURL("https://github.com/ChoiDaeYoung-94/Tamer#개인정보처리방침"));
            var age = DeletionView.Button("AgeChoice", view.Message.transform.parent, "연령대 다시 선택", font);
            age.transform.SetSiblingIndex(view.Message.transform.GetSiblingIndex() + 1);
            age.onClick.AddListener(() => manager.GetComponent<AgeChoicePresenter>()?.Open());
            var privacy = DeletionView.Button("AdPrivacyOptions", view.Message.transform.parent, "광고 개인정보 선택", font);
            privacy.transform.SetSiblingIndex(age.transform.GetSiblingIndex() + 1);
            privacy.onClick.AddListener(() => Managers.GoogleAdMobM?.ShowPrivacyOptions());
            panel.gameObject.AddComponent<AgePrivacyOptionsEntry>().Bind(age, privacy);
            panel.gameObject.AddComponent<DeletionPresenter>().Bind(view);
            panel.gameObject.AddComponent<PopupObject>();
            button.onClick.AddListener(() =>
            {
                panel.SetAsLastSibling();
                panel.gameObject.SetActive(true);
            });
        }
    }
}
