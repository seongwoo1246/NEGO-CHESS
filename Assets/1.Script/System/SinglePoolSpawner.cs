using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>풀에서 꺼낼 때 상태를 되돌려야 하는 컴포넌트가 구현합니다. (폰의 첫 이동, 킹/룩의 hasMoved 등)</summary>
public interface IPoolReset
{
    void ResetForPool();
}

public class SinglePoolSpawner : MonoBehaviour, IPieceSpawner
{
    private readonly Dictionary<GameObject, Queue<GameObject>> PoolDict = new();
    private readonly HashSet<GameObject> _inPool = new();   // 지금 풀에 들어 있는 기물 (중복 반환 방지)

    public void Awake()
    {
        ScriptM.Register<SinglePoolSpawner>(this, UseSpace.Global);
    }

    private void OnEnable() { SceneManager.sceneUnloaded += OnSceneUnloaded; }
    private void OnDisable() { SceneManager.sceneUnloaded -= OnSceneUnloaded; }

    private void OnSceneUnloaded(Scene scene)
    {
        // 풀 안의 기물은 씬과 함께 파괴됩니다. 남은 참조를 모두 비웁니다.
        PoolDict.Clear();
        _inPool.Clear();

    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[SinglePoolSpawner] prefab이 비어 있습니다.");
            return null;
        }

        if (!PoolDict.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            PoolDict[prefab] = queue;
        }

        GameObject piece = null;
        while (queue.Count > 0)
        {
            GameObject candidate = queue.Dequeue();
            if (candidate == null) continue;          // 파괴된 것은 버리고 다음 것
            _inPool.Remove(candidate);
            piece = candidate;
            break;
        }

        if (piece != null)
        {
            piece.transform.SetPositionAndRotation(position, rotation);
            piece.SetActive(true);
            foreach (var r in piece.GetComponents<IPoolReset>()) r.ResetForPool();   // 이전 판 상태 초기화
        }
        else
        {
            piece = Instantiate(prefab, position, rotation);

            // 나중에 반환하기 위해 필요한 프리팹 정보
            var pooledObj = piece.AddComponent<PooledObject>();
            pooledObj.OriginPrefab = prefab;
        }

        return piece;
    }

    public void Despawn(GameObject piece)
    {
        if (piece == null) return;
        if (_inPool.Contains(piece)) return;          // 이미 반환된 기물

        piece.SetActive(false);

        if (piece.TryGetComponent<PooledObject>(out var pooledObj)
            && pooledObj.OriginPrefab != null
            && PoolDict.TryGetValue(pooledObj.OriginPrefab, out var queue))
        {
            queue.Enqueue(piece);
            _inPool.Add(piece);
        }
        else
        {
            Destroy(piece);   // 풀에서 관리되지 않는 오브젝트
        }
    }
}