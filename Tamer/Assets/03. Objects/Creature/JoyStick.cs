using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour
{
    [Header("JoyStick 관련")]
    [SerializeField] RectTransform _RTR_handle;
    [SerializeField] RectTransform _RTR_handleArea;
    [SerializeField] Vector3 _vec_joystick;
    private float _joystickDistance;
    private float _handleAreaRadius;
    private Vector3 _vec_firstTouchPosition;

    [Header("조종 대상 관련")]
    [SerializeField] GameObject _go_player;
    [SerializeField] Transform _tr_cameraArm;
    [SerializeField] float _speed;

    private void Start()
    {
        Init();
    }

    private void FixedUpdate()
    {
        Control();
    }

    #region Functions
    private void Init()
    {
        _handleAreaRadius = _RTR_handleArea.sizeDelta.y / 5;
        _vec_firstTouchPosition = _RTR_handle.position;
    }

    private void Control()
    {
        Debug.DrawRay(_tr_cameraArm.position, new Vector3(_tr_cameraArm.forward.x, 0f, _tr_cameraArm.forward.z).normalized, Color.red);

        Vector3 cameraVecVertical = new Vector3(_tr_cameraArm.forward.x, 0f, _tr_cameraArm.forward.z).normalized;
        Vector3 cameraVecHorizontal = new Vector3(_tr_cameraArm.right.x, 0f, _tr_cameraArm.right.z).normalized;

#if UNITY_EDITOR
        if (Application.platform == RuntimePlatform.WindowsEditor && _vec_joystick.magnitude == 0)
        {
            float moveHorizontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");

            Vector3 moveDIr = cameraVecVertical * moveVertical + cameraVecHorizontal * moveHorizontal;
            _go_player.transform.position += moveDIr * _speed * Time.deltaTime;
        }
#endif

        if (_vec_joystick.magnitude != 0 && _joystickDistance > 5f)
        {
            Vector3 moveDIr = cameraVecVertical * _vec_joystick.y + cameraVecHorizontal * _vec_joystick.x;

            _go_player.transform.forward = moveDIr;
            _go_player.transform.position += moveDIr * _speed * Time.deltaTime;
        }
    }
    #endregion

    #region EventTrigger
    public void PointDown(BaseEventData baseEventData)
    {
        PointerEventData pointerEventData = baseEventData as PointerEventData;

        Vector3 inputPos = pointerEventData.position;
        _RTR_handle.position = inputPos;

        _vec_joystick = (inputPos - _vec_firstTouchPosition).normalized;

        _joystickDistance = Vector3.Distance(inputPos, _vec_firstTouchPosition);

        // Player Ani 설정
        if (_joystickDistance > 5f)
        {

        }
    }

    public void Drag(BaseEventData baseEventData)
    {
        PointerEventData pointerEventData = baseEventData as PointerEventData;
        Vector3 DragPosition = pointerEventData.position;

        _vec_joystick = (DragPosition - _vec_firstTouchPosition).normalized;

        _joystickDistance = Vector3.Distance(DragPosition, _vec_firstTouchPosition);

        // Player Ani 설정
        if (_joystickDistance > 5f)
        {

        }
        else
        {

        }

        if (_joystickDistance < _handleAreaRadius)
            _RTR_handle.transform.position = _vec_firstTouchPosition + _vec_joystick * _joystickDistance;
        else
            _RTR_handle.transform.position = _vec_firstTouchPosition + _vec_joystick * _handleAreaRadius;
    }

    public void Drop()
    {
        _RTR_handle.anchoredPosition = Vector2.zero;
        _vec_joystick = Vector3.zero;

        // Player Ani 설정 (Idle)
    }
    #endregion
}