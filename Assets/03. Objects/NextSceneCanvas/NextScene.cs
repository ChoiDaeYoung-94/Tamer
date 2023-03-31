using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

public class NextScene : MonoBehaviour
{
    private void Start()
    {
        AD.Managers.SceneM.GoScene();
    }
}
