using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프레임 설정
/// </summary>
public class SetFPS : MonoBehaviour
{
    [SerializeField, Tooltip("프레임 설정")]
    int _fps = 60;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = this._fps;
    }
}
