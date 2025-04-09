using UnityEngine;

public class LoginCheck : MonoBehaviour
{
    private ILoginState _currentState;

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        if (!AD.Managers.GameM.LoginCheck)
            return;

        SetState(new SetCharacterState(this));
    }

    public void SetState(ILoginState newState)
    {
        _currentState = newState;
        _currentState?.Handle();
    }
}

#region LoginCheck Step State

/// <summary>
/// 여러 상태를 정의하기 위해 인터페이스 사용
/// </summary>
public interface ILoginState
{
    void Handle();
}

public class SetCharacterState : ILoginState
{
    private readonly LoginCheck _loginCheck;

    public SetCharacterState(LoginCheck loginCheck)
    {
        _loginCheck = loginCheck;
    }

    public void Handle()
    {
        AD.DebugLogger.Log("LoginCheck", "SetCharacterState 진입");

        string sex = AD.Managers.DataM.LocalPlayerData["Sex"];
        AD.Managers.ResourceM.InstantiatePrefab("Player", "JoyStick/Canvas");
        AD.Managers.ResourceM.InstantiatePrefab("Player", "CMvcams/CM vcams");
        AD.Managers.ResourceM.InstantiatePrefab("Player", "MainAndGameUICanvas/Canvas");
        AD.Managers.ResourceM.InstantiatePrefab("Player", "Player/Player_" + sex);

        _loginCheck.SetState(new CheckTutorialState(_loginCheck));
    }
}

public class CheckTutorialState : ILoginState
{
    private readonly LoginCheck _loginCheck;

    public CheckTutorialState(LoginCheck loginCheck)
    {
        _loginCheck = loginCheck;
    }

    public void Handle()
    {
        AD.DebugLogger.Log("LoginCheck", "CheckTutorialState 진입");

        if (AD.Managers.DataM.LocalPlayerData["Tutorial"].Equals("null"))
        {
            AD.DebugLogger.Log("FirstLogin", "TODO - TUTORIAL");
        }
    }
}

#endregion