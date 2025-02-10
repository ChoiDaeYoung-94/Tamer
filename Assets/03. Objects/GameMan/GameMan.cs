using UnityEngine;

public class GameMan : MonoBehaviour
{
    [SerializeField] private GameObject _gamePortalObject = null;

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
            _gamePortalObject.SetActive(true);
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
            _gamePortalObject.SetActive(false);
    }
}
