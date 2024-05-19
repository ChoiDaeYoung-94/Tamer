#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoginCheck : MonoBehaviour
{
    private State _state;

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        _state = new SetCharacterState(this);
        _state.Handle();
    }

    public void SetState(State state)
    {
        _state = state;
        _state.Handle();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(LoginCheck))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("로그인시 해야 할 작업을 위한 데이터 체크" +
                "\n1. 성별에 따른 캐릭터 생성" +
                "\n2. Tutorial 여부", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}

#region LoginCheck Step State
/// <summary>
/// 여러 상태를 정의하기 위해 abstract 사용
/// 아래 State를 상속 받는 class들의 순서대로 상태 진입
/// </summary>
public abstract class State
{
    protected LoginCheck _loginCheck;

    public State(LoginCheck loginCheck)
    {
        _loginCheck = loginCheck;
    }

    public abstract void Handle();
}

class SetCharacterState : State
{
    public SetCharacterState(LoginCheck loginCheck) : base(loginCheck) { }

    public override void Handle()
    {
        AD.Debug.Log("LoginCheck", "SetCharacterState 진입");

        string sex = AD.Managers.DataM._dic_player["Sex"];
        AD.Managers.ResourceM.Instantiate_("Player", "Player/Player_" + sex);

        _loginCheck.SetState(new CheckTutorialState(_loginCheck));
    }
}

class CheckTutorialState : State
{
    public CheckTutorialState(LoginCheck loginCheck) : base(loginCheck) { }

    public override void Handle()
    {
        AD.Debug.Log("LoginCheck", "CheckTutorialState 진입");

        if (AD.Managers.DataM._dic_player["Tutorial"].Equals("null"))
        {
            AD.Debug.Log("FirstLogin", "TODO - TUTORIAL");
        }


    }
}
#endregion