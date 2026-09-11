using UnityEngine;

namespace AD
{
    /// <summary>
    /// PopupObject는 팝업 관리와 관련된 컴포넌트로, 
    /// 활성화 시 PopupManager와 상호작용하여 팝업 스택에 등록하거나 예외/Flow 상태를 설정
    /// </summary>
    public class PopupObject : MonoBehaviour
    {
        private enum CheckType
        {
            Normal,
            Exception, // Exception인 경우, 팝업이 사라질 때 Release 처리
            Flow       // Flow 예외인 경우, 상황에 맞게 Flow 종료 후 Release 처리
        }

        [SerializeField] private CheckType _checkType = CheckType.Normal;

        private PopupManager _manager;

        private void OnEnable()
        {
            _manager = Managers.Instance != null ? Managers.PopupM : null;
            if (_manager == null) return;
            switch (_checkType)
            {
                case CheckType.Normal:
                    _manager.EnablePop(gameObject);
                    break;
                case CheckType.Exception:
                    _manager.RegisterBlocker(gameObject, false);
                    break;
                case CheckType.Flow:
                    _manager.EnablePop(gameObject);
                    _manager.RegisterBlocker(gameObject, true);
                    break;
            }
        }

        /// <summary>
        /// Normal 타입일 경우 팝업을 비활성화 처리합니다.
        /// </summary>
        public void DisablePop()
        {
            if (_checkType == CheckType.Normal)
            {
                if (_manager != null)
                {
                    _manager.RequestClosePopup(gameObject);
                }
            }
        }

        private void OnDisable()
        {
            // Use the manager that accepted this registration, even during teardown.
            if (_manager != null) _manager.UnregisterPopup(gameObject);
            _manager = null;
        }
    }
}
