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
