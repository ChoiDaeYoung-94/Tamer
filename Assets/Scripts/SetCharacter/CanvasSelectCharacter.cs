using System.Collections.Generic;

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
    private UniTask _moveTask = UniTask.CompletedTask;

    private void Start()
    {
        AD.Managers.PopupM.ReleaseException();
    }

    public void ButtonPlay()
    {
        AD.Managers.PopupM.SetException();
        AD.Managers.SoundM.UI_Ok();

        Play().Forget();
    }

    public void ButtonDirection(string direction)
    {
        if (!_moveTask.Status.IsCompleted())
            return;

        AD.Managers.SoundM.UI_Click();

        SetTargetPosition(direction);
        _moveTask = Move();
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

    private async UniTask Move()
    {
        while (Mathf.Abs(_trMale.position.x - _targetMalePosition.x) > 0.01f)
        {
            _trMale.position = Vector3.Lerp(_trMale.position, _targetMalePosition, 0.2f);
            _trFemale.position = Vector3.Lerp(_trFemale.position, _targetFemalePosition, 0.2f);

            await UniTask.Yield();
        }

        _trMale.position = _targetMalePosition;
        _trFemale.position = _targetFemalePosition;
    }

    private async UniTaskVoid Play()
    {
        string selectedGender = Mathf.Approximately(_trMale.position.x, 0) ? "Man" : "Woman";
        AD.Managers.ServerM.SetData(new Dictionary<string, string> { { "Sex", selectedGender } }, false, false);

        _maleAnimator.CrossFade("Select", 0.1f);
        _femaleAnimator.CrossFade("Select", 0.1f);

        await UniTask.WaitUntil(() => !AD.Managers.ServerM.IsInProgress);
        AD.Managers.DataM.UpdatePlayerData();
        await UniTask.WaitUntil(() => !AD.Managers.ServerM.IsInProgress);

        AD.Managers.SceneM.NextScene(AD.GameConstants.Scene.Main);
    }
}