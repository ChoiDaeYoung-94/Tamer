using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AD
{
    /// <summary>
    /// Game에서 호출되는 메서드 관리
    /// </summary>
    public class GameManager
    {
        [Header("--- 참고용 ---")]
        [SerializeField, Tooltip("첫 진입에만 로그인 체크 하기 위함")] internal bool _loginCheck = true;
        [SerializeField, Tooltip("현재 게임씬 여부")] private bool _isGame = false;
        internal bool IsGame { get { return _isGame; } }
        [Tooltip("Main scene - CM cam position")] Vector3 _vec_mainCmCam = new Vector3(0f, 36f, -10f);
        [Tooltip("Game scene - CM cam position")] Vector3 _vec_gameCmCam = new Vector3(40f, 36f, -30f);
        [Tooltip("Game scene - player position(main - vec3.zero)")] Vector3 _vec_player = new Vector3(40f, 0f, -20f);

        #region Functions
        /// <summary>
        /// Main scene -> Game scene, Game scene -> Main scene으로 전환 시 사용
        /// </summary>
        internal void SwitchMainOrGameScene(AD.Define.Scenes scene)
        {
            AD.Managers.PopupM.SetException();

            _isGame = !_isGame;

            JoyStick.Instance.transform.parent.gameObject.SetActive(false);
            PlayerUICanvas.Instance.gameObject.SetActive(false);

            Player.Instance.transform.parent.gameObject.SetActive(false);

            AD.Managers.SceneM.NextScene(scene);
        }

        /// <summary>
        /// Main or Game scene 진입 후 초기화
        /// </summary>
        internal void InitMainOrGameScene()
        {
            JoyStick.Instance.transform.parent.gameObject.SetActive(true);

            CameraManage.Instance.CM_cameras[0].transform.position =
                _isGame ? _vec_gameCmCam : _vec_mainCmCam;

            PlayerUICanvas.Instance.StartInit();
            PlayerUICanvas.Instance.gameObject.SetActive(true);

            Player.Instance._tr_cameraArm.transform.position = _isGame ? new Vector3(_vec_player.x, Player.Instance._tr_cameraArm.transform.position.y, _vec_player.z)
                : new Vector3(0f, Player.Instance._tr_cameraArm.transform.position.y, 0f);
            Player.Instance.transform.transform.position = _isGame ? _vec_player : Vector3.zero;
            Player.Instance.transform.transform.rotation = Quaternion.identity;
            Player.Instance.transform.parent.gameObject.SetActive(true);
        }
        #endregion
    }
}
