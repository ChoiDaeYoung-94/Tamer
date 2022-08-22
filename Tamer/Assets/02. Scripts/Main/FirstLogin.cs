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
    /// 첫 로그인시 해야 할 작업을 위한 데이터 체크
    /// </summary>
    void CheckData()
    {
        if (AD.Managers.DataM._dic_PlayFabPlayerData["Tutorial"].Value.ToString().Equals("null"))
        {
            AD.Debug.Log("FirstLogin", "TODO - TUTORIAL");
        }
    }
    #endregion

#if UNITY_EDITOR
    [CustomEditor(typeof(FirstLogin))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("첫 로그인시 해야 할 작업을 위한 데이터 체크\n1. Tutorial 여부", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
