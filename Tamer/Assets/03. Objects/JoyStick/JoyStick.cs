using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class JoyStick : MonoBehaviour
{
    enum Mode
    {
        Null,
        FixedArea,
        FreeArea
    }

    [Header("JoyStick 관련 세팅")]
    [SerializeField] Mode _mode = Mode.Null;
    [SerializeField] RectTransform _RTR_handle = null;
    [SerializeField] RectTransform _RTR_handleArea = null;
    [SerializeField] GameObject _go_touchableArea = null;
    [SerializeField] Vector3 _vec_joystick = Vector3.zero;
    private float _joystickDistance = 0;
    private float _handleAreaRadius = 0;
    private Vector3 _vec_firstTouchPosition = Vector3.zero;
    private Vector3 _vec_disPosition = Vector3.zero;
    private bool _isPointUp = false;

    [Header("조종 대상 관련 세팅")]
    [SerializeField] GameObject _go_player = null;
    [SerializeField] Transform _tr_cameraArm = null;
    [SerializeField] float _speed = 0f;

    private void FixedUpdate()
    {
        Control();
    }

    #region Functions

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        _handleAreaRadius = _RTR_handleArea.sizeDelta.y * 0.5f;
        _vec_firstTouchPosition = _RTR_handle.position;
        _vec_disPosition = _RTR_handle.position - _RTR_handleArea.position;

        if (_mode == Mode.FixedArea)
        {
            _go_touchableArea.SetActive(false);
            _RTR_handleArea.gameObject.SetActive(true);
        }
        else if (_mode == Mode.FreeArea)
            _RTR_handleArea.gameObject.SetActive(false);
    }

    private void Control()
    {
        Debug.DrawRay(_tr_cameraArm.position, new Vector3(_tr_cameraArm.forward.x, 0f, _tr_cameraArm.forward.z).normalized, Color.red);

        if (_isPointUp)
            return;

        Vector3 cameraVecVertical = new Vector3(_tr_cameraArm.forward.x, 0f, _tr_cameraArm.forward.z).normalized;
        Vector3 cameraVecHorizontal = new Vector3(_tr_cameraArm.right.x, 0f, _tr_cameraArm.right.z).normalized;

        Vector3 moveDIr = Vector3.zero;

#if UNITY_EDITOR
        if (_vec_joystick.magnitude == 0)
        {
            float moveHorizontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");

            moveDIr = cameraVecVertical * moveVertical + cameraVecHorizontal * moveHorizontal;
        }
#endif

        if (_vec_joystick.magnitude != 0f && _joystickDistance > 5f)
            moveDIr = cameraVecVertical * _vec_joystick.y + cameraVecHorizontal * _vec_joystick.x;

        _go_player.transform.forward = moveDIr;
        _go_player.transform.position += moveDIr * _speed * Time.deltaTime;
    }
    #endregion

    #region EventTrigger
    public void PointDown(BaseEventData baseEventData)
    {
        _isPointUp = false;

        PointerEventData pointerEventData = baseEventData as PointerEventData;

        Vector3 inputPos = pointerEventData.position;

        if (_mode == Mode.FreeArea)
        {
            _vec_firstTouchPosition = inputPos;
            _RTR_handleArea.position = inputPos - _vec_disPosition;
            _RTR_handleArea.gameObject.SetActive(true);
        }

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
        if (_joystickDistance > _handleAreaRadius)
            _joystickDistance = _handleAreaRadius;

        _RTR_handle.position = Vector3.Lerp(_RTR_handle.position, _vec_firstTouchPosition + _vec_joystick * _joystickDistance, 0.7f);

        // Player Ani 설정
        if (_joystickDistance > 5f)
        {

        }
        else
        {

        }
    }

    public void PointUp(BaseEventData baseEventData)
    {
        _isPointUp = true;

        _RTR_handle.anchoredPosition = Vector2.zero;

        if (_mode == Mode.FreeArea)
            _RTR_handleArea.gameObject.SetActive(false);

        // Player Ani 설정 (Idle)
    }
    #endregion
}