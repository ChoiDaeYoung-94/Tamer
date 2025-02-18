using UnityEngine;
using UnityEngine.EventSystems;

public class JoyStick : MonoBehaviour
{
    private static JoyStick _instance;
    public static JoyStick Instance { get { return _instance; } }

    private enum Mode
    {
        Null,
        FixedArea,
        FreeArea
    }

    [Header("JoyStick 관련 세팅")]
    [SerializeField] private Mode _mode = Mode.Null;
    [SerializeField] private RectTransform _handleTransform;
    [SerializeField] private RectTransform _handleAreaTransform;
    [SerializeField] private GameObject _touchableArea;
    private Vector3 _joystickVector = Vector3.zero;
    private float _joystickDistance = 0;
    private float _handleAreaRadius = 0;
    private Vector3 _firstTouchPosition = Vector3.zero;
    private Vector3 _distanceVector = Vector3.zero;
    private bool _isPointerUp = false;

    [Header("조종 대상 관련 세팅")]
    [SerializeField] private GameObject _playerObject;
    [SerializeField] private Transform _cameraArmTransform;
    [SerializeField] private float _speed = 0f;

    /// <summary>
    /// LoginCheck.cs 에서 생성
    /// </summary>
    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(transform.parent.gameObject);
    }

    private void FixedUpdate()
    {
        if (!_isPointerUp)
            Control();
    }

    #region Functions
    /// <summary>
    /// Player.cs 에서 플레이어 초기화 후 호출
    /// </summary>
    public void StartInit()
    {
        _playerObject = Player.Instance.PlayerObject;
        _cameraArmTransform = Player.Instance.CameraArm;

        _handleAreaRadius = _handleAreaTransform.sizeDelta.y * 0.5f;
        _firstTouchPosition = _handleTransform.position;
        _distanceVector = _handleTransform.position - _handleAreaTransform.position;

        if (_mode == Mode.FixedArea)
        {
            _touchableArea.SetActive(false);
            _handleAreaTransform.gameObject.SetActive(true);
        }
        else if (_mode == Mode.FreeArea)
        {
            _handleAreaTransform.gameObject.SetActive(false);
        }
    }

    private void Control()
    {
        Debug.DrawRay(_cameraArmTransform.position, new Vector3(_cameraArmTransform.forward.x, 0f, _cameraArmTransform.forward.z).normalized, Color.red);

        Vector3 cameraVerticalVector = new Vector3(_cameraArmTransform.forward.x, 0f, _cameraArmTransform.forward.z).normalized;
        Vector3 cameraHorizontalVector = new Vector3(_cameraArmTransform.right.x, 0f, _cameraArmTransform.right.z).normalized;
        Vector3 moveDirection = Vector3.zero;

#if UNITY_EDITOR
        // GameView에서 Test시 Mode.FixedArea로하고 사용
        if (_joystickVector.magnitude == 0)
        {
            float moveHorizontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");
            moveDirection = cameraVerticalVector * moveVertical + cameraHorizontalVector * moveHorizontal;
        }
#endif

        if (_joystickVector.magnitude != 0f && _joystickDistance > 5f)
            moveDirection = cameraVerticalVector * _joystickVector.y + cameraHorizontalVector * _joystickVector.x;

        if (moveDirection != Vector3.zero)
        {
            _playerObject.transform.forward = moveDirection;
            _playerObject.transform.position += moveDirection * _speed * Time.deltaTime;

            _cameraArmTransform.position =
                new Vector3(_playerObject.transform.position.x, _cameraArmTransform.position.y, _playerObject.transform.position.z);
        }
    }

    public void SetSpeed(float speed) => _speed = speed;
    #endregion

    #region EventTrigger
    public void PointDown(BaseEventData baseEventData)
    {
        _isPointerUp = false;

        PointerEventData pointerEventData = baseEventData as PointerEventData;
        Vector3 inputPos = pointerEventData.position;

        if (_mode == Mode.FreeArea)
        {
            _firstTouchPosition = inputPos;
            _handleAreaTransform.position = inputPos - _distanceVector;
            _handleAreaTransform.gameObject.SetActive(true);
        }

        _handleTransform.position = inputPos;
        _joystickVector = (inputPos - _firstTouchPosition).normalized;
        _joystickDistance = Vector3.Distance(inputPos, _firstTouchPosition);

        // Player Ani 설정
        if (_joystickDistance > 5f)
            Player.Instance.State = Creature.CreatureState.Move;
    }

    public void Drag(BaseEventData baseEventData)
    {
        PointerEventData pointerEventData = baseEventData as PointerEventData;
        Vector3 dragPosition = pointerEventData.position;
        _joystickVector = (dragPosition - _firstTouchPosition).normalized;
        _joystickDistance = Vector3.Distance(dragPosition, _firstTouchPosition);

        if (_joystickDistance > _handleAreaRadius)
            _joystickDistance = _handleAreaRadius;

        _handleTransform.position = Vector3.Lerp(_handleTransform.position, _firstTouchPosition + _joystickVector * _joystickDistance, 0.7f);

        // Player Ani 설정
        if (_joystickDistance > 5f)
            Player.Instance.State = Creature.CreatureState.Move;
        else
        {
            Player.Instance.State = Creature.CreatureState.Idle;
            Player.Instance.AllyIdle();
        }
    }

    public void PointUp(BaseEventData baseEventData)
    {
        _isPointerUp = true;

        _handleTransform.anchoredPosition = Vector2.zero;

        if (_mode == Mode.FreeArea)
            _handleAreaTransform.gameObject.SetActive(false);

        // Player Ani 설정 (Idle)
        Player.Instance.State = Creature.CreatureState.Idle;
        Player.Instance.AllyIdle();
    }
    #endregion
}