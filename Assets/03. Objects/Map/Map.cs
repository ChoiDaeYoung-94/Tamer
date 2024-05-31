using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class Map : MonoBehaviour
{
    [Header("--- 세팅 맵에 필요한 객체들---")]
    [SerializeField] private GameObject[] _go_grass = null;
    [SerializeField] private GameObject[] _go_rock = null;
    [SerializeField] private GameObject[] _go_trees = null;

    [Header("--- 참고용 ---")]
    [SerializeField] private int _grassCount = 200;
    [SerializeField] private float _grassDistance = 7f;
    [SerializeField] private int _rockCount = 30;
    [SerializeField] private float _rockDistance = 5f;
    [SerializeField] private int _treeCount = 50;
    [SerializeField] private float _treeDistance = 7f;
    private float _mapWidth = 70f, _mapHeight = 40f;
    List<Vector3> _list_position = new List<Vector3>();
    private int _failCount = 0;

    private void Awake()
    {
        Init();
    }

    /// <summary>
    /// Map에 필요한 요소들 생성
    /// </summary>
    private void Init()
    {
        Spawn(_go_grass, _grassCount, _grassDistance);
        Spawn(_go_rock, _rockCount, _rockDistance);
        Spawn(_go_trees, _treeCount, _treeDistance);
    }

    private void Spawn(GameObject[] go, int count, float minimumDistance)
    {
        int layer = go[0].layer;

        for (int i = -1; ++i < count;)
        {
            int random = Random.Range(0, go.Length);
            Vector3 randomVector = GetRandomPosition(minimumDistance, layer);

            Instantiate(go[random], randomVector, Quaternion.identity, gameObject.transform);
        }

        _list_position.Clear();
        _failCount = 0;
    }

    /// <summary>
    /// random position 검출
    /// </summary>
    /// <param name="minimumDistance"></param>
    /// <param name="layer"></param>
    /// <returns></returns>
    private Vector3 GetRandomPosition(float minimumDistance, int layer)
    {
        Vector3 vec_temp = Vector3.zero;

        while (true)
        {
            vec_temp = new Vector3(Random.Range(-_mapWidth, _mapWidth), 0, Random.Range(-_mapHeight, _mapHeight));

            if (!_list_position.Contains(vec_temp) && !IsOverlapping(vec_temp, minimumDistance, layer))
            {
                _list_position.Add(vec_temp);
                break;
            }

            if (++_failCount > 50)
                break;
        }

        return vec_temp;
    }

    /// <summary>
    /// 주위에 생성하려는 오브젝트가 겹치나 확인
    /// </summary>
    /// <param name="position"></param>
    /// <param name="minimumDistance"></param>
    /// <param name="layer"></param>
    /// <returns></returns>
    bool IsOverlapping(Vector3 position, float minimumDistance, int layer)
    {
        Collider[] col = Physics.OverlapSphere(position, minimumDistance, 1 << layer);

        return col.Length > 0;
    }
}
