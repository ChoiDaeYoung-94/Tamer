using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraManage : MonoBehaviour
{
    static CameraManage instance;
    public static CameraManage Instance { get { return instance; } }

    [Header("사용하는 시네머신 카메라 세팅")]
    [SerializeField] internal CinemachineVirtualCamera[] CM_cameras = null;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Player.cs 에서 플레이어 초기화 후 호출
    /// </summary>
    internal void StartInit()
    {
        foreach (CinemachineVirtualCamera cm in CM_cameras)
        {
            cm.Follow = Player.Instance._tr_cameraArm;
            cm.LookAt = Player.Instance._go_player.transform;
        }
    }
}
