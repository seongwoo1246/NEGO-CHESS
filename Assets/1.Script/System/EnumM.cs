


public enum UseSpace
    {
        Local, // 로컬에서만 사용되는 것으로 씬 이동시 파괴 되는 것들
        Global, // 전역으로 사용되는 것
    Network  //멀티 통신에 필요한 것
}

public enum SceneNumber
{
    GameStart,
    Lobby,
    MultiLobby,
    Battle,
    BattleSingle,
    MultiBattle,

}

public enum PieceColor
{
    none,
    white,
    black
}

public enum GameState
{
    WaitingForPlayers, // 플레이어 대기 중
    GameStart,          // 게임 시작 준비
    Playing,            // 게임 진행 중
    GameEnd,            // 게임 종료시
    Resign,             // 상대방 기권
    Stalemate,          // 무승부
}

public enum pieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King,
    none
}

// 화면 모드 종류 정의 (UI 드롭다운 인덱스와 매핑하기 용이)
public enum WindowMode
{
    ExclusiveFullScreen, // 전용 전체 화면
    FullScreenWindow,    // 테두리 없는 창 모드 (전체 화면 창)
    Windowed             // 창 모드
}

public enum VolumeType
{
    Master, 
    BGM, 
    SFX 
}

public enum CharacterSprite
{
    Nomal,
    Check,
    Anger,
    Move,
    Smile
}

public enum CharacterType
{
    seo,
    reon,
    cloe
}

public enum EditMode
{
    None,
    Name,
    Gender,
    Message
}

public enum NegotiationType 
{ 
    EnPassant,
    Castling,
    Promotion,
    Undo 
}

public enum GameResultType
{ 
    Win,
    Lose,
    Draw,
    Giveup
}

public enum UpgradeType
{
    MaxHp,      
    UseHp,      
    MaxCost,    
    Negotiation,
    SpawnPawn,  
    SpawnBishop,
    SpawnKnight,
    SpawnRook,
    SpawnQueen,
}

public enum RoomTeam : byte { Black = 0, White = 1 }

/// <summary>방의 진행 단계 (UI 안내 문구를 고를 때 사용)</summary>
public enum RoomPhase
{
    WaitingForPlayers, // 사람이 덜 모임
    WaitingForReady,   // 다 모였고, 준비를 기다리는 중
    Countdown,         // 모두 준비 → 카운트다운 중
    Starting           // 게임 시작 확정
}

/// <summary>한 수를 둔 뒤 판이 어떤 상태인지 (규칙 코드가 판정해서 알려 줍니다)</summary>
public enum ChessOutcome
{
    Ongoing,    // 계속 진행
    Checkmate,  // 체크메이트 (방금 둔 쪽이 이김)
    Stalemate,  // 스테일메이트 (무승부)
    Draw        // 그 밖의 무승부 (3회 반복, 기물 부족 등)
}

/// <summary>게임이 끝난 이유 (결과 화면 문구에 사용)</summary>
public enum GameEndReason
{
    None,
    Checkmate,
    Stalemate,
    Draw,
    Resign,      // 기권
    Timeout,     // 시간 초과
    Disconnect   // 상대가 나감
}

/// <summary>대국이 끝난 뒤 각자 고른 선택</summary>
public enum PostGameChoice
{
    None,
    Rematch,  // 같은 상대와 다시 (기물 배치부터)
    Lobby     // 로비로
}
