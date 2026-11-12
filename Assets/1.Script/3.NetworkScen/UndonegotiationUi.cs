using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 멀티 "무르기 협상" 화면 연결 스크립트. 인스펙터에서 버튼/글자를 끌어다 연결하세요. (안 쓰는 칸은 비워도 됩니다)
///
/// 모드
///   Compose : 내가 제안을 만드는 중 (화살표/슬롯 편집 가능, [제안하기] 표시)
///   Waiting : 제안을 보내고 상대 답을 기다리는 중 (읽기 전용 — 수락/거절 버튼은 안 보임)
///   Respond : 상대의 제안을 받은 중 (읽기 전용 + [수락]/[거절] 표시 — 상대에게만 보임)
/// </summary>
public class UndoNegotiationUI : MonoBehaviour
{
    [Header("연결 (비워 두면 씬에서 자동으로 찾습니다)")]
    [SerializeField] private NetworkUndoM undoM;
    [SerializeField] private NetworkGameM game;

    [Header("메인 화면(HUD)")]
    [SerializeField] private Button openButton;                 // "무르기 협상" 버튼
    [SerializeField] private TMP_Text turnText;                 // 현재 턴 숫자 (흰색이 움직일 때마다 +1)
    [SerializeField] private string turnFormat = "현재 턴 : {0}";
    [SerializeField] private TMP_Text myPromotionText;          // 프 / 앙 / 캐 : 금지 남은 턴 (내 쪽)
    [SerializeField] private TMP_Text myEnPassantText;
    [SerializeField] private TMP_Text myCastlingText;
    [SerializeField] private TMP_Text oppPromotionText;         // (상대 쪽)
    [SerializeField] private TMP_Text oppEnPassantText;
    [SerializeField] private TMP_Text oppCastlingText;
    [Tooltip("본 화면에 항상 보이는 협상력 (기물이 잡히거나 승급하면 바뀝니다)")]
    [SerializeField] private TMP_Text hudMyPowerText;
    [SerializeField] private TMP_Text hudOppPowerText;

    [Header("패널")]
    [SerializeField] private GameObject panel;                  // 판넬 루트 (꺼 두세요)
    [SerializeField] private TMP_Text maxText;                  // 최대수 (오른쪽 노란 숫자)
    [SerializeField] private TMP_Text countText;                // 가운데 숫자
    [SerializeField] private TMP_Text chanceText;               // 성공확률
    [SerializeField] private TMP_Text powerText;                // 나의 협상력 VS 상대 협상력
    [SerializeField] private TMP_Text timerText;                // 남은 시간 (Waiting/Respond)
    [SerializeField] private TMP_Text reasonText;               // 제안이 안 되는 이유 / 조건 합계
    [SerializeField] private TMP_Text toastText;                // 결과/안내 메시지 (패널 밖이어도 됨)
    [SerializeField] private float toastSeconds = 3f;

    [Header("화살표 (-5 -1 +1 +5)")]
    [SerializeField] private Button minus5Button;
    [SerializeField] private Button minus1Button;
    [SerializeField] private Button plus1Button;
    [SerializeField] private Button plus5Button;

    [Header("조건 추가 버튼")]
    [SerializeField] private Button promotionButton;
    [SerializeField] private Button enPassantButton;
    [SerializeField] private Button castlingButton;
    [SerializeField] private Button pawnButton;
    [SerializeField] private Button knightButton;
    [SerializeField] private Button rookButton;
    [SerializeField] private Button bishopButton;
    [SerializeField] private Button queenButton;

    [Header("슬롯")]
    [SerializeField] private Transform slotParent;
    [SerializeField] private UndoSlotView slotPrefab;
    [SerializeField] private int maxRuleTurns = 30;
    [SerializeField] private int maxPieceCount = 8;

    [Header("버튼")]
    [SerializeField] private Button proposeButton;              // 제안하기 (Compose)
    [SerializeField] private Button closeButton;                // 닫기 (Compose)
    [SerializeField] private Button acceptButton;               // 제안 수락 (Respond)
    [SerializeField] private Button rejectButton;               // 제안 거절 (Respond)

    [Header("모드별로 켜질 오브젝트 (화살표/조건 버튼/수락·거절 등을 넣으세요)")]
    [SerializeField] private GameObject[] showWhenComposing;
    [SerializeField] private GameObject[] showWhenWaiting;
    [SerializeField] private GameObject[] showWhenResponding;

    private enum Mode { Closed, Compose, Waiting, Respond }
    private Mode _mode = Mode.Closed;

    private UndoDeal _deal = new UndoDeal();
    private int _count = 1;
    private float _shownChance;                 // Respond일 때 보여줄 (제안자 기준) 성공확률

    private readonly Dictionary<DealRule, UndoSlotView> _ruleSlots = new Dictionary<DealRule, UndoSlotView>();
    private readonly Dictionary<pieceType, UndoSlotView> _pieceSlots = new Dictionary<pieceType, UndoSlotView>();

    private bool _subscribed;
    private float _nextFind, _nextRefresh, _toastUntil;
    private float _modeSince;

    // ───────────── 초기화 ─────────────

    private void Awake()
    {
        Bind(openButton, OpenCompose);
        Bind(minus5Button, () => AddCount(-5));
        Bind(minus1Button, () => AddCount(-1));
        Bind(plus1Button, () => AddCount(+1));
        Bind(plus5Button, () => AddCount(+5));

        Bind(promotionButton, () => AddRuleSlot(DealRule.Promotion));
        Bind(enPassantButton, () => AddRuleSlot(DealRule.EnPassant));
        Bind(castlingButton, () => AddRuleSlot(DealRule.Castling));

        Bind(pawnButton, () => AddPieceSlot(pieceType.Pawn));
        Bind(knightButton, () => AddPieceSlot(pieceType.Knight));
        Bind(rookButton, () => AddPieceSlot(pieceType.Rook));
        Bind(bishopButton, () => AddPieceSlot(pieceType.Bishop));
        Bind(queenButton, () => AddPieceSlot(pieceType.Queen));

        Bind(proposeButton, Propose);
        Bind(closeButton, CloseCompose);
        Bind(acceptButton, () => Answer(true));
        Bind(rejectButton, () => Answer(false));

        if (panel != null) panel.SetActive(false);
        if (toastText != null) toastText.gameObject.SetActive(false);
        ApplyModeObjects();
    }

    private static void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b != null) b.onClick.AddListener(a);
    }

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || undoM == null) return;
        undoM.ProposalSent += OnProposalSent;
        undoM.ProposalReceived += OnProposalReceived;
        undoM.Resolved += OnResolved;
        undoM.Denied += OnDenied;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || undoM == null) return;
        undoM.ProposalSent -= OnProposalSent;
        undoM.ProposalReceived -= OnProposalReceived;
        undoM.Resolved -= OnResolved;
        undoM.Denied -= OnDenied;
        _subscribed = false;
    }

    // ───────────── 매 프레임 ─────────────

    private void Update()
    {
        // 네트워크 오브젝트는 늦게 생길 수 있어서 0.5초마다 찾아봅니다.
        if ((undoM == null || game == null) && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + 0.5f;
            if (undoM == null) undoM = FindFirstObjectByType<NetworkUndoM>();
            if (game == null) game = FindFirstObjectByType<NetworkGameM>();
        }
        if (undoM != null) Subscribe();

        // 네트워크 오브젝트가 아직 스폰 전이면(씬 로딩 직후) 네트워크 값을 읽지 않습니다.
        if (!NetReady) return;

        if (toastText != null && toastText.gameObject.activeSelf && Time.unscaledTime >= _toastUntil)
            toastText.gameObject.SetActive(false);

        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.1f;

        RefreshHud();
        if (_mode != Mode.Closed) RefreshPanel();
        if ((_mode == Mode.Waiting || _mode == Mode.Respond) && !undoM.Pending && Time.unscaledTime - _modeSince > 1.5f)
        {
            ClearSlots();
            SetMode(Mode.Closed);
        }

    }

    /// <summary>NetworkGameM / NetworkUndoM 이 둘 다 스폰을 마쳐서 [Networked] 값을 읽어도 되는 상태인가</summary>
    private bool NetReady =>
        undoM != null && undoM.Object != null && undoM.Object.IsValid &&
        game != null && game.Object != null && game.Object.IsValid;

    private bool TryMe(out PieceColor me)
    {
        me = PieceColor.white;
        return game != null && game.TryGetLocalColor(out me);
    }

    private void RefreshHud()
    {
        if (game != null && turnText != null) turnText.text = string.Format(turnFormat, game.TurnNumber);

        if (TryMe(out PieceColor me))
        {
            PieceColor opp = NetworkGameM.Opposite(me);
            SetBan(myPromotionText, me, DealRule.Promotion);
            SetBan(myEnPassantText, me, DealRule.EnPassant);
            SetBan(myCastlingText, me, DealRule.Castling);
            SetBan(oppPromotionText, opp, DealRule.Promotion);
            SetBan(oppEnPassantText, opp, DealRule.EnPassant);
            SetBan(oppCastlingText, opp, DealRule.Castling);
        }

        if (undoM != null && (hudMyPowerText != null || hudOppPowerText != null)
            && undoM.GetPowers(out float myP, out float oppP, out _))
        {
            if (hudMyPowerText != null) hudMyPowerText.text = myP.ToString("0");
            if (hudOppPowerText != null) hudOppPowerText.text = oppP.ToString("0");
        }

        if (openButton != null)
            openButton.interactable = _mode == Mode.Closed && undoM != null && undoM.CanPropose(out _);
    }

    private static void SetBan(TMP_Text t, PieceColor color, DealRule rule)
    {
        if (t == null) return;
        int r = RuleBans.RemainingTurns(color, rule);
        t.text = r < 0 ? "∞" : r.ToString();
    }

    private void RefreshPanel()
    {
        if (undoM == null) return;

        // 내 제안을 만드는 중에 내 차례가 아니게 되면(시간 초과 등) 닫습니다.
        if (_mode == Mode.Compose && game != null && (!game.IsLocalTurn || game.State != GameState.Playing))
        {
            CloseCompose();
            return;
        }

        if (undoM.GetPowers(out float my, out float opp, out float chance))
        {
            if (powerText != null) powerText.text = $"나의 협상력 : ({my:0}) VS ({opp:0}) : 상대 협상력";
            float shown = _mode == Mode.Respond ? _shownChance : chance;
            if (chanceText != null) chanceText.text = $"{Mathf.RoundToInt(shown)}%";
        }

        if (maxText != null) maxText.text = undoM.MaxUndoable().ToString();

        if (_mode == Mode.Compose)
        {
            int max = undoM.MaxUndoable();
            _count = max <= 0 ? 0 : Mathf.Clamp(_count, 1, max);
            BuildDealFromSlots();

            bool ok = undoM.Validate(_deal, out string reason);
            if (proposeButton != null) proposeButton.interactable = ok;
            if (reasonText != null)
                reasonText.text = ok ? $"조건 {undoM.ComputeCredit(_deal)}/{_deal.undoCount}수" : reason;

            RefreshAddButtons();
        }
        else if (_mode == Mode.Waiting || _mode == Mode.Respond)
        {
            if (timerText != null) timerText.text = $"{undoM.RemainingSeconds:0}";
            if (reasonText != null) reasonText.text = "";
        }

        if (countText != null) countText.text = _count.ToString();
    }

    private void RefreshAddButtons()
    {
        if (!TryMe(out PieceColor me)) return;

        SetAddInteractable(promotionButton, !_ruleSlots.ContainsKey(DealRule.Promotion) && !RuleBans.IsBanned(me, DealRule.Promotion));
        SetAddInteractable(enPassantButton, !_ruleSlots.ContainsKey(DealRule.EnPassant) && !RuleBans.IsBanned(me, DealRule.EnPassant));
        SetAddInteractable(castlingButton, !_ruleSlots.ContainsKey(DealRule.Castling) && !RuleBans.IsBanned(me, DealRule.Castling));

        SetAddInteractable(pawnButton, !_pieceSlots.ContainsKey(pieceType.Pawn));
        SetAddInteractable(knightButton, !_pieceSlots.ContainsKey(pieceType.Knight));
        SetAddInteractable(rookButton, !_pieceSlots.ContainsKey(pieceType.Rook));
        SetAddInteractable(bishopButton, !_pieceSlots.ContainsKey(pieceType.Bishop));
        SetAddInteractable(queenButton, !_pieceSlots.ContainsKey(pieceType.Queen));
    }

    private static void SetAddInteractable(Button b, bool on)
    {
        if (b != null) b.interactable = on;
    }

    // ───────────── 열기 / 닫기 ─────────────

    private void OpenCompose()
    {
        if (!NetReady) return;
        if (!undoM.CanPropose(out string reason)) { Toast(reason); return; }

        ClearSlots();
        _count = Mathf.Min(1, undoM.MaxUndoable());
        SetMode(Mode.Compose);
        RefreshPanel();
    }

    private void CloseCompose()
    {
        ClearSlots();
        SetMode(Mode.Closed);
    }

    private void SetMode(Mode mode)
    {
        _modeSince = Time.unscaledTime;
        _mode = mode;
        if (panel != null) panel.SetActive(mode != Mode.Closed);
        ApplyModeObjects();

        bool editable = mode == Mode.Compose;
        foreach (UndoSlotView s in _ruleSlots.Values) s.SetInteractable(editable);
        foreach (UndoSlotView s in _pieceSlots.Values) s.SetInteractable(editable);
    }

    private void ApplyModeObjects()
    {
        SetAll(showWhenComposing, _mode == Mode.Compose);
        SetAll(showWhenWaiting, _mode == Mode.Waiting);
        SetAll(showWhenResponding, _mode == Mode.Respond);
        if (timerText != null) timerText.gameObject.SetActive(_mode == Mode.Waiting || _mode == Mode.Respond);
    }

    private static void SetAll(GameObject[] list, bool on)
    {
        if (list == null) return;
        foreach (GameObject g in list) if (g != null) g.SetActive(on);
    }

    // ───────────── 편집 (Compose) ─────────────

    private void AddCount(int delta)
    {
        if (_mode != Mode.Compose || undoM == null) return;
        int max = undoM.MaxUndoable();
        _count = max <= 0 ? 0 : Mathf.Clamp(_count + delta, 1, max);
        RefreshPanel();
    }

    private void AddRuleSlot(DealRule rule)
    {
        if (_mode != Mode.Compose || _ruleSlots.ContainsKey(rule) || slotPrefab == null) return;

        UndoSlotView slot = Instantiate(slotPrefab, slotParent);
        slot.Setup(RuleLabel(rule), 3, 0, maxRuleTurns, TurnsText, s => RefreshPanel(), s => RemoveRuleSlot(rule));
        _ruleSlots.Add(rule, slot);
        RefreshPanel();
    }

    private void AddPieceSlot(pieceType type)
    {
        if (_mode != Mode.Compose || _pieceSlots.ContainsKey(type) || slotPrefab == null) return;

        UndoSlotView slot = Instantiate(slotPrefab, slotParent);
        slot.Setup(PieceLabel(type), 1, 1, maxPieceCount, v => v.ToString(), s => RefreshPanel(), s => RemovePieceSlot(type));
        _pieceSlots.Add(type, slot);
        RefreshPanel();
    }

    private void RemoveRuleSlot(DealRule rule)
    {
        if (_ruleSlots.TryGetValue(rule, out UndoSlotView s)) { Destroy(s.gameObject); _ruleSlots.Remove(rule); }
        RefreshPanel();
    }

    private void RemovePieceSlot(pieceType type)
    {
        if (_pieceSlots.TryGetValue(type, out UndoSlotView s)) { Destroy(s.gameObject); _pieceSlots.Remove(type); }
        RefreshPanel();
    }

    private void ClearSlots()
    {
        foreach (UndoSlotView s in _ruleSlots.Values) if (s != null) Destroy(s.gameObject);
        foreach (UndoSlotView s in _pieceSlots.Values) if (s != null) Destroy(s.gameObject);
        _ruleSlots.Clear();
        _pieceSlots.Clear();
    }

    private void BuildDealFromSlots()
    {
        _deal = new UndoDeal { undoCount = _count };
        foreach (KeyValuePair<DealRule, UndoSlotView> kv in _ruleSlots)
            _deal.rules.Add(new DealRuleChip { rule = kv.Key, turns = kv.Value.Value });
        foreach (KeyValuePair<pieceType, UndoSlotView> kv in _pieceSlots)
            _deal.pieceChips.Add(new DealPieceChip { type = kv.Key, count = kv.Value.Value });
    }

    private void Propose()
    {
        if (_mode != Mode.Compose || undoM == null) return;
        BuildDealFromSlots();
        undoM.Propose(_deal);   // 통과하면 ProposalSent, 막히면 Denied 이벤트
    }

    private void Answer(bool accept)
    {
        if (_mode != Mode.Respond || undoM == null) return;
        undoM.Respond(accept);
        SetMode(Mode.Waiting);   // 결과가 올 때까지 버튼 숨김
    }

    // ───────────── 이벤트 ─────────────

    private void OnProposalSent(NetworkUndoM.UndoProposal p)
    {
        ShowReadOnly(p, Mode.Waiting);
    }

    private void OnProposalReceived(NetworkUndoM.UndoProposal p)
    {
        _shownChance = p.chanceIfRejected;
        ShowReadOnly(p, Mode.Respond);
    }

    private void ShowReadOnly(NetworkUndoM.UndoProposal p, Mode mode)
    {
        ClearSlots();
        _count = p.deal.undoCount;

        SetMode(mode);   // 먼저 모드를 정하고 슬롯을 만든 뒤 읽기 전용으로 바꿉니다.

        foreach (DealRuleChip chip in p.deal.rules)
        {
            if (slotPrefab == null) break;
            UndoSlotView slot = Instantiate(slotPrefab, slotParent);
            slot.Setup(RuleLabel(chip.rule), chip.turns, 0, 100, TurnsText, null, null);
            slot.SetInteractable(false);
            _ruleSlots[chip.rule] = slot;
        }
        foreach (DealPieceChip chip in p.deal.pieceChips)
        {
            if (slotPrefab == null) break;
            UndoSlotView slot = Instantiate(slotPrefab, slotParent);
            slot.Setup(PieceLabel(chip.type), chip.count, 0, 16, v => v.ToString(), null, null);
            slot.SetInteractable(false);
            _pieceSlots[chip.type] = slot;
        }
        RefreshPanel();
    }

    private void OnResolved(DealResult r)
    {
        bool iAmMover = TryMe(out PieceColor me) && me == r.mover;
        string msg;

        if (r.success)
        {
            msg = iAmMover
                ? (r.response == DealResponse.Accepted ? "상대가 허락했어요! 무르기 성공" : $"거절당했지만 협상에 성공했어요! ({Mathf.RoundToInt(r.chance)}%)")
                : (r.response == DealResponse.Accepted ? "무르기를 허락했어요." : "거절했지만 상대의 협상이 통했어요.");
        }
        else
        {
            switch (r.response)
            {
                case DealResponse.TimedOut: msg = iAmMover ? "상대가 시간 안에 답하지 않아 무르기가 무산됐어요." : "시간이 지나 무르기가 무산됐어요."; break;
                case DealResponse.Cancelled: msg = "협상이 취소됐어요."; break;
                default: msg = iAmMover ? $"무르기 협상 실패... ({Mathf.RoundToInt(r.chance)}%)" : "거절이 받아들여졌어요."; break;
            }
        }

        ClearSlots();
        SetMode(Mode.Closed);
        Toast(msg);
    }

    private void OnDenied(string reason) => Toast(reason);

    // ───────────── 글자 ─────────────

    private void Toast(string msg)
    {
        if (toastText == null || string.IsNullOrEmpty(msg)) return;
        toastText.text = msg;
        toastText.gameObject.SetActive(true);
        _toastUntil = Time.unscaledTime + toastSeconds;
    }

    private static string TurnsText(int turns) => turns <= 0 ? "전판" : $"{turns}턴";

    private static string RuleLabel(DealRule rule)
    {
        switch (rule)
        {
            case DealRule.Promotion: return "프로모션 금지";
            case DealRule.EnPassant: return "앙파상 금지";
            default: return "캐슬링 금지";
        }
    }

    private static string PieceLabel(pieceType type)
    {
        switch (type)
        {
            case pieceType.Pawn: return "폰 제거";
            case pieceType.Knight: return "나이트 제거";
            case pieceType.Rook: return "룩 제거";
            case pieceType.Bishop: return "비숍 제거";
            default: return "퀸 제거";
        }
    }
}