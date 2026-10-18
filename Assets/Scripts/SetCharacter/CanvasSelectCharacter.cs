using System;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;
using TMPro;

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

    private sealed class CharacterSave
    {
        private readonly AD.Managers _owner = AD.Managers.Instance;
        public readonly AD.DataManager Data = AD.Managers.DataM;
        public readonly AD.ServerManager Server = AD.Managers.ServerM;
        public readonly AD.SceneManager Scene = AD.Managers.SceneM;
        public readonly AD.PopupManager Popup = AD.Managers.PopupM;
        private readonly string _account;
        private readonly int _generation, _deletionEpoch;

        public CharacterSave()
        {
            if (Data == null) return;
            _account = Data.PlayFabId;
            _generation = Data.AccountGeneration;
            _deletionEpoch = Data.DeletionEpoch;
        }

        public bool IsCurrent => _owner != null && AD.Managers.Instance == _owner &&
            Data != null && Server != null && Scene != null && Popup != null &&
            ReferenceEquals(AD.Managers.DataM, Data) && ReferenceEquals(AD.Managers.ServerM, Server) &&
            ReferenceEquals(AD.Managers.SceneM, Scene) && ReferenceEquals(AD.Managers.PopupM, Popup) &&
            Data.PlayFabId == _account && !string.IsNullOrEmpty(_account) &&
            Data.AccountGeneration == _generation && Data.DeletionEpoch == _deletionEpoch &&
            Data.IsServerDataReady && !Data.DeletionInProgress && !Data.ReceiptOnly;
    }

    private void Start()
    {
        AD.Managers.PopupM.ReleaseException();
        EnsureAccountPrivacyEntry();
    }

    private void EnsureAccountPrivacyEntry()
    {
        var safeArea = transform.Find("PanelSafeArea");
        if (safeArea == null || safeArea.Find("AccountPrivacyEntry") != null) return;
        var font = GetComponentInChildren<TMP_Text>(true)?.font;
        var button = AD.DeletionView.Button("AccountPrivacyEntry", safeArea, "Account & privacy", font);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(0, 320);
        rect.sizeDelta = new Vector2(620, 80);
        button.transform.SetAsLastSibling(); // The character direction controls cover the safe area.
        button.onClick.AddListener(() =>
        {
            if (!_isSaving && !_isMoving) AD.Managers.PopupM.PopupSetting();
        });
    }

    private static bool HasWritableSession()
    {
        var data = AD.Managers.DataM;
        return data != null && data.IsServerDataReady && !data.DeletionInProgress &&
            !string.IsNullOrEmpty(data.PlayFabId);
    }

    public void ButtonPlay()
    {
        if (_isSaving || _isMoving || !HasWritableSession()) return;
        var save = new CharacterSave();
        if (!save.IsCurrent) return;
        _isSaving = true;
        save.Popup.SetException();
        AD.Managers.SoundM.UI_Ok();

        Play(save).Forget();
    }

    public void ButtonDirection(string direction)
    {
        if (_isSaving || _isMoving || !HasWritableSession())
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

    private async UniTask Play(CharacterSave save)
    {
        try
        {
            if (!save.IsCurrent) return;
            string selectedGender = Mathf.Approximately(_trMale.position.x, 0) ? "Man" : "Woman";
            save.Server.SetData(new Dictionary<string, string> { { "Sex", selectedGender } },
                getAllData: true, update: true);
            if (!save.IsCurrent) return;
            _maleAnimator.CrossFade("Select", 0.1f);
            _femaleAnimator.CrossFade("Select", 0.1f);
            if (!await WaitForSaveAsync(save, selectedGender, this.GetCancellationTokenOnDestroy()))
            {
                if (this != null && save.IsCurrent)
                    Debug.LogWarning("[Tamer/Character] Character save was not confirmed. Please retry.");
                return;
            }
            save.Scene.NextScene(AD.GameConstants.Scene.Main);
        }
        finally { FinishSave(save); }
    }

    private async UniTask<bool> WaitForSaveAsync(CharacterSave save, string selectedGender, CancellationToken token)
    {
        // Stop on ownership loss without cancelling a server shared with the new session.
        if (await UniTask.WaitUntil(() => this == null || !save.IsCurrent || !save.Server.IsInProgress,
            cancellationToken: token).SuppressCancellationThrow()) return false;
        return this != null && save.IsCurrent && !save.Server.HasFailed &&
            save.Data.LocalPlayerData != null && save.Data.LocalPlayerData.TryGetValue("Sex", out var savedGender) &&
            savedGender == selectedGender;
    }

    private void FinishSave(CharacterSave save)
    {
        _isSaving = false;
        if (this != null && save.IsCurrent) save.Popup.ReleaseException();
    }
}
