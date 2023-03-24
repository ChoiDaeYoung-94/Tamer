#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoginCheck : MonoBehaviour
{
    #region Check Step
    enum Step
    {
        Ready,
        SetCharacter,
        CheckTutorial
    }

    Step _step = Step.Ready;

    void NextStep()
    {
        _step += 1;

        switch (_step)
        {
            case Step.SetCharacter:
                SetCharacter();
                break;
            case Step.CheckTutorial:
                CheckTutorial();
                break;
        }
    }
    #endregion

    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        NextStep();
    }

    #region Functions
    /// <summary>
    /// 성별에 따른 캐릭터 생성
    /// </summary>
    void SetCharacter()
    {
        string sex = AD.Managers.DataM._dic_PlayFabPlayerData["Sex"].Value.Equals("Man") ? "Man" : "Woman";

        AD.Managers.ResourceM.Instantiate_("Player", "Player/Player_" + sex);

        NextStep();
    }

    /// <summary>
    /// Tutorial 여부
    /// </summary>
    void CheckTutorial()
    {
        if (AD.Managers.DataM._dic_PlayFabPlayerData["Tutorial"].Value.ToString().Equals("null"))
        {
            AD.Debug.Log("FirstLogin", "TODO - TUTORIAL");
        }

        NextStep();
    }
    #endregion

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
