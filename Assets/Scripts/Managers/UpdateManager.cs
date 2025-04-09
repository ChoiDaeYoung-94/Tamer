using System;

using UnityEngine;

using UniRx;

namespace AD
{
    /// <summary>
    /// 애매한 Update() 처리 관리
    /// </summary>
    public class UpdateManager : MonoBehaviour
    {
        public event Action OnUpdateEvent;

        private void Awake()
        {
            // UniRx를 활용한 업데이트 이벤트 관리
            Observable.EveryUpdate()
                .Subscribe(_ => OnUpdateEvent?.Invoke())
                .AddTo(this);
        }
    }
}
