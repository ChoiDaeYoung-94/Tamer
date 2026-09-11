using UnityEngine;

public class MiniMapCanvas : MonoBehaviour
{
    private MiniMap _owner;

    private void OnEnable() => _owner = MiniMap.Instance;

    private void OnDisable()
    {
        if (_owner != null) _owner.CloseMap();
        _owner = null;
    }
}
