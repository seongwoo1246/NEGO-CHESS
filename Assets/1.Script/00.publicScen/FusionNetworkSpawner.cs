using UnityEngine;
using Fusion;
using Debug = DebugM<FusionNetworkSpawner>;
public class FusionNetworkSpawner : IPieceSpawner
{
  private NetworkRunner runner;

    public FusionNetworkSpawner(NetworkRunner runner)
    {
        this.runner = runner;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if(runner == null|| !runner.IsServer)
        {
            Debug.LogWarning("기물 생성 권한이 없거나 NetworkRunner가 준비되지 않았습니다.");
        }

        NetworkObject netObj = runner.Spawn(prefab, position, rotation);
        return netObj.gameObject;
    }

    public void Despawn(GameObject priece)
    {
        if(runner ==null|| !runner.IsServer) return;

        if(priece.TryGetComponent<NetworkObject>(out var netObj))
        {
            runner.Despawn(netObj);
        }
    }


}
