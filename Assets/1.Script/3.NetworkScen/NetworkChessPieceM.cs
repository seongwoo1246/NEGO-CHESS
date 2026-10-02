using Fusion;
using UnityEngine;

// 멀티플레이 전용 네트워크 래퍼 클래스
public class NetworkChessPieceM : NetworkBehaviour
{

    private void Awake()
    {
        ScriptM.Register<NetworkChessPieceM>(this, UseSpace.Network);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkChessPieceM>();
    }

    // 클라이언트가 기물을 움직였을 때 서버(Host)로 RPC 요청
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestMove(int fromX, int fromY, int toX, int toY)
    {
        // 1. 서버 측에서 규칙 검증 수행 후 Core의 MovePiece 실행
        if(ScriptM.TryGet<ChessPieceM>(out ChessPieceM _corePieceM))
        {
            _corePieceM.MovePiece(fromX, fromY, toX, toY);
        }
        // 2. 다른 클라이언트들에게도 이동 결과 전파 (필요시 All-RPC 또는 Networked 변수 활용)
        RPC_BroadcastMove(fromX, fromY, toX, toY);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastMove(int fromX, int fromY, int toX, int toY)
    {
        // 호스트를 제외한 클라이언트들의 로컬 보드 갱신
        if (!HasStateAuthority)
        {
            if (ScriptM.TryGet<ChessPieceM>(out ChessPieceM _corePieceM))
            {
                _corePieceM.MovePiece(fromX, fromY, toX, toY);
            }
        }
    }
}