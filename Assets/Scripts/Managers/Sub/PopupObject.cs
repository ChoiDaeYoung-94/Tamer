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

        private void OnEnable()
        {
            switch (_checkType)
            {
                case CheckType.Normal:
                    AD.Managers.PopupM.EnablePop(gameObject);
                    break;
                case CheckType.Exception:
                    AD.Managers.PopupM.SetException();
                    break;
                case CheckType.Flow:
                    AD.Managers.PopupM.EnablePop(gameObject);
                    AD.Managers.PopupM.SetFlow();
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
                AD.Managers.PopupM.DisablePop();
            }
        }

        private void OnDisable()
        {
            if (_checkType == CheckType.Exception)
            {
                AD.Managers.PopupM.ReleaseException();
            }
            else if (_checkType == CheckType.Flow)
            {
                AD.Managers.PopupM.ReleaseFlow();
            }
        }
    }
}
