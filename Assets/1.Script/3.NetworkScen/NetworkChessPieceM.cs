using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 멀티플레이 전용 "기물 우체국".
/// 기물을 놓고, 치우고, 옮기는 요청을 방장에게 보내고, 방장이 허락한 것만 모두에게 똑같이 전달합니다.
///
/// [중요 1] 내 컴퓨터에서도 "요청을 보낸 즉시" 판을 바꾸지 않습니다.
///          방장이 허락해서 돌려보낸 답장(Broadcast)을 받았을 때 판을 바꿉니다.
/// [중요 2] 배치 단계의 답장은 "그 기물을 놓은 본인"에게만 적용됩니다. (상대에게는 안 보임)
///          서로의 배치는 게임이 시작되는 순간 NetworkGameM이 한꺼번에 공개합니다.
///          대국 단계의 이동은 두 사람 모두에게 똑같이 적용됩니다.
///
/// ※ NetworkGameM과 같은 오브젝트(NetworkObject 하나)에 함께 붙이는 것을 추천합니다.
/// </summary>
public class NetworkChessPieceM : NetworkBehaviour
{
    // 기물이 움직였을 때 뿌려줄 메시지 (효과음/연출용, 기존 그대로)
    public struct PieceMovedMessage : IGameMessage
    {
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
        public bool IsCaptured; // 적을 잡았는지 여부
    }

    private void Awake()
    {
        ScriptM.Register<NetworkChessPieceM>(this, UseSpace.Network);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkChessPieceM>();
    }

    private static bool TryGetGame(out NetworkGameM game)
    {
        if (ScriptM.TryGet<NetworkGameM>(out game) && game != null && game.Adapter != null) return true;
        game = null;
        return false;
    }

    private static PieceColor ToColor(bool isWhite) => isWhite ? PieceColor.white : PieceColor.black;

    // ───────────── 내 화면에서 부르는 함수 (요청만 보냄) ─────────────

    public void RequestPlace(pieceType type, int x, int y) => RPC_RequestPlace((int)type, x, y);
    public void RequestRemove(int x, int y) => RPC_RequestRemove(x, y);
    public void RequestClearMySide() => RPC_RequestClearSide();

    // ───────────── 배치 단계: 요청 (누구나 → 방장) ─────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPlace(int typeId, int x, int y, RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game)) return;
        if (!Enum.IsDefined(typeof(pieceType), typeId)) return;

        var type = (pieceType)typeId;
        if (type == pieceType.none) return;

        if (!game.AuthorityTryPlace(info.Source, type, x, y, out PieceColor color, out string reason))
        {
            game.Announce(info.Source, reason);
            return;
        }

        RPC_BroadcastPlace(game.MatchRound, info.Source.PlayerId, typeId, color == PieceColor.white, x, y);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRemove(int x, int y, RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game)) return;
        if (!game.AuthorityTryRemove(info.Source, x, y, out PieceColor color)) return;

        RPC_BroadcastRemove(game.MatchRound, info.Source.PlayerId, x, y);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestClearSide(RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game)) return;
        if (!game.AuthorityTryClearSide(info.Source, out PieceColor color)) return;

        RPC_BroadcastClearSide(game.MatchRound, info.Source.PlayerId, color == PieceColor.white);
    }

    // ───────────── 배치 단계: 답장 (방장 → 요청한 본인에게만 적용) ─────────────
    // 모두에게 보내되, ownerId가 내 번호가 아니면 무시합니다. → 상대의 판에는 아무것도 나타나지 않아요.

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastPlace(int round, int ownerId, int typeId, bool isWhite, int x, int y)
    {
        if (Runner.LocalPlayer.PlayerId != ownerId) return;
        if (!TryGetGame(out NetworkGameM game)) return;

        game.EnsureRound(round);   // 혹시 새 판 신호보다 먼저 도착했어도 판을 먼저 비우고 시작
        game.Adapter.PlacePiece((pieceType)typeId, ToColor(isWhite), x, y);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastRemove(int round, int ownerId, int x, int y)
    {
        if (Runner.LocalPlayer.PlayerId != ownerId) return;
        if (!TryGetGame(out NetworkGameM game)) return;

        game.EnsureRound(round);
        game.Adapter.RemovePiece(x, y);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastClearSide(int round, int ownerId, bool isWhite)
    {
        if (Runner.LocalPlayer.PlayerId != ownerId) return;
        if (!TryGetGame(out NetworkGameM game)) return;

        game.EnsureRound(round);
        game.Adapter.ClearSide(ToColor(isWhite));
    }

    // ───────────── 대국 단계: 이동 요청 (누구나 → 방장) ─────────────

    /// <summary>
    /// 클라이언트가 기물을 움직이고 싶을 때 호출합니다. (기존 이름 그대로)
    /// 내 판은 바로 바꾸지 않고, 방장이 허락한 뒤 도착하는 방송으로 바뀝니다.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestMove(int fromX, int fromY, int toX, int toY, RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game)) return;

        // 1. 방장이 "내 차례인지 + 규칙에 맞는 수인지" 검사
        if (!game.AuthorityTryBeginMove(info.Source, fromX, fromY, toX, toY, out PieceColor mover, out string reason))
        {
            game.Announce(info.Source, reason);
            return;
        }

        // 2. 통과하면 모두에게 이동 방송
        RPC_BroadcastMove(fromX, fromY, toX, toY, mover == PieceColor.white);
    }

    // ───────────── 대국 단계: 프로모션 (폰이 끝줄에 닿았을 때) ─────────────

    /// <summary>승급할 기물을 골랐다고 방장에게 알립니다. (내 판은 방장의 허락이 온 뒤에 바뀝니다)</summary>
    public void RequestPromotion(pieceType type) => RPC_RequestPromotion((int)type);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPromotion(int typeId, RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game)) return;

        if (!game.AuthorityTryPromote(info.Source, typeId, out int x, out int y, out PieceColor color, out string reason))
        {
            game.Announce(info.Source, reason);
            return;
        }

        RPC_BroadcastPromotion(x, y, typeId, color == PieceColor.white);
    }

    /// <summary>방장 전용: 시간 초과 때 자동 승급을 모두에게 알립니다.</summary>
    internal void AuthorityBroadcastPromotion(int x, int y, pieceType type, PieceColor color)
    {
        RPC_BroadcastPromotion(x, y, (int)type, color == PieceColor.white);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastPromotion(int x, int y, int typeId, bool isWhite)
    {
        if (!TryGetGame(out NetworkGameM game)) return;

        game.Adapter.ApplyPromotion(x, y, (pieceType)typeId, ToColor(isWhite));

        if (Object.HasStateAuthority) game.AuthorityOnPromotionApplied(ToColor(isWhite));
    }

    // 3. 방장 포함 모든 컴퓨터가 같은 수를 같은 순서로 적용
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BroadcastMove(int fromX, int fromY, int toX, int toY, bool moverIsWhite)
    {
        if (!TryGetGame(out NetworkGameM game)) return;

        bool captured = game.Adapter.ApplyMove(fromX, fromY, toX, toY);

        ScriptM.Send(new PieceMovedMessage
        {
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            IsCaptured = captured
        });

        // 판이 바뀐 "뒤에" 방장이 체크메이트 판정과 차례 넘기기를 합니다.
        if (Object.HasStateAuthority) game.AuthorityOnMoveApplied(ToColor(moverIsWhite));
    }
}