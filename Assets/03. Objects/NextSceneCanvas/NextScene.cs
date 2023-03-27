using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

public class NextScene : MonoBehaviour
{
    [SerializeField] TMP_Text _TMP_progress = null;

    private void Start()
    {
        AD.Managers.SceneM.GoScene();
    }

    private void Update()
    {
        this._TMP_progress.text = $"{AD.Managers.SceneM.Progress * 100}%";
    }
}
