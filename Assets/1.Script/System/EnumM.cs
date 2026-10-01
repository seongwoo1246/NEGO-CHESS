


public enum UseSpace
    {
        Local, // 로컬에서만 사용되는 것으로 씬 이동시 파괴 되는 것들
        Network_Global, // 전역으로 사용되거나 멀티통신에 필요한 것들
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