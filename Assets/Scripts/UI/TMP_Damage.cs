using UnityEngine;

using TMPro;

using DG.Tweening;

public class TMP_Damage : MonoBehaviour
{
    [SerializeField] private RectTransform _thisTransform = null;
    [SerializeField] private TMP_Text _thisText = null;

    private float _startPosY = 0.35f;
    private float _plusPosY = 0.15f;
    private Vector3 _startScale = new Vector3(0.01f, 0.01f, 0.01f);
    private Vector3 _plusScale = new Vector3(0.15f, 0.15f, 0.15f);
    private Vector3 _endScale = new Vector3(0.1f, 0.1f, 0.1f);

    private Sequence _effect = null;

    private Quaternion _orgRotation = Quaternion.identity;
    private Vector3 _orgPosition = Vector3.zero;

    private void Awake()
    {
        _orgRotation = _thisTransform.rotation;
        _orgPosition = _thisTransform.position;
    }

    public void Init(float damage)
    {
        _thisText.text = damage.ToString();

        if (_effect == null)
        {
            _thisTransform.anchoredPosition = new Vector3(0f, _startPosY, 0f);

            _effect = DOTween.Sequence();

            _effect.Append(transform.DOScale(_plusScale, 0.2f));
            _effect.Append(transform.DOScale(_endScale, 0.2f));
            _effect.Append(_thisTransform.DOAnchorPosY(_startPosY + _plusPosY, 0.3f));
            _effect.Join(_thisText.DOFade(0f, 0.3f)).OnComplete(() => AD.Managers.PoolM.PushToPool(gameObject));
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    public void Clear()
    {
        if (_effect != null)
        {
            DOTween.Kill(transform);
            _effect = null;
        }

        _thisTransform.rotation = _orgRotation;
        _thisTransform.position = _orgPosition;
        _thisTransform.anchoredPosition = new Vector3(0f, _startPosY, 0f);
        _thisTransform.localScale = _startScale;
        _thisText.alpha = 1f;
    }
}
