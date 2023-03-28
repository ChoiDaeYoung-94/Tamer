using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraManage : MonoBehaviour
{
    [Header("사용하는 시네머신 카메라 세팅")]
    [SerializeField] CinemachineVirtualCamera[] CM_cameras = null;

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    public void StartInit()
    {
        foreach (CinemachineVirtualCamera cm in CM_cameras)
        {
            cm.Follow = Player.Instance._tr_cameraArm;
            cm.LookAt = Player.Instance._go_player.transform;
        }
    }
}
