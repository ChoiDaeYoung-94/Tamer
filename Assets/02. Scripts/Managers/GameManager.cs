using UnityEngine;

namespace AD
{
    /// <summary>
    /// Game에서 호출되는 메서드 관리
    /// </summary>
    public class GameManager
    {
        // 로그인 체크 여부 (첫 진입 시)
        public bool LoginCheck = true;

        // 현재 씬 상태 (Main / Game)
        private AD.GameConstants.Scene _currentScene = AD.GameConstants.Scene.Main;
        public bool IsGame => _currentScene == AD.GameConstants.Scene.Game;

        // 카메라 및 플레이어 위치 벡터
        private readonly Vector3 _vecMainCmCam = new(0f, 36f, -10f);
        private readonly Vector3 _vecGameCmCam = new(40f, 36f, -30f);
        private readonly Vector3 _vecPlayer = new(40f, 0f, -20f);

        #region Functions

        /// <summary>
        /// Main Scene ↔ Game Scene 전환
        /// </summary>
        public void SwitchMainOrGameScene()
        {
            AD.Managers.PopupM.SetException();

            // 씬 전환
            _currentScene = _currentScene == AD.GameConstants.Scene.Main
                ? AD.GameConstants.Scene.Game
                : AD.GameConstants.Scene.Main;

            // UI 및 플레이어 상태 업데이트
            SetSceneUIActive(false);
            var player = Player.Instance;
            player.ActiveControl(false);
            player.transform.parent.gameObject.SetActive(false);

            // 씬 변경 실행
            AD.Managers.SceneM.NextScene(_currentScene);
        }

        /// <summary>
        /// Main Scene 또는 Game Scene 진입 후 초기화
        /// </summary>
        public void InitMainOrGameScene()
        {
            var player = Player.Instance;
            var cameraArm = player.CameraArm;

            CameraManage.Instance.CinemachineCameras[0].transform.position = IsGame ? _vecGameCmCam : _vecMainCmCam;

            PlayerUICanvas.Instance.StartInit();
            SetSceneUIActive(true);

            cameraArm.transform.position = IsGame ? new Vector3(_vecPlayer.x, cameraArm.transform.position.y, _vecPlayer.z)
                                                  : new Vector3(0f, cameraArm.transform.position.y, 0f);
            player.transform.position = IsGame ? _vecPlayer : Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            player.transform.parent.gameObject.SetActive(true);
            player.ActiveControl(true);
            player.HandleAttackCoroutine(IsGame);

            if (!IsGame)
                player.Heal();
        }

        /// <summary>
        /// UI 상태 활성화/비활성화
        /// </summary>
        private void SetSceneUIActive(bool isActive)
        {
            JoyStick.Instance.transform.parent.gameObject.SetActive(isActive);
            PlayerUICanvas.Instance.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// 게임 오버 처리
        /// </summary>
        public void GameOver()
        {
            Player.Instance.RemoveAllAllyMonster();
            AD.Managers.PopupM.PopupGameOver();
        }

        /// <summary>
        /// 게임 오버 후 로비로 이동
        /// </summary>
        public void GameOverGoLobby()
        {
            Player.Instance.ReSetPlayer();
            SwitchMainOrGameScene();
        }

        #endregion
    }
}
