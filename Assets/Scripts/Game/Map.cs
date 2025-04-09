using System.Collections.Generic;

using UnityEngine;

public class Map : MonoBehaviour
{
    [Header("--- 세팅 맵에 필요한 객체들 ---")]
    [SerializeField] private GameObject[] _grassPrefabs;
    [SerializeField] private GameObject[] _rockPrefabs;
    [SerializeField] private GameObject[] _treePrefabs;

    [Header("--- 맵 요소 개수 및 거리 설정 ---")]
    [SerializeField] private int _grassCount = 200;
    [SerializeField] private float _grassDistance = 7f;
    [SerializeField] private int _rockCount = 30;
    [SerializeField] private float _rockDistance = 5f;
    [SerializeField] private int _treeCount = 50;
    [SerializeField] private float _treeDistance = 7f;

    private const float _mapWidth = 70f, _mapHeight = 40f;
    private List<Vector3> _spawnedPositions = new List<Vector3>();
    private int _spawnRetryCount = 0;

    private void Awake()
    {
        _spawnedPositions.Clear();
        Init();
    }

    /// <summary>
    /// Map에 필요한 요소들 생성
    /// </summary>
    private void Init()
    {
        var mapElements = new Dictionary<GameObject[], (int, float)>
        {
            { _grassPrefabs, (_grassCount, _grassDistance) },
            { _rockPrefabs, (_rockCount, _rockDistance) },
            { _treePrefabs, (_treeCount, _treeDistance) }
        };

        foreach (var element in mapElements)
        {
            Spawn(element.Key, element.Value.Item1, element.Value.Item2);
        }
    }

    private void Spawn(GameObject[] prefabs, int count, float minDistance)
    {
        int layer = prefabs[0].layer;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetRandomPosition(minDistance, layer);
            if (spawnPosition == Vector3.zero)
                continue;

            int randomIndex = Random.Range(0, prefabs.Length);
            Instantiate(prefabs[randomIndex], spawnPosition, Quaternion.identity, transform);
        }

        _spawnedPositions.Clear();
        _spawnRetryCount = 0;
    }

    /// <summary>
    /// 랜덤한 위치를 생성하고, 최소 거리 조건을 충족하는지 확인
    /// </summary>
    private Vector3 GetRandomPosition(float minDistance, int layer)
    {
        Vector3 candidatePosition;

        while (true)
        {
            candidatePosition = new Vector3(Random.Range(-_mapWidth, _mapWidth), 0, Random.Range(-_mapHeight, _mapHeight));

            if (!_spawnedPositions.Contains(candidatePosition) && !IsOverlapping(candidatePosition, minDistance, layer))
            {
                _spawnedPositions.Add(candidatePosition);
                return candidatePosition;
            }

            if (++_spawnRetryCount > 50)
                return Vector3.zero;
        }
    }

    /// <summary>
    /// 주위에 생성하려는 오브젝트가 겹치는지 확인
    /// </summary>
    private bool IsOverlapping(Vector3 position, float minDistance, int layer)
    {
        return Physics.OverlapSphere(position, minDistance, 1 << layer).Length > 0;
    }
}
