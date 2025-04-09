using UnityEngine;

public class MiniMapCanvas : MonoBehaviour
{
    private void OnDisable()
    {
        MiniMap.Instance.CloseMap();
    }
}
