using System.Collections.Generic;
using Fusion;

/// <summary>대국에 참가하는 사람 한 명의 자리 정보</summary>
public readonly struct MatchSeat
{
    public PlayerRef Player { get; }
    public string Nickname { get; }
    public RoomTeam Team { get; }

    public MatchSeat(PlayerRef player, string nickname, RoomTeam team)
    {
        Player = player;
        Nickname = nickname;
        Team = team;
    }
}

/// <summary>
/// "대국 참가자 명단".
/// 방(로비)에서 정한 닉네임과 팀(흑/백)을, 게임 씬으로 넘어가기 직전에 따로 적어 둡니다.
/// 씬이 바뀌면 방의 공용 게시판(RoomState)이 사라질 수 있으니, 이 명단이 "이삿짐 상자" 역할을 해요.
/// </summary>
public static class MatchRoster
{
    private static readonly List<MatchSeat> Seats = new List<MatchSeat>(2);
    private static readonly List<RoomPlayerView> Buffer = new List<RoomPlayerView>(8);

    public static int Count => Seats.Count;

    /// <summary>RoomState가 게임 시작을 알아챈 순간 호출합니다. (씬이 바뀌기 전)</summary>
    public static void Capture(RoomState room)
    {
        Seats.Clear();
        if (room == null) return;

        room.GetPlayers(Buffer);
        for (int i = 0; i < Buffer.Count; i++)
        {
            RoomPlayerView view = Buffer[i];
            Seats.Add(new MatchSeat(view.Player, view.Nickname, view.Team));
        }
    }

    public static void Clear() => Seats.Clear();

    public static bool TryGetSeat(RoomTeam team, out MatchSeat seat)
    {
        for (int i = 0; i < Seats.Count; i++)
        {
            if (Seats[i].Team == team)
            {
                seat = Seats[i];
                return true;
            }
        }
        seat = default;
        return false;
    }

    public static bool TryGetNickname(PlayerRef player, out string nickname)
    {
        for (int i = 0; i < Seats.Count; i++)
        {
            if (Seats[i].Player == player)
            {
                nickname = Seats[i].Nickname;
                return true;
            }
        }
        nickname = null;
        return false;
    }
}