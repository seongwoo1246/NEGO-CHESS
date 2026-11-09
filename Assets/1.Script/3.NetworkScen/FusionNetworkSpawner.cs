using UnityEngine;
using Fusion;

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
           return null;
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
