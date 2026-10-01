

using UnityEngine;

/// <summary>
/// 메세지 혹은 정보 전달이 필요할 때 사용할 인터페이스
/// </summary>
public interface IGameMessage { };

public interface IPieceSpawner
{
    /// <summary>
    /// 생산할 때 사용하는 함수
    /// </summary>
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
    /// <summary>
    /// 파괴 혹은 풀로 되돌릴 때 사용
    /// </summary>
    void Despawn(GameObject prefab);

}