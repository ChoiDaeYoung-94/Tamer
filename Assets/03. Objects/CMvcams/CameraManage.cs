using UnityEngine;

using Cinemachine;

public class CameraManage : MonoBehaviour
{
    private static CameraManage _instance;
    public static CameraManage Instance { get { return _instance; } }

    [Header("사용하는 시네머신 카메라 세팅")]
    public CinemachineVirtualCamera[] CinemachineCameras;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Player.cs 에서 플레이어 초기화 후 호출
    /// </summary>
    public void StartInit()
    {
        foreach (CinemachineVirtualCamera cm in CinemachineCameras)
        {
            cm.Follow = Player.Instance.CameraArm;
            cm.LookAt = Player.Instance.PlayerObject.transform;
        }
    }
}
