#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using UnityEngine;

public class InitializeGame : MonoBehaviour
{
    /// <summary>
    /// 초기화 해야하는 스크립트들의 이름을 그대로 선언
    /// -> 먼저 적은 순으로 초기화 진행
    /// * PopupManager의 경우 첫 씬인 Main에서 Init이 모두 끝난 뒤 isException를 false로
    /// </summary>
    enum Scripts
    {

    }

    [Tooltip("초기화 해야 할 스크립트를 지닌 게임오브젝트")]
    [SerializeField] GameObject[] _initializeObjects = null;

    private void Start()
    {
        foreach (Scripts script in Enum.GetValues(typeof(Scripts)))
        {
            foreach (GameObject item in _initializeObjects)
            {
                if (item.GetComponent(script.ToString()) != null)
                {
                    item.GetComponent(script.ToString()).SendMessage("StartInit");
                    break;
                }
            }
        }

        MiniMap.Instance.StartInit();
        AD.Managers.GameM.InitMainOrGameScene();
        MonsterGenerator.Instance.Init();
        AD.Managers.PopupM.ReleaseException();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InitializeGame))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("초기화 순서를 지정할 경우 사용", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
