


public enum UseSpace
    {
        Local, // 로컬에서만 사용되는 것으로 씬 이동시 파괴 되는 것들
        Global, // 전역으로 사용되는 것
    Network  //멀티 통신에 필요한 것
}


public enum PlayMode
{
    Single,//싱글 모드
    multi // 멀티 모드
}

public enum SceneNumber
{
    GameStart,
    Lobby,
    Battle =10,
    multiBattle =20,

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