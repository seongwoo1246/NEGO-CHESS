using UnityEngine;

/// <summary>
/// 네트워크 코드와 내 체스판(CheseGameM)을 이어 주는 "통역사" 틀입니다.
/// NetworkGameM의 boardAdapter 칸에 이 컴포넌트를 끌어다 놓으세요.
///
/// ※ 아직 연결되지 않은 함수는 호출되면 빨간 에러 로그로 알려 주고, 안전한 기본값을 돌려줍니다.
///    (예: IsLegalMove → false 이므로 연결 전에는 수가 두어지지 않아요)
///    각 함수의 [연결할 곳]을 보고 CheseGameM의 기존 함수를 불러 주세요.
/// </summary>
public class CheseGameMBoardAdapter : MonoBehaviour, IChessBoardAdapter
{
    private static void NotWired(string method)
    {
        Debug.LogError($"[CheseGameMBoardAdapter] {method} 가 아직 CheseGameM에 연결되지 않았어요.");
    }

    // ───── 규칙 정보 ─────

    // [연결할 곳] PieceDatabase / PieceData.maxPiece (싱글 모드 RefreshButtonUI의 max 값과 같은 출처)
    public int GetMaxPieceCount(pieceType type)
    {
        NotWired(nameof(GetMaxPieceCount));
        return 0;
    }

    // ───── 배치 단계 ─────

    // [연결할 곳] 싱글 모드의 SpawnPiece(prefab, x, y, color) + AddSpawnedCount + RefreshButtonUI
    //   · 내 색(local)일 때만 남은 개수 UI를 갱신하세요.
    public void PlacePiece(pieceType type, PieceColor color, int x, int y)
    {
        NotWired(nameof(PlacePiece));
    }

    // [연결할 곳] DestroyPiece(x, y) + 개수 되돌리기
    public void RemovePiece(int x, int y)
    {
        NotWired(nameof(RemovePiece));
    }

    // [연결할 곳] 해당 색 기물 전부 DestroyPiece + 개수(spawnedPieceCounts) 초기화
    public void ClearSide(PieceColor color)
    {
        NotWired(nameof(ClearSide));
    }

    // [연결할 곳] pieceGrid 전체 DestroyPiece + spawnedPieceCounts.Clear() + 흑/백 위치 기록 초기화
    //   · 새 판(재대결)마다 호출됩니다. 위치기록(positionHistory) 같은 대국 기록도 여기서 비우세요.
    public void ClearBoard()
    {
        NotWired(nameof(ClearBoard));
    }

    // [연결할 곳] 기물 버튼(PieceButtonUI)과 타일 클릭 가능 여부. false면 눌러도 아무 일도 없게.
    //   · 켜져 있을 때 타일을 누르면 → NetworkChessPieceM.RequestPlace(type, x, y)
    public void SetSetupInputEnabled(bool enabled, PieceColor localColor)
    {
        NotWired(nameof(SetSetupInputEnabled));
    }

    // ───── 대국 단계 ─────

    // [연결할 곳] 기물 선택/이동 클릭 가능 여부.
    //   · 이동을 확정할 때 MovePiece 대신 → NetworkChessPieceM.RPC_RequestMove(fx, fy, tx, ty)
    public void SetPlayInputEnabled(bool enabled, PieceColor localColor)
    {
        NotWired(nameof(SetPlayInputEnabled));
    }

    // [연결할 곳] CheseGameM이 들고 있는 "현재 턴" 변수
    public void SetSideToMove(PieceColor color)
    {
        NotWired(nameof(SetSideToMove));
    }

    // [연결할 곳] 해당 기물의 GetPossibleMoves 결과 + 내 왕이 체크되지 않는지 검사 (AI의 IsKingInCheck 계열)
    public bool IsLegalMove(PieceColor mover, int fromX, int fromY, int toX, int toY)
    {
        NotWired(nameof(IsLegalMove));
        return false;
    }

    // MovePiece는 이미 알고 있는 함수라 연결해 두었어요.
    // 기물을 잡았는지(true/false)를 알려 주려면, MovePiece가 그 정보를 돌려주게 바꿔서 여기서 받으세요.
    public bool ApplyMove(int fromX, int fromY, int toX, int toY)
    {
        if (ScriptM.TryGet<CheseGameM>(out CheseGameM game))
        {
            game.MovePiece(fromX, fromY, toX, toY);
        }
        return false;
    }

    // [연결할 곳] 체크메이트/스테일메이트/3회 반복 판정 (sideToMove 가 둘 차례인 판)
    public ChessOutcome EvaluateOutcome(PieceColor sideToMove)
    {
        NotWired(nameof(EvaluateOutcome));
        return ChessOutcome.Ongoing;
    }
}