using UnityEngine;
using AD.Advertising;

namespace AD
{
    public sealed class AgePrivacyOptionsEntry : MonoBehaviour
    {
        private UnityEngine.UI.Button _age, _privacy;
        private TMPro.TMP_Text _status;
        private AgeChoicePresenter _agePresenter;
        public void Bind(UnityEngine.UI.Button age, UnityEngine.UI.Button privacy,
            TMPro.TMP_Text status = null, AgeChoicePresenter agePresenter = null)
        {
            if (_privacy != null) _privacy.onClick.RemoveListener(CheckPrivacy);
            _age = age; _privacy = privacy; _status = status; _agePresenter = agePresenter;
            if (_privacy != null) _privacy.onClick.AddListener(CheckPrivacy);
            Refresh();
        }
        private void OnDestroy()
        {
            if (_privacy != null) _privacy.onClick.RemoveListener(CheckPrivacy);
        }
        private void CheckPrivacy()
        {
            var ads = Managers.GoogleAdMobM;
            if (ads == null) return;
            ads.RequestPrivacySettings();
            if (ads.PrivacySettingsResult == AdPrivacyResult.AgeRequired) _agePresenter?.Open();
            // Selecting an age never resumes this request automatically.
            Refresh();
        }
        private void OnEnable() => Refresh();
        private void Update() => Refresh();
        private void Refresh()
        {
            if (_age == null || _privacy == null) return;
            var ads = Managers.GoogleAdMobM;
            _age.interactable = ads != null && ads.CanChangeAge;
            _privacy.gameObject.SetActive(true);
            _privacy.interactable = ads != null && ads.CanCheckPrivacySettings;
            if (_status != null) _status.text = StatusText(ads != null
                ? ads.PrivacySettingsResult : AdPrivacyResult.Unavailable);
        }

        private static string StatusText(AdPrivacyResult result)
        {
            switch (result)
            {
                case AdPrivacyResult.Checking: return "개인정보 설정을 확인하고 있습니다.";
                case AdPrivacyResult.AgeRequired: return "먼저 연령대를 선택해 주세요. 선택 후 다시 확인할 수 있습니다.";
                case AdPrivacyResult.Unavailable: return "현재 개인정보 설정 확인 서비스를 사용할 수 없습니다. 개인정보처리방침을 확인해 주세요.";
                case AdPrivacyResult.Busy: return "진행 중인 작업이 끝난 후 다시 확인해 주세요.";
                case AdPrivacyResult.NotRequired: return "현재 별도 개인정보 선택 창이 제공되지 않습니다. 이전 동의가 철회됐다는 뜻은 아닙니다.";
                case AdPrivacyResult.OptionsClosed: return "개인정보 선택 창을 닫았습니다. 선택 내용을 변경하려면 다시 확인해 주세요.";
                case AdPrivacyResult.Cancelled: return "연령 선택이 변경되어 확인을 중단했습니다. 다시 확인해 주세요.";
                case AdPrivacyResult.UpdateFailed:
                case AdPrivacyResult.Unknown: return "개인정보 설정 상태를 확인하지 못했습니다. 다시 확인해 주세요.";
                case AdPrivacyResult.FormFailed: return "개인정보 선택 창을 열지 못했습니다. 다시 확인해 주세요.";
                default: return "개인정보 설정 상태를 직접 확인할 수 있습니다.";
            }
        }
    }
}
