using UnityEngine;

public class MiniMap : MonoBehaviour
{
    private static MiniMap _instance;
    public static MiniMap Instance { get { return _instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] private Sprite[] _playerIcons;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private SpriteRenderer _playerIconRenderer;
    [SerializeField] private GameObject _mainCamera;
    [SerializeField] private GameObject _minimapCamera;
    [SerializeField] private GameObject _minimapCanvas;

    private Vector3 _touchStartPos;
    private Vector3 _dragCurrentPos;
    private Vector3 _offsetPos;
    private Vector3 _targetPos;
    private Vector3 _originalCameraPos;

    private void Awake()
    {
        _instance = this;
    }

    private void OnDisable()
    {
        if (AD.Managers.Instance)
        {
            AD.Managers.UpdateM.OnUpdateEvent -= SetPlayerIcon;
            AD.Managers.UpdateM.OnUpdateEvent -= MiniMapDrag;
        }
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    /// <summary>
    /// InitializeGame.cs에서 호출
    /// </summary>
    public void StartInit()
    {
        Settings();
    }

    private void Settings()
    {
        _playerIconRenderer.sprite = AD.Managers.DataM.LocalPlayerData["Sex"] == "Man" ? _playerIcons[0] : _playerIcons[1];

        AD.Managers.UpdateM.OnUpdateEvent -= SetPlayerIcon;
        AD.Managers.UpdateM.OnUpdateEvent += SetPlayerIcon;
    }

    private void SetPlayerIcon()
    {
        _playerTransform.localPosition = new Vector3(Player.Instance.transform.position.x, 1f, Player.Instance.transform.position.z);
    }

    public void OpenMap()
    {
        Time.timeScale = 0;

        _minimapCamera.transform.localPosition = new Vector3(_playerTransform.localPosition.x, 31f, _playerTransform.localPosition.z);
        _minimapCamera.SetActive(true);

        ToggleGameUI(false);
        _minimapCanvas.SetActive(true);
        _mainCamera.SetActive(false);

        AD.Managers.UpdateM.OnUpdateEvent -= MiniMapDrag;
        AD.Managers.UpdateM.OnUpdateEvent += MiniMapDrag;
    }

    public void CloseMap()
    {
        Time.timeScale = 1;

        AD.Managers.UpdateM.OnUpdateEvent -= MiniMapDrag;

        ToggleGameUI(true);
        _mainCamera.SetActive(true);
        _minimapCamera.SetActive(false);
    }

    private void ToggleGameUI(bool isActive)
    {
        JoyStick.Instance.transform.parent.gameObject.SetActive(isActive);
        PlayerUICanvas.Instance.gameObject.SetActive(isActive);
    }

    private void MiniMapDrag()
    {
        if (Input.touchCount == 0) return;
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _touchStartPos = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 0f));
                    _originalCameraPos = _minimapCamera.transform.localPosition;
                    break;

                case TouchPhase.Moved:
                    _dragCurrentPos = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 0f));

                    _offsetPos = _touchStartPos - _dragCurrentPos;

                    float x = _originalCameraPos.x + _offsetPos.x;
                    float z = _originalCameraPos.z + _offsetPos.z;

                    x = Mathf.Clamp(x, -70f, 70f);
                    z = Mathf.Clamp(z, -18f, 18f);

                    _targetPos = new Vector3(x, _originalCameraPos.y, z);

                    _minimapCamera.transform.localPosition = Vector3.Lerp(_minimapCamera.transform.localPosition, _targetPos, 0.7f);
                    break;

                case TouchPhase.Ended:
                    break;
            }
        }
    }
}
