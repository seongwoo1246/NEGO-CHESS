using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 멀티플레이 "무르기 협상" 중개자. (NetworkGameM과 같은 오브젝트에 붙이는 것을 추천)
///
/// 흐름
///   내 차례(기물을 움직이기 전)에 Propose(deal) → 방장이 심사(규칙/차례/쿨다운/조건 충분한지)
///   → 상대에게 제안 표시 + 시계 정지 → 상대가 Respond(true/false) 또는 제한시간(기본 30초)
///   → 수락 = 100% 성공 / 거절 = 협상력 비교 확률로 굴림 / 시간 초과 = 실패
///   → 방장이 결과를 모두에게 방송 → 모든 컴퓨터가 똑같이 UndoDealLogic.Apply 실행
///
/// UI는 아래 이벤트/프로퍼티만 쓰면 됩니다. (UI 쪽 가이드는 파일 맨 아래 주석)
/// </summary>
public class NetworkUndoM : NetworkBehaviour
{
    [SerializeField] private UndoDealConfig config = new UndoDealConfig();

    // ───────────── 동기화 값 (방장이 바꾸고 모두가 읽음) ─────────────
    [Networked] public NetworkBool Pending { get; private set; }            // 협상 진행 중?
    [Networked] public NetworkBool ProposerIsWhite { get; private set; }    // 제안한 쪽
    [Networked] public TickTimer ResponseTimer { get; private set; }        // 상대의 응답 제한시간
    [Networked] private int CooldownUntilWhite { get; set; }                // 이 차례 번호(TurnSerial)가 될 때까지 다시 못 함
    [Networked] private int CooldownUntilBlack { get; set; }

    // ───────────── UI용 이벤트 ─────────────
    /// <summary>상대(응답하는 쪽)에게 제안이 도착했을 때</summary>
    public event Action<UndoProposal> ProposalReceived;
    /// <summary>내가 낸 제안이 방장 심사를 통과해 상대의 답을 기다리기 시작했을 때</summary>
    public event Action<UndoProposal> ProposalSent;
    /// <summary>협상이 끝났을 때(양쪽 모두). result.success가 true면 이미 판이 되돌려진 뒤입니다.</summary>
    public event Action<DealResult> Resolved;
    /// <summary>내가 제안하기 전에 막히는 이유 (UI 안내문용)</summary>
    public event Action<string> Denied;

    public struct UndoProposal
    {
        public UndoDeal deal;
        public bool proposerIsWhite;
        public float chanceIfRejected;   // 상대가 거절했을 때 그래도 성사될 확률(%)
        public float seconds;            // 응답 제한시간
    }

    // ───────────── 내 컴퓨터에서만 쓰는 값 ─────────────
    private UndoDeal _deal;              // 방장: 심사 통과한 제안 / 클라이언트: 도착한 제안
    private byte[] _dealBytes;           // 방장: 결과 방송에 다시 쓰려고 보관
    private bool _resolving;             // 방장: 결과 처리 중 중복 방지

    public UndoDealConfig Config => config;

    private void Awake()
    {
        ScriptM.Register<NetworkUndoM>(this, UseSpace.Network);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkUndoM>();
    }

    private bool TryGetGame(out NetworkGameM game, out IUndoBoard board)
    {
        board = null;
        if (ScriptM.TryGet<NetworkGameM>(out game) && game != null && game.Adapter != null)
        {
            board = game.Adapter as IUndoBoard;
            if (board != null) return true;
        }
        game = null;
        return false;
    }

    private static PieceColor ToColor(bool isWhite) => isWhite ? PieceColor.white : PieceColor.black;

    // ═════════════ UI가 부르는 조회 함수 ═════════════

    /// <summary>내가 지금 제안할 수 있는가? 안 되면 이유를 돌려줍니다.</summary>
    public bool CanPropose(out string reason)
    {
        reason = null;
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board) || !game.TryGetLocalColor(out PieceColor me))
        {
            reason = "지금은 할 수 없어요.";
            return false;
        }
        if (game.State != GameState.Playing) { reason = "대국 중에만 할 수 있어요."; return false; }
        if (Pending || game.NegotiationPending) { reason = "이미 협상이 진행 중이에요."; return false; }
        if (game.IsRevealing || game.PromotionPending) { reason = "지금은 할 수 없어요."; return false; }
        if (!game.IsLocalTurn) { reason = "내 차례에만 할 수 있어요."; return false; }
        if (game.TurnMoved) { reason = "기물을 움직이기 전에만 할 수 있어요."; return false; }

        int cd = CooldownRemaining(me);
        if (cd > 0) { reason = $"{cd}번 더 차례가 넘어가야 다시 할 수 있어요."; return false; }

        if (UndoDealLogic.MaxUndoable(board, me) < 1) { reason = "무를 수 있는 수가 없어요."; return false; }
        return true;
    }

    /// <summary>내가 지금 무를 수 있는 최대 수 (내가 둔 수 기준, 한 번에 최대치 적용)</summary>
    public int MaxUndoable()
    {
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board) || !game.TryGetLocalColor(out PieceColor me)) return 0;
        return Mathf.Min(UndoDealLogic.MaxUndoable(board, me), config.maxUndoPerDeal);
    }

    /// <summary>쿨다운이 남은 "차례 넘어감 횟수". 0이면 사용 가능</summary>
    public int CooldownRemaining(PieceColor color)
    {
        if (!ScriptM.TryGet<NetworkGameM>(out NetworkGameM game) || game == null) return 0;
        int until = color == PieceColor.white ? CooldownUntilWhite : CooldownUntilBlack;
        return Mathf.Max(0, until - game.TurnSerial);
    }

    /// <summary>나/상대 협상력과, 상대가 거절했을 때의 성공 확률(%)</summary>
    public bool GetPowers(out float myPower, out float opponentPower, out float chanceIfRejected)
    {
        myPower = opponentPower = chanceIfRejected = 0f;
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board) || !game.TryGetLocalColor(out PieceColor me)) return false;

        myPower = UndoDealLogic.Power(board, me);
        opponentPower = UndoDealLogic.Power(board, NetworkGameM.Opposite(me));
        chanceIfRejected = UndoDealLogic.SuccessChance(myPower, opponentPower);
        return true;
    }

    /// <summary>UI 미리보기: 이 제안이 규칙에 맞는지 (제안 버튼 활성화 여부에 사용)</summary>
    public bool Validate(UndoDeal deal, out string reason)
    {
        reason = "지금은 할 수 없어요.";
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board) || !game.TryGetLocalColor(out PieceColor me)) return false;
        return UndoDealLogic.Validate(board, me, deal, config, out reason);
    }

    /// <summary>UI 미리보기: 조건들이 환산한 "무를 수" 합계</summary>
    public int ComputeCredit(UndoDeal deal)
    {
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board) || !game.TryGetLocalColor(out PieceColor me)) return 0;
        return UndoDealLogic.ComputeCredit(board, me, deal, config);
    }

    /// <summary>응답 제한시간이 남은 초 (진행 중이 아니면 0)</summary>
    public float RemainingSeconds
    {
        get
        {
            if (!Pending || Runner == null) return 0f;
            float? left = ResponseTimer.RemainingTime(Runner);
            return left.HasValue ? Mathf.Max(0f, left.Value) : 0f;
        }
    }

    /// <summary>내가 응답해야 하는 쪽인가?</summary>
    public bool AmIResponder
    {
        get
        {
            if (!Pending) return false;
            if (!ScriptM.TryGet<NetworkGameM>(out NetworkGameM game) || game == null) return false;
            return game.TryGetLocalColor(out PieceColor me) && (me == PieceColor.white) != (bool)ProposerIsWhite;
        }
    }

    // ═════════════ UI가 부르는 행동 함수 ═════════════

    /// <summary>제안하기. 내 판에서 먼저 검사하고, 통과하면 방장에게 보냅니다.</summary>
    public void Propose(UndoDeal deal)
    {
        if (!CanPropose(out string reason) || !Validate(deal, out reason))
        {
            Denied?.Invoke(reason);
            return;
        }
        RPC_Propose(deal.ToBytes());
    }

    /// <summary>상대의 제안에 응답하기 (true = 허락, false = 거절)</summary>
    public void Respond(bool accept) => RPC_Respond(accept);

    // ═════════════ 방장 심사 ═════════════

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Propose(byte[] data, RpcInfo info = default)
    {
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board)) return;
        if (!game.TryGetColor(info.Source, out PieceColor mover)) return;

        if (Pending || _resolving) return;

        if (!game.AuthorityCanBeginNegotiation(mover, out string reason))
        {
            game.Announce(info.Source, reason);
            return;
        }

        int serial = game.TurnSerial;
        int until = mover == PieceColor.white ? CooldownUntilWhite : CooldownUntilBlack;
        if (until > serial)
        {
            game.Announce(info.Source, $"{until - serial}번 더 차례가 넘어가야 다시 할 수 있어요.");
            return;
        }

        if (!UndoDeal.TryFromBytes(data, out UndoDeal deal))
        {
            game.Announce(info.Source, "제안이 올바르지 않아요.");
            return;
        }

        if (!UndoDealLogic.Validate(board, mover, deal, config, out reason))
        {
            game.Announce(info.Source, reason);
            return;
        }

        _deal = deal;
        _dealBytes = data;
        _resolving = false;

        ProposerIsWhite = mover == PieceColor.white;
        ResponseTimer = TickTimer.CreateFromSeconds(Runner, config.responseSeconds);
        Pending = true;
        game.AuthoritySetNegotiating(true);   // 시계 정지 + 이동/턴 넘기기 잠금

        float myPower = UndoDealLogic.Power(board, mover);
        float oppPower = UndoDealLogic.Power(board, NetworkGameM.Opposite(mover));
        float chance = UndoDealLogic.SuccessChance(myPower, oppPower);

        RPC_ShowProposal(data, mover == PieceColor.white, chance, config.responseSeconds);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Respond(bool accept, RpcInfo info = default)
    {
        if (!Pending || _resolving) return;
        if (!TryGetGame(out NetworkGameM game, out _)) return;
        if (!game.TryGetColor(info.Source, out PieceColor responder)) return;
        if ((responder == PieceColor.white) == (bool)ProposerIsWhite) return;   // 제안한 본인은 응답 불가

        Resolve(accept ? DealResponse.Accepted : DealResponse.Rejected);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !Pending || _resolving) return;
        if (!TryGetGame(out NetworkGameM game, out _)) return;

        // 대국이 끝났거나(기권/시간 초과/연결 끊김) 판이 바뀌었으면 협상 취소
        if (game.State != GameState.Playing)
        {
            Resolve(DealResponse.Cancelled);
            return;
        }

        if (ResponseTimer.Expired(Runner)) Resolve(DealResponse.TimedOut);
    }

    /// <summary>방장 전용: 결과를 정하고 모두에게 방송합니다.</summary>
    private void Resolve(DealResponse response)
    {
        if (_resolving || _deal == null) return;
        if (!TryGetGame(out NetworkGameM game, out IUndoBoard board)) return;

        _resolving = true;

        PieceColor mover = ProposerIsWhite ? PieceColor.white : PieceColor.black;
        bool success = false;
        float chance = 0f;

        if (response == DealResponse.Accepted)
        {
            success = true;
        }
        else if (response == DealResponse.Rejected)
        {
            float myPower = UndoDealLogic.Power(board, mover);
            float oppPower = UndoDealLogic.Power(board, NetworkGameM.Opposite(mover));
            chance = UndoDealLogic.SuccessChance(myPower, oppPower);
            success = UnityEngine.Random.Range(0f, 100f) <= chance;
        }
        // TimedOut / Cancelled = 실패

        // 안전: 심사 후 판이 바뀌지 않았지만, 혹시 모르니 한 번 더 검사
        if (success && !UndoDealLogic.Validate(board, mover, _deal, config, out _)) success = false;

        int serial = game.TurnSerial;
        if (response != DealResponse.Cancelled)
        {
            int until = serial + config.cooldownTurns;
            if (mover == PieceColor.white) CooldownUntilWhite = until;
            else CooldownUntilBlack = until;
        }

        Pending = false;
        ResponseTimer = default;

        RPC_Result(game.MatchRound, serial, mover == PieceColor.white, (int)response, success, chance, _dealBytes);

        _deal = null;
        _dealBytes = null;
    }

    // ═════════════ 방장 → 모두 ═════════════

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowProposal(byte[] data, bool proposerIsWhite, float chance, float seconds)
    {
        if (!UndoDeal.TryFromBytes(data, out UndoDeal deal)) return;
        if (!TryGetGame(out NetworkGameM game, out _)) return;

        _deal = deal;

        var proposal = new UndoProposal
        {
            deal = deal,
            proposerIsWhite = proposerIsWhite,
            chanceIfRejected = chance,
            seconds = seconds
        };

        if (!game.TryGetLocalColor(out PieceColor me)) return;   // 관전자 등

        if ((me == PieceColor.white) == proposerIsWhite) ProposalSent?.Invoke(proposal);
        else ProposalReceived?.Invoke(proposal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Result(int round, int serial, bool moverIsWhite, int responseId, bool success, float chance, byte[] data)
    {
        ScriptM.TryGet<NetworkGameM>(out NetworkGameM game);
        PieceColor mover = ToColor(moverIsWhite);
        int plies = 0;

        try
        {
            if (success && TryGetGame(out game, out IUndoBoard board) && UndoDeal.TryFromBytes(data, out UndoDeal deal))
            {
                RuleBans.CurrentTurn = serial;
                plies = UndoDealLogic.Apply(board, mover, deal, config);
            }
            else success = false;
        }
        catch (Exception e)
        {
            Debug.LogException(e);   // ← 이 에러 내용을 알려 주세요
            success = false;
        }
        finally
        {
            _deal = null;
            Resolved?.Invoke(new DealResult
            {
                mover = mover,
                success = success,
                response = (DealResponse)responseId,
                chance = chance,
                undonePlies = plies
            });

            if (Object.HasStateAuthority)
            {
                _resolving = false;
                if (game != null) game.AuthorityOnNegotiationEnded(success ? plies : 0);
            }
        }
    }

    /*
     ───────────── UI 연결 가이드 ─────────────
      버튼 활성화      : undoM.CanPropose(out reason)
      "최대 N수" 표시  : undoM.MaxUndoable()
      ±1 / ±5 화살표   : deal.undoCount 를 1~MaxUndoable 사이로 조절
      협상력 표시      : undoM.GetPowers(out my, out opp, out chance)
      조건 칩          : deal.rules.Add(new DealRuleChip{ rule=DealRule.Castling, turns=3 })   // turns 0 = 이번 판 내내
                         deal.pieceChips.Add(new DealPieceChip{ type=pieceType.Pawn, count=2 })  // "폰 제거" 슬롯 (종류당 1개 슬롯, 킹 불가)
      금지 남은 턴(프앙캐): RuleBans.RemainingTurns(color, DealRule.Promotion/EnPassant/Castling)  (0=없음, -1=이번 판 내내)
      현재 턴 숫자     : NetworkGameM.TurnNumber (흰색이 움직일 때마다 +1)
      제안자 화면      : ProposalSent 이후엔 수락/거절 버튼 숨김 (AmIResponder가 true인 사람만 버튼 표시)
      조건 합계 표시   : undoM.ComputeCredit(deal)  (>= deal.undoCount 여야 제안 가능)
      제안 버튼        : undoM.Propose(deal)  → 실패하면 Denied 이벤트
      상대 팝업        : ProposalReceived 이벤트 → 조건 표시 + undoM.RemainingSeconds 로 타이머 → undoM.Respond(true/false)
      결과 팝업        : Resolved 이벤트 (DealResult.success / response / chance)
    */
}