

using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 메세지 혹은 정보 전달이 필요할 때 사용할 인터페이스
/// </summary>
public interface IGameMessage { };

public interface ISoundService
{
    void playBGM(string soundName, bool loop = true);
    void playSFX(string soundName);
    void StopBGM();
    UniTask FadeSound(string newSound, float fadeTime);

    void SetMasterVolume(float linearVolume);
    void SetBGMVolume(float linearVolume);
    void SetSFXVolume(float linearVolume);
    void SoundMute(VolumeType type);
    void ResetAllSoundSettings();
}

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


/// <summary>
/// 네트워크 코드와 "내 체스판 코드(CheseGameM)" 사이의 연결 창구입니다.
///
/// 네트워크 코드는 "누가 언제 무엇을 해도 되는지"만 정하고,
/// 실제로 기물을 만들고 옮기는 일은 이 창구를 통해 체스판 코드에 시킵니다.
/// 그래서 체스 규칙/연출 코드를 건드리지 않고도 네트워크 부분을 따로 고칠 수 있어요.
/// (구현: SingleChessGameM.Network.cs)
/// </summary>
public interface IChessBoardAdapter
{
    // ───── 규칙 정보 ─────

    /// <summary>이 종류의 기물을 한 사람이 최대 몇 개까지 놓을 수 있나요? (PieceData.maxPiece)</summary>
    int GetMaxPieceCount(pieceType type);

    // ───── 배치 단계 (모든 컴퓨터에서 똑같이 실행됩니다) ─────

    /// <summary>
    /// (x, y)에 기물을 만듭니다.
    /// 배치 중에는 "내 기물"만 이 함수로 들어오고, 상대 기물은 게임 시작 때 한꺼번에 들어옵니다.
    /// 내 색 기물일 때만 남은 개수 UI를 갱신하세요. (상대 기물은 개수에 세지 않음)
    /// </summary>
    void PlacePiece(pieceType type, PieceColor color, int x, int y);

    /// <summary>(x, y)의 기물을 치웁니다.</summary>
    void RemovePiece(int x, int y);

    /// <summary>해당 색 기물을 전부 치웁니다 ("리셋" 버튼).</summary>
    void ClearSide(PieceColor color);

    /// <summary>판 위의 모든 기물을 치웁니다 (새 판 시작).</summary>
    void ClearBoard();

    /// <summary>배치 입력(기물 버튼/칸 클릭)을 켜거나 끕니다. localColor = 내 색.</summary>
    void SetSetupInputEnabled(bool enabled, PieceColor localColor);

    // ───── 대국 단계 ─────

    /// <summary>기물 선택/이동 입력을 켜거나 끕니다. (내 차례일 때만 true)</summary>
    void SetPlayInputEnabled(bool enabled, PieceColor localColor);

    /// <summary>지금 누구 차례인지 체스판 코드에 알려 줍니다. (여러 번 불려도 안전해야 해요)</summary>
    void SetSideToMove(PieceColor color);

    /// <summary>mover가 (from → to)로 두는 것이 규칙에 맞나요? (방장 컴퓨터에서 호출)</summary>
    bool IsLegalMove(PieceColor mover, int fromX, int fromY, int toX, int toY);

    /// <summary>실제로 기물을 옮깁니다. 기물을 잡았다면 true.</summary>
    bool ApplyMove(int fromX, int fromY, int toX, int toY);

    /// <summary>sideToMove 차례인 판의 상태를 판정합니다. (방장 컴퓨터에서 호출)</summary>
    ChessOutcome EvaluateOutcome(PieceColor sideToMove);

    // ───── 프로모션 (폰이 끝줄에 닿았을 때) ─────

    /// <summary>mover의 폰이 끝줄에 닿아 승급을 기다리고 있나요? 있으면 그 칸을 알려 줍니다. (방장 컴퓨터에서 호출)</summary>
    bool TryFindPromotion(PieceColor mover, out int x, out int y);

    /// <summary>(x, y)의 폰을 선택한 기물로 바꿉니다. (모든 컴퓨터에서 똑같이 실행)</summary>
    void ApplyPromotion(int x, int y, pieceType newType, PieceColor color);

    /// <summary>승급 선택창을 보이거나 숨깁니다. visible일 때는 localColor 쪽 창만 켭니다.</summary>
    void SetPromotionChoiceVisible(bool visible, PieceColor localColor);
}


/// <summary>기물 프리셋을 쓸 수 있는 판(AI전 / 연습 / 멀티)이 구현하는 약속</summary>
public interface IPresetHost
{
    /// <summary>지금 프리셋을 저장하거나 불러올 수 있는 단계인가요? (배치 단계)</summary>
    bool CanUsePreset { get; }

    /// <summary>지금 배치 중인 내 쪽 기물을 "내 진영 기준 좌표"로 모아 줍니다.</summary>
    void CollectMyPieces(List<PresetPiece> output);

    /// <summary>프리셋을 판에 놓습니다. 실제로 놓은 개수를 돌려줍니다.</summary>
    int ApplyPreset(IReadOnlyList<PresetPiece> pieces);
}