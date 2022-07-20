using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    public class PopupObject : MonoBehaviour
    {
        enum CheckType
        {
            Nomal,
            Exception, // Exception건뒤 사라질 때 Release
            Flow //Flow의 Exception걸고 경우는 상황에 맞게 Flow가 끝난 뒤 Release
        }

        [SerializeField, Tooltip("팝업 처리에 있어서 타입 지정")]
        CheckType _checkType = CheckType.Nomal;

        private void OnEnable()
        {
            switch (this._checkType)
            {
                case CheckType.Nomal:
                    AD.Managers.PopupM.EnablePop(gameObject);
                    break;
                case CheckType.Exception:
                    AD.Managers.PopupM.SetException();
                    break;
                case CheckType.Flow:
                    AD.Managers.PopupM.SetException();
                    break;
            }
        }

        public void DisablePop()
        {
            if (this._checkType == CheckType.Nomal)
                AD.Managers.PopupM.DisablePop(isEscape: false);
        }

        private void OnDisable()
        {
            if (this._checkType == CheckType.Exception)
                AD.Managers.PopupM.ReleaseException();
        }
    }
}
