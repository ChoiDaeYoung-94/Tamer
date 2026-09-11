using System;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;

using Cysharp.Threading.Tasks;

public class CanvasSelectCharacter : MonoBehaviour
{
    [SerializeField] private Transform _trMale;
    [SerializeField] private Transform _trFemale;
    [SerializeField] private Animator _maleAnimator;
    [SerializeField] private Animator _femaleAnimator;

    private Vector3 _targetMalePosition;
    private Vector3 _targetFemalePosition;
    private bool _isMoving;
    private bool _isSaving;

    private void Start()
    {
        AD.Managers.PopupM.ReleaseException();
    }

    public void ButtonPlay()
    {
        if (_isSaving || _isMoving) return;
        _isSaving = true;
        AD.Managers.PopupM.SetException();
        AD.Managers.SoundM.UI_Ok();

        Play().Forget();
    }

    public void ButtonDirection(string direction)
    {
        if (_isSaving || _isMoving)
            return;

        AD.Managers.SoundM.UI_Click();

        SetTargetPosition(direction);
        _isMoving = true;
        Move(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void SetTargetPosition(string direction)
    {
        float xOffset = direction.Equals("Right") ? -5f : 5f;

        _targetMalePosition = Mathf.Approximately(_trMale.position.x, 0)
            ? new Vector3(xOffset, _trMale.position.y, _trMale.position.z)
            : new Vector3(0f, _trMale.position.y, _trMale.position.z);

        _targetFemalePosition = Mathf.Approximately(_trFemale.position.x, 0)
            ? new Vector3(xOffset, _trFemale.position.y, _trFemale.position.z)
            : new Vector3(0f, _trFemale.position.y, _trFemale.position.z);
    }

    private async UniTask Move(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (Mathf.Abs(_trMale.position.x - _targetMalePosition.x) > 0.01f)
            {
                _trMale.position = Vector3.Lerp(_trMale.position, _targetMalePosition, 0.2f);
                _trFemale.position = Vector3.Lerp(_trFemale.position, _targetFemalePosition, 0.2f);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _trMale.position = _targetMalePosition;
            _trFemale.position = _targetFemalePosition;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _isMoving = false;
        }
    }

    private async UniTask Play()
    {
        try
        {
            string selectedGender = Mathf.Approximately(_trMale.position.x, 0) ? "Man" : "Woman";
            AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "Sex", selectedGender } },
                getAllData: true, update: true);
            _maleAnimator.CrossFade("Select", 0.1f);
            _femaleAnimator.CrossFade("Select", 0.1f);
            if (await UniTask.WaitUntil(() => !AD.Managers.ServerM.IsInProgress,
                cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;
            if (AD.Managers.ServerM.HasFailed || !AD.Managers.DataM.IsServerDataReady ||
                !AD.Managers.DataM.LocalPlayerData.TryGetValue("Sex", out var savedGender) || savedGender != selectedGender)
            {
                Debug.LogWarning("[Tamer/Character] Character save was not confirmed. Please retry.");
                return;
            }
            AD.Managers.SceneM.NextScene(AD.GameConstants.Scene.Main);
        }
        finally
        {
            _isSaving = false;
            if (this) AD.Managers.PopupM.ReleaseException();
        }
    }
}
