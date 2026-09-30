using Fusion;
using UnityEngine;

//public class NetworkMessageBridge : NetworkBehaviour
//{
//    private void OnEnable()
//    {
//        // 중앙 버스로 들어오는 메시지 중 네트워크 메시지 수신 등록
//        ScriptM.Subscribe<DamageMessage>(OnDamageMessageReceived);
//    }

//    private void OnDisable()
//    {
//        ScriptM.Unsubscribe<DamageMessage>(OnDamageMessageReceived);
//    }

//    private void OnDamageMessageReceived(DamageMessage msg)
//    {
//        // LocalOnly 메시지라면 네트워크 전송 건너뜀
//        if (msg.Scope == UseSpace.Local) return;

//        // Networked 메시지일 경우, 상태 권한(StateAuthority)이 있을 때 RPC 또는 Networked 변수로 전송
//        if (HasStateAuthority)
//        {
//            RPC_BroadcastDamage(msg.TargetId, msg.Amount);
//        }
//    }

//    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
//    private void RPC_BroadcastDamage(int targetId, float amount)
//    {
//        // 수신받은 다른 클라이언트들은 이 메시지를 로컬 버스로 다시 흘려보내 UI/이펙트 등이 반응하게 함
//        ScriptM.Send(new DamageMessage
//        {
//            TargetId = targetId,
//            Amount = amount,
//            Scope = UseSpace.Local // 이미 수신받았으므로 local 처리
//        });
//    }
//}