using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextScene : MonoBehaviour
{
    private void Start()
    {
        print("HI");
        AD.Managers.SceneM.GoScene();
    }
}
