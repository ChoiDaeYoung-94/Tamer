using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataManager
{
    [Header("--- 참고용 ---")]
    [Tooltip("Dictionary<string, object> - Test")]
    public Dictionary<string, object> _dic_test = null;

    /// <summary>
    /// Managers - Awake() -> Init()
    /// 필요한 데이터 미리 받아 둠
    /// </summary>
    public void Init()
    {
        string getTest = Managers.ResourceM.Load<TextAsset>("DataManager", "Data/getTest").ToString();
        _dic_test = Utils.JsonToObject(getTest) as Dictionary<string, object>;
    }
}
