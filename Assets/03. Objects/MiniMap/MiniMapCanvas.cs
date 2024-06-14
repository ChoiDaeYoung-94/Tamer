using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMapCanvas : MonoBehaviour
{
    private void OnDisable()
    {
        MiniMap.Instance.CloseMap();
    }
}
