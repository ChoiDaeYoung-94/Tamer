using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniMap : MonoBehaviour
{
    static MiniMap instance;
    public static MiniMap Instance { get { return instance; } }

    [Header("--- 세팅 ---")]
    [SerializeField] private Sprite[] _spr_player = null;
    [SerializeField] private Transform _tr_player = null;
    [SerializeField] private SpriteRenderer _sprR_player = null;
    [SerializeField] GameObject _go_mainCamera = null;
    [SerializeField] GameObject _go_minimapCamera = null;
    [SerializeField] GameObject _go_minimapCanvas = null;
    private Vector3 touchPosition = Vector3.zero;
    private Vector3 dragPosition = Vector3.zero;
    private Vector3 offsetPosition = Vector3.zero;
    private Vector3 targetPosition = Vector3.zero;
    private Vector3 orgCameraPosition = Vector3.zero;

    private void Awake()
    {
        instance = this;
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
        instance = null;
    }

    /// <summary>
    /// InitializeGame.cs에서 호출
    /// </summary>
    internal void StartInit()
    {
        Settings();
    }

    private void Settings()
    {
        _sprR_player.sprite = AD.Managers.DataM._dic_player["Sex"] == "Man" ? _spr_player[0] : _spr_player[1];

        AD.Managers.UpdateM.OnUpdateEvent -= SetPlayerIcon;
        AD.Managers.UpdateM.OnUpdateEvent += SetPlayerIcon;
    }

    private void SetPlayerIcon()
    {
        Transform tr_temp = Player.Instance.transform;

        _tr_player.localPosition = new Vector3(tr_temp.position.x, 1f, tr_temp.position.z);
    }

    internal void OpenMap()
    {
        Time.timeScale = 0;

        _go_minimapCamera.transform.localPosition = new Vector3(_tr_player.localPosition.x, 31f, _tr_player.localPosition.z);
        _go_minimapCamera.SetActive(true);

        JoyStick.Instance.transform.parent.gameObject.SetActive(false);
        PlayerUICanvas.Instance.gameObject.SetActive(false);

        _go_minimapCanvas.SetActive(true);

        _go_mainCamera.SetActive(false);

        AD.Managers.UpdateM.OnUpdateEvent -= MiniMapDrag;
        AD.Managers.UpdateM.OnUpdateEvent += MiniMapDrag;
    }

    public void CloseMap()
    {
        Time.timeScale = 1;

        AD.Managers.UpdateM.OnUpdateEvent -= MiniMapDrag;

        JoyStick.Instance.transform.parent.gameObject.SetActive(true);
        PlayerUICanvas.Instance.gameObject.SetActive(true);

        _go_mainCamera.SetActive(true);
        _go_minimapCamera.SetActive(false);
    }

    private void MiniMapDrag()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 0f));
                    orgCameraPosition = _go_minimapCamera.transform.localPosition;
                    break;

                case TouchPhase.Moved:
                    dragPosition = Camera.main.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, 0f));

                    offsetPosition = touchPosition - dragPosition;

                    float x = orgCameraPosition.x + offsetPosition.x;
                    float z = orgCameraPosition.z + offsetPosition.z;

                    x = Mathf.Clamp(x, -70f, 70f);
                    z = Mathf.Clamp(z, -18f, 18f);

                    targetPosition = new Vector3(x, orgCameraPosition.y, z);

                    _go_minimapCamera.transform.localPosition = Vector3.Lerp(_go_minimapCamera.transform.localPosition, targetPosition, 0.7f);
                    break;

                case TouchPhase.Ended:
                    break;
            }
        }
    }
}
