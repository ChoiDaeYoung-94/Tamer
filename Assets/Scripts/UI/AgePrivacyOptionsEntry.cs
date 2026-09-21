using UnityEngine;

namespace AD
{
    public sealed class AgePrivacyOptionsEntry : MonoBehaviour
    {
        private UnityEngine.UI.Button _age, _privacy;
        public void Bind(UnityEngine.UI.Button age, UnityEngine.UI.Button privacy)
        { _age = age; _privacy = privacy; Refresh(); }
        private void OnEnable() => Refresh();
        private void Update() => Refresh();
        private void Refresh()
        {
            if (_age == null || _privacy == null) return;
            var ads = Managers.GoogleAdMobM;
            _age.interactable = ads != null && ads.CanChangeAge;
            _privacy.gameObject.SetActive(ads != null && ads.PrivacyOptionsRequired);
            _privacy.interactable = ads != null && ads.CanChangeAge;
        }
    }
}
