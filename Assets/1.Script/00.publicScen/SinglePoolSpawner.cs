using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Audio.ProcessorInstance;

public class SinglePoolSpawner : IPieceSpawner
{
    private Dictionary<GameObject, Queue<GameObject>> PoolDict = new();

    public GameObject Spawn(GameObject prefab,Vector3 position, Quaternion rotation)
    {
        if(!PoolDict.ContainsKey(prefab))
        {
            PoolDict[prefab] = new Queue<GameObject>();
        }

        GameObject piece;
        if (PoolDict[prefab].Count > 0)
        {
            piece = PoolDict[prefab].Dequeue();
            piece.transform.position = position;
            piece.transform.rotation = rotation;
            piece.SetActive(true);
        }
        else
        {
            piece = GameObject.Instantiate(prefab,position,rotation);

            // 나중에 반환 하기 위해서 필요한 프리팹 정보
            var pooledObj = piece.AddComponent<PooledObject>();
            pooledObj.OriginPrefab = prefab;
        }
        
        return piece;
    }


    public void Despawn(GameObject piece)
    {
       piece.SetActive(false);

        //등록한 프리랩 키 가져오기
        if (piece.TryGetComponent<PooledObject>(out var pooledObj))
        {
            GameObject key = pooledObj.OriginPrefab;

            // 딕셔너리의 큐에 다시 넣어주기!
            if (PoolDict.TryGetValue(key, out var queue))
            {
                queue.Enqueue(piece);
            }
        }
        else
        {
            // 혹시 풀에서 관리되지 않는 오브젝트라면 그냥 파괴
            Object.Destroy(piece);
        }
    }

}



