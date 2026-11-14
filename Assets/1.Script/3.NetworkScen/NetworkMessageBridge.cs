using Fusion;
//메세지 생길시 이런 식으로 활용한다.


public struct GameMessage : IGameMessage
{
    public int TargetId { get; set; }
    public float Amount { get; set; }
    public bool IsLocalOnly { get; set; }
}
public class NetworkMessageBridge : NetworkBehaviour
{
  
    private void OnEnable()
    {
        // 중앙 버스로 들어오는 메시지 중 네트워크 메시지 수신 등록
        ScriptM.Subscribe<GameMessage>(OnMessageReceived);
    }

    private void OnDisable()
    { 
        ScriptM.Unsubscribe<GameMessage>(OnMessageReceived);
    }

    private void OnMessageReceived(GameMessage msg)
    {
        // 1. 로컬 전용 메시지라면 네트워크 전송 건너뜀 (무한 루프 방지)
        if (msg.IsLocalOnly) return;

        // 2. 상태 권한(StateAuthority)이 있을 때만 RPC 전송
        if (Object.HasStateAuthority)
        {
            RPC_BroadcastMessage(msg.TargetId, msg.Amount);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastMessage(int targetId, float amount)
    {
        // 3. 수신받은 다른 클라이언트들은 이 메시지를 로컬 버스로 다시 쏴줌
        var incomingMsg = new GameMessage
        {
            TargetId = targetId,
            Amount = amount,
            IsLocalOnly = true // 이미 네트워크를 통해 받았으므로 로컬 전용으로 마킹하여 재전송 방지
        };

        ScriptM.Send(incomingMsg);
    }
}