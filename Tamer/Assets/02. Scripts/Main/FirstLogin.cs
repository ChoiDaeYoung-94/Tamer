#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstLogin : MonoBehaviour
{
    /// <summary>
    /// InitializeMain.cs 에서 호출
    /// </summary>
    private void StartInit()
    {
        CheckData();
    }

    #region Functions
    /// <summary>
    /// Sex Data유무 확인
    /// </summary>
    void CheckData()
    {
        if (AD.Managers.DataM._dic_PlayFabPlayerData["Sex"].Value.ToString().Equals("null"))
            AD.Managers.SceneM.NextScene(AD.Define.Scenes.SetCharacter);
    }
    #endregion

#if UNITY_EDITOR
    [CustomEditor(typeof(FirstLogin))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("첫 로그인 시 캐릭터 성별 선택 위함", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
