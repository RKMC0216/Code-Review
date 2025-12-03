using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Offline WPA 8-Ball turn manager (Player vs AI).
/// Key points:
/// - Table stays OPEN until a post-break shot legally pockets exactly ONE group.
/// - First-hit does NOT assign groups on open table (except 8-first is a foul).
/// - Group assignment happens at end-of-shot resolve, only if !foul and XOR(one group).
/// - BIH message tells player to <b>click and drag</b> the cue ball.
/// - Prevents 8-ball icon flicker before match end.
/// </summary>
public class TurnManager8Ball : MonoBehaviour
{
    [Header("Scene Refs")]
    public CustomCueBall cueBall;
    public GameObject playerCueRoot;
    public CustomTrajectory trajectory;
    public SimpleAIOpponent ai;               // optional
    public CueStickController cueController;
    public CueBallPlacement ballInHand;       // player BIH helper
    public Image turnTimerImage;

    [Header("Objects")]
    public string ballTag = "Ball";
    public CustomCueBall eightBall;
    public Transform footSpot;
    public Transform cueBallResetPoint;

    [Header("Timer / Policies")]
    public bool useTurnTimer = true;
    public float turnTimeSeconds = 30f;
    public bool showTimerOnlyOnPlayersTurn = false;

    public enum IllegalBreakPolicy { AcceptTable, Rerack_OpponentBreaks, Rerack_SameShooterBreaks }
    public IllegalBreakPolicy illegalBreakPolicy = IllegalBreakPolicy.AcceptTable;

    public enum EightOnBreakPolicy { RespotAndContinue, Rebreak }
    public EightOnBreakPolicy eightOnBreak_NoFoul = EightOnBreakPolicy.RespotAndContinue;
    public EightOnBreakPolicy eightOnBreak_WithScratch = EightOnBreakPolicy.RespotAndContinue;

    [Header("Groups")]
    public bool useOpenTableRules = true;

    [Header("Call Shot")]
    public bool requireCallOnEight = true;
    [Tooltip("Set TRUE from your 'called pocket' UI when player/AI calls and makes the 8.")]
    public bool lastShotCalledPocketOk = true;

    [Header("Kitchen / BIH")]
    public bool restrictBIHToKitchenOnBreakScratch = true;
    public KitchenConstraint kitchen;

    [Header("Rule Indicator UI")]
    public GameObject ruleIndicatorPanel;
    public TMP_Text ruleIndicatorText;
    public float ruleIndicatorDefaultSeconds = 2.5f;

    // =====================  BALL UI =====================
    [System.Serializable] public enum Group { Unknown, Solids, Stripes }

    [Header("Ball UI (top rows)")]
    [SerializeField] private RectTransform leftSlot;   // Player-side slot
    [SerializeField] private RectTransform rightSlot;  // AI-side slot
    [SerializeField] private GameObject solidsBar;
    [SerializeField] private GameObject stripesBar;
    [SerializeField] private GameObject playerFrameGlow;
    [SerializeField] private GameObject aiFrameGlow;
    [SerializeField] private bool hideBarsOnStart = true;

    [Header("8-Ball Winner Icons (Group-Based)")]
    [Tooltip("Drag the 8-ball icon that sits inside the SOLIDS bar.")]
    public Image solidsEightIcon;
    [Tooltip("Drag the 8-ball icon that sits inside the STRIPES bar.")]
    public Image stripesEightIcon;
    [Tooltip("Lit color (winner). Leave alpha 1.")]
    public Color eightLitColor = Color.white;
    [Tooltip("Dim color (loser/off). Use low alpha if you want it barely visible.")]
    public Color eightDimColor = new Color(1, 1, 1, 0.15f);

    [Header("Win/Loss Panel")]
    public GameObject winLossPanel;
    public TMP_Text winLossTMP;
    public string winHeadline = "YOU WIN!";
    public string loseHeadline = "YOU LOSE!";
    public Color winColor = new Color(0.15f, 0.85f, 0.3f, 1f);
    public Color loseColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("House Rule")]
    [Tooltip("If true: if you pocket ANY opponent ball on your shot, your turn ENDS (even if you also pocket your own).")]
    public bool passTurnIfOpponentPocketed = true;

    // ===== Internal State =====
    enum Shooter { Player, AI }
    Shooter currentShooter;
    public bool playerStarts = true;
    public float pollInterval = 0.25f;

    // Group assignment
    Group playerGroup = Group.Unknown;
    Group aiGroup = Group.Unknown;
    bool groupsLocked = false;
    bool isBreakShotOfRack = true;

    // Per-shot trackers
    bool waitingForShot = false;
    bool cueBallContacted = false;
    int firstHitBallNumber = -1;
    bool cushionAfterContact = false;
    int ballsPocketedThisShot = 0;
    int shotPocketedSolids = 0;
    int shotPocketedStripes = 0;
    bool shooterScratchedThisShot = false;
    bool eightDownThisShot = false;

    int solidsDown = 0;
    int stripesDown = 0;

    // BIH pending for each side (player BIH uses CueBallPlacement; AI BIH handled here)
    bool aiBIHPending = false;

    // Break analytics: distinct object balls that contacted cushions during break
    private readonly System.Collections.Generic.HashSet<int> _breakCushionHitBalls = new System.Collections.Generic.HashSet<int>();

    // Control
    Coroutine turnFlow, timerFlow, uiHideFlow;
    bool resolving = false;

    // Winner lock
    bool matchEnded = false;
    Group winnerGroupLocked = Group.Unknown;

    // UI cache
    private Group _playerGroupUI = Group.Unknown;
    private Group _aiGroupUI = Group.Unknown;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!cueBall) cueBall = FindAnyObjectByType<CustomCueBall>();
        if (!trajectory) trajectory = FindAnyObjectByType<CustomTrajectory>();
        if (!cueController) cueController = FindAnyObjectByType<CueStickController>();
        if (!ai) ai = FindAnyObjectByType<SimpleAIOpponent>();
        if (!ballInHand) ballInHand = FindAnyObjectByType<CueBallPlacement>();
#else
        if (!cueBall) cueBall = FindObjectOfType<CustomCueBall>();
        if (!trajectory) trajectory = FindObjectOfType<CustomTrajectory>();
        if (!cueController) cueController = FindObjectOfType<CueStickController>();
        if (!ai) ai = FindObjectOfType<SimpleAIOpponent>();
        if (!ballInHand) ballInHand = FindObjectOfType<CueBallPlacement>();
#endif

        if (!eightBall)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(ballTag))
            {
                var b = go.GetComponent<CustomCueBall>();
                if (b && b.ballNumber == 8) { eightBall = b; break; }
            }
        }

        if (ruleIndicatorPanel) ruleIndicatorPanel.SetActive(false);
        if (winLossPanel) winLossPanel.SetActive(false);

        InitializeBallUI();

        // Pre-locked groups if open table disabled
        if (!useOpenTableRules)
        {
            playerGroup = Group.Solids;
            aiGroup = Group.Stripes;
            groupsLocked = true;
            SetPlayerGroupUI(playerGroup);
        }

        // Ensure 8-ball icons start dim/off
        ForceDimBothEightIcons();
    }

    void OnEnable()
    {
        CustomCueBall.OnBallPocketed += OnBallPocketed;
        CustomCueBall.OnCueScratch += OnCueScratch;
        CustomCueBall.OnAnyCushionContact += OnCushionContact;
        CustomCueBall.OnFirstHitBall += OnFirstHitBall;
    }

    void OnDisable()
    {
        CustomCueBall.OnBallPocketed -= OnBallPocketed;
        CustomCueBall.OnCueScratch -= OnCueScratch;
        CustomCueBall.OnAnyCushionContact -= OnCushionContact;
        CustomCueBall.OnFirstHitBall -= OnFirstHitBall;
    }

    void Start()
    {
        currentShooter = playerStarts ? Shooter.Player : Shooter.AI;

        // Open table at rack start if enabled
        if (useOpenTableRules)
        {
            playerGroup = Group.Unknown;
            aiGroup = Group.Unknown;
            groupsLocked = false;
        }

        StartTurnFor(currentShooter);
        ShowRuleMessage(playerStarts ? "Your break" : "Opponent's break", 1.4f);

        ForceDimBothEightIcons();
    }

    // Keep winner icons DIM while rack is active to prevent any transient "on" frames.
    void LateUpdate()
    {
        if (!matchEnded)
        {
            if (solidsEightIcon) solidsEightIcon.color = eightDimColor;
            if (stripesEightIcon) stripesEightIcon.color = eightDimColor;
        }
    }

    // ===== Ball UI =====
    void InitializeBallUI()
    {
        if (!leftSlot || !rightSlot) return;
        if (hideBarsOnStart)
        {
            if (solidsBar) solidsBar.SetActive(false);
            if (stripesBar) stripesBar.SetActive(false);
        }
        if (playerFrameGlow) playerFrameGlow.SetActive(false);
        if (aiFrameGlow) aiFrameGlow.SetActive(false);
    }

    public void SetPlayerGroupUI(Group playerGroupNew)
    {
        if (matchEnded) return;
        _playerGroupUI = playerGroupNew;
        _aiGroupUI = (playerGroupNew == Group.Solids) ? Group.Stripes :
                     (playerGroupNew == Group.Stripes) ? Group.Solids : Group.Unknown;
        ApplyBallUI();
    }

    void ApplyBallUI()
    {
        if (matchEnded) return;
        if (!leftSlot || !rightSlot || !solidsBar || !stripesBar) return;

        if (_playerGroupUI == Group.Unknown || _aiGroupUI == Group.Unknown)
        {
            solidsBar.SetActive(false);
            stripesBar.SetActive(false);
            if (playerFrameGlow) playerFrameGlow.SetActive(false);
            if (aiFrameGlow) aiFrameGlow.SetActive(false);
            return;
        }

        if (playerFrameGlow) playerFrameGlow.SetActive(true);
        if (aiFrameGlow) aiFrameGlow.SetActive(true);

        GameObject leftBar = (_playerGroupUI == Group.Solids) ? solidsBar : stripesBar;
        GameObject rightBar = (_playerGroupUI == Group.Solids) ? stripesBar : solidsBar;

        Place(leftBar, leftSlot);
        Place(rightBar, rightSlot);

        solidsBar.SetActive(true);
        stripesBar.SetActive(true);
    }

    static void Place(GameObject bar, RectTransform slot)
    {
        var rt = bar.transform as RectTransform;
        rt.SetParent(slot, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();
    }

    // ===== Events from balls =====
    void OnFirstHitBall(int number)
    {
        if (firstHitBallNumber < 0) firstHitBallNumber = number;
        cueBallContacted = true;
    }

    void OnCushionContact() => cushionAfterContact = true;

    void OnBallPocketed(int number)
    {
        if (!waitingForShot || matchEnded) return;
        if (number == 8) { eightDownThisShot = true; return; }

        ballsPocketedThisShot++;
        if (number >= 1 && number <= 7) { solidsDown++; shotPocketedSolids++; }
        if (number >= 9 && number <= 15) { stripesDown++; shotPocketedStripes++; }
    }

    void OnCueScratch()
    {
        if (matchEnded) return;
        shooterScratchedThisShot = true;

        // Respawn cue ball immediately so table never looks empty
        if (cueBall)
        {
            Vector3 resp = cueBallResetPoint ? cueBallResetPoint.position
                                             : new Vector3(0f, cueBall.transform.position.y, 0f);
            cueBall.RespawnAt(resp);
            cueBall.ForceStopNow();
        }

        // BIH assignment to incoming player
        if (currentShooter == Shooter.Player)
        {
            aiBIHPending = true;
            ShowRuleMessage("Foul: Scratch — Opponent has ball in hand", 2.2f);
        }
        else
        {
            if (ballInHand) ballInHand.SetBallInHandPending(true);
            ShowRuleMessage("Foul: Scratch — You have ball in hand next", 2.2f);
        }

        // If shot wasn't started, force a resolve so the turn can progress
        if (!waitingForShot)
        {
            BeginShot();
            if (currentShooter == Shooter.Player)
                SafeStart(ref turnFlow, ResolveAfterPlayer());
            else
                SafeStart(ref turnFlow, ResolveAfterAI());
        }
    }

    // ===== External hooks (call right after your shoot function) =====
    public void OnPlayerShotFired()
    {
        if (waitingForShot || matchEnded) return;
        currentShooter = Shooter.Player;
        BeginShot();
        SafeStart(ref turnFlow, ResolveAfterPlayer());
    }

    public void OnAIShotFired()
    {
        if (waitingForShot || matchEnded) return;
        currentShooter = Shooter.AI;
        BeginShot();
        SafeStart(ref turnFlow, ResolveAfterAI());
    }

    // ===== Shot lifecycle =====
    void BeginShot()
    {
        waitingForShot = true;
        resolving = false;

        StopTurnTimer();
        ApplyTurnVisibility(false);

        // reset per-shot
        cueBallContacted = false;
        firstHitBallNumber = -1;
        cushionAfterContact = false;
        ballsPocketedThisShot = 0;
        shotPocketedSolids = 0;
        shotPocketedStripes = 0;
        shooterScratchedThisShot = false;
        eightDownThisShot = false;
    }

    IEnumerator ResolveAfterPlayer()
    {
        yield return WaitTableIdle();
        if (!BeginResolveOnce() || matchEnded) yield break;
        waitingForShot = false;

        if (isBreakShotOfRack)
        {
            PostBreakResolution(shooterWasPlayer: true);
            yield break;
        }

        bool foul = EvaluateFoul(currentShooter);
        if (shooterScratchedThisShot) foul = true;

        if (!foul) TryAssignGroupsAfterCleanShot(shooterWasPlayer: true);

        bool lose8 = EvaluateEightLoss(currentShooter, foul);
        bool win8 = !lose8 && EvaluateEightWin(currentShooter, foul);

        if (lose8) { FinalizeWinnerUI(GetOpponentGroup(true), playerWon: false, reason: "8-ball pocketed early / foul / wrong pocket"); yield break; }
        if (win8) { FinalizeWinnerUI(playerGroup, playerWon: true, reason: "Legal 8-ball pocket"); yield break; }

        bool keep = KeepsTable(currentShooter, foul);
        StartTurnFor(keep ? Shooter.Player : Shooter.AI);
    }

    IEnumerator ResolveAfterAI()
    {
        yield return WaitTableIdle();
        if (!BeginResolveOnce() || matchEnded) yield break;
        waitingForShot = false;

        if (isBreakShotOfRack)
        {
            PostBreakResolution(shooterWasPlayer: false);
            yield break;
        }

        bool foul = EvaluateFoul(currentShooter);
        if (shooterScratchedThisShot) foul = true;

        if (!foul) TryAssignGroupsAfterCleanShot(shooterWasPlayer: false);

        bool lose8 = EvaluateEightLoss(currentShooter, foul);
        bool win8 = !lose8 && EvaluateEightWin(currentShooter, foul);

        if (lose8) { FinalizeWinnerUI(playerGroup, playerWon: true, reason: "Opponent lost on the 8-ball"); yield break; }
        if (win8) { FinalizeWinnerUI(aiGroup, playerWon: false, reason: "Opponent pocketed the 8-ball legally"); yield break; }

        bool keep = KeepsTable(currentShooter, foul);
        StartTurnFor(keep ? Shooter.AI : Shooter.Player);
    }

    Group GetOpponentGroup(bool shooterWasPlayer)
    {
        return shooterWasPlayer ? aiGroup : playerGroup;
    }

    // ===== Rule evaluation =====

    void PostBreakResolution(bool shooterWasPlayer)
    {
        if (matchEnded) return;

        bool breakLegal = (ballsPocketedThisShot > 0) || CushionEstimateAtLeast4();

        if (eightDownThisShot)
        {
            if (!shooterScratchedThisShot)
            {
                if (eightOnBreak_NoFoul == EightOnBreakPolicy.Rebreak)
                { ShowRuleMessage("8 on the break — Re-rack", 2.2f); ClearBreakCushionTracker(); Rerack(nextBreaker: shooterWasPlayer ? Shooter.Player : Shooter.AI); return; }

                RespotEight();
                isBreakShotOfRack = false;
                ClearBreakCushionTracker();
                StartTurnFor(shooterWasPlayer ? Shooter.Player : Shooter.AI);
                ShowRuleMessage("8 on the break — Spotted. Shooter continues.", 2.4f);
                return;
            }
            else
            {
                if (eightOnBreak_WithScratch == EightOnBreakPolicy.Rebreak)
                { ShowRuleMessage("8 on the break with scratch — Re-rack", 2.2f); ClearBreakCushionTracker(); Rerack(nextBreaker: shooterWasPlayer ? Shooter.Player : Shooter.AI); return; }

                RespotEight();
                isBreakShotOfRack = false;
                ClearBreakCushionTracker();

                if (shooterWasPlayer)
                {
                    aiBIHPending = true;
                    ShowRuleMessage("Scratch on break — Opponent has ball in hand", 2.2f);
                    StartTurnFor(Shooter.AI);
                }
                else
                {
                    if (ballInHand) ballInHand.SetBallInHandPending(true);
                    ShowRuleMessage("Opponent scratched on break — Ball in hand", 2.2f);
                    StartTurnFor(Shooter.Player);
                }
                return;
            }
        }

        if (!breakLegal)
        {
            switch (illegalBreakPolicy)
            {
                case IllegalBreakPolicy.Rerack_OpponentBreaks:
                    ShowRuleMessage("Illegal break — Re-rack (opponent breaks)", 2.2f);
                    ClearBreakCushionTracker();
                    Rerack(nextBreaker: shooterWasPlayer ? Shooter.AI : Shooter.Player);
                    return;
                case IllegalBreakPolicy.Rerack_SameShooterBreaks:
                    ShowRuleMessage("Illegal break — Re-rack (same shooter)", 2.2f);
                    ClearBreakCushionTracker();
                    Rerack(nextBreaker: shooterWasPlayer ? Shooter.Player : Shooter.AI);
                    return;
                default:
                    isBreakShotOfRack = false;
                    ClearBreakCushionTracker();
                    ShowRuleMessage("Illegal break — Turn passes", 1.8f);
                    StartTurnFor(shooterWasPlayer ? Shooter.AI : Shooter.Player);
                    return;
            }
        }

        // Legal break (with/without pocket)
        isBreakShotOfRack = false;
        ClearBreakCushionTracker();

        if (ballsPocketedThisShot > 0 && !shooterScratchedThisShot)
        {
            StartTurnFor(shooterWasPlayer ? Shooter.Player : Shooter.AI);
            ShowRuleMessage("Legal break — Shooter continues", 1.6f);
        }
        else
        {
            StartTurnFor(shooterWasPlayer ? Shooter.AI : Shooter.Player);
            ShowRuleMessage("Legal break — Turn passes", 1.4f);
        }
    }

    // Legal-break cushion count (≥4 distinct object balls). Falls back to old flag if not wired.
    bool CushionEstimateAtLeast4()
    {
        if (_breakCushionHitBalls.Count > 0)
            return _breakCushionHitBalls.Count >= 4;
        return cushionAfterContact;
    }
    void ClearBreakCushionTracker() => _breakCushionHitBalls.Clear();

    // Call this from object-ball scripts when they hit a cushion DURING THE BREAK.
    public void NotifyObjectBallCushion(int ballNumber)
    {
        if (!isBreakShotOfRack) return;
        if (ballNumber <= 0 || ballNumber == 8 || ballNumber > 15) return; // only 1..7,9..15
        _breakCushionHitBalls.Add(ballNumber);
    }

    bool EvaluateFoul(Shooter s)
    {
        if (!cueBallContacted)
        {
            ShowRuleMessage(s == Shooter.Player ? "Foul: No contact — Opponent has ball in hand"
                                                : "Foul: No contact — You have ball in hand", 2.2f);
            return MarkFoul(s);
        }

        Group gShooter = (s == Shooter.Player) ? playerGroup : aiGroup;
        bool openTable = useOpenTableRules && !groupsLocked;
        bool legallyOnEight = ClearedGroup(gShooter);

        // Open table: hitting the 8 first is a foul (WPA)
        if (openTable && firstHitBallNumber == 8)
        {
            ShowRuleMessage(s == Shooter.Player
                ? "Foul: 8-ball first on open table — Opponent has ball in hand"
                : "Foul: 8-ball first on open table — You have ball in hand", 2.2f);
            return MarkFoul(s);
        }

        // Once groups are locked and you’re not on the 8 yet, must contact your own group first.
        if (!openTable && !legallyOnEight)
        {
            if (!IsBallInGroup(firstHitBallNumber, gShooter))
            {
                ShowRuleMessage(s == Shooter.Player ? "Foul: Wrong ball first — Opponent has ball in hand"
                                                    : "Foul: Wrong ball first — You have ball in hand", 2.2f);
                return MarkFoul(s);
            }
        }

        // No rail after contact (unless a ball was pocketed)
        if (!cushionAfterContact && ballsPocketedThisShot == 0)
        {
            ShowRuleMessage(s == Shooter.Player ? "Foul: No rail after contact — Opponent has ball in hand"
                                                : "Foul: No rail after contact — You have ball in hand", 2.2f);
            return MarkFoul(s);
        }

        if (shooterScratchedThisShot) return MarkFoul(s);

        return false;
    }

    // ======= Assign groups ONLY when exactly one group was pocketed (and no foul) =======
    void TryAssignGroupsAfterCleanShot(bool shooterWasPlayer)
    {
        if (!useOpenTableRules || groupsLocked || isBreakShotOfRack) return;

        bool pocketedSolids = shotPocketedSolids > 0;
        bool pocketedStripes = shotPocketedStripes > 0;

        // If both groups pocketed, table stays open (no assignment).
        if (pocketedSolids && pocketedStripes)
        {
            ShowRuleMessage("Both groups pocketed on open table — Table remains open.", 2.0f);
            return;
        }

        // If neither group was pocketed, no assignment.
        if (!(pocketedSolids ^ pocketedStripes)) return;

        // Exactly one group fell: assign to the shooter regardless of first-hit.
        Group chosen = pocketedSolids ? Group.Solids : Group.Stripes;

        if (shooterWasPlayer)
        {
            playerGroup = chosen;
            aiGroup = (chosen == Group.Solids) ? Group.Stripes : Group.Solids;
            ShowRuleMessage($"Groups assigned: You are <b>{playerGroup}</b>.", 2.0f);
        }
        else
        {
            aiGroup = chosen;
            playerGroup = (chosen == Group.Solids) ? Group.Stripes : Group.Solids;
            ShowRuleMessage($"Groups assigned: Opponent is <b>{aiGroup}</b>.", 2.0f);
        }

        groupsLocked = true;
        SetPlayerGroupUI(playerGroup);
    }

    bool EvaluateEightLoss(Shooter s, bool foul)
    {
        if (!eightDownThisShot || isBreakShotOfRack) return false;

        Group gShooter = (s == Shooter.Player) ? playerGroup : aiGroup;
        bool cleared = ClearedGroup(gShooter);

        if (!cleared) { ShowRuleMessage("Loss: 8-ball pocketed early", 2.6f); return true; }
        if (foul) { ShowRuleMessage("Loss: Foul while pocketing 8-ball", 2.6f); return true; }
        if (requireCallOnEight && !lastShotCalledPocketOk)
        {
            ShowRuleMessage("Loss: 8-ball in wrong pocket (call required)", 2.6f);
            return true;
        }
        return false;
    }

    bool EvaluateEightWin(Shooter s, bool foul)
    {
        if (!eightDownThisShot || isBreakShotOfRack) return false;

        Group gShooter = (s == Shooter.Player) ? playerGroup : aiGroup;
        bool cleared = ClearedGroup(gShooter);

        return cleared && !foul && (!requireCallOnEight || lastShotCalledPocketOk);
    }

    bool KeepsTable(Shooter s, bool foul)
    {
        if (foul) return false;

        if (!groupsLocked) // open table: any pocket keeps (except scratch already handled)
            return ballsPocketedThisShot > 0 && !shooterScratchedThisShot;

        Group gShooter = (s == Shooter.Player) ? playerGroup : aiGroup;

        int own = (gShooter == Group.Solids) ? shotPocketedSolids : shotPocketedStripes;
        int opp = (gShooter == Group.Solids) ? shotPocketedStripes : shotPocketedSolids;

        if (passTurnIfOpponentPocketed && opp > 0) return false;

        return own > 0 && !shooterScratchedThisShot;
    }

    bool ClearedGroup(Group g)
    {
        if (g == Group.Solids) return solidsDown >= 7;
        if (g == Group.Stripes) return stripesDown >= 7;
        return false;
    }

    bool IsBallInGroup(int n, Group g)
    {
        if (n <= 0 || n == 8 || n > 15) return false;
        if (g == Group.Solids) return n >= 1 && n <= 7;
        if (g == Group.Stripes) return n >= 9 && n <= 15;
        return false;
    }

    bool MarkFoul(Shooter s)
    {
        if (s == Shooter.Player) aiBIHPending = true;
        else if (ballInHand) ballInHand.SetBallInHandPending(true);
        return true;
    }

    // ===== Turn transitions =====

    void StartTurnFor(Shooter s)
    {
        if (matchEnded) return;

        if (turnFlow != null) { StopCoroutine(turnFlow); turnFlow = null; }
        StopTurnTimer();

        waitingForShot = false;
        resolving = false;
        currentShooter = s;

        cueController?.ForceExitBallInHandLocal();

        if (s == Shooter.Player)
        {
            ApplyTurnVisibility(true);
            cueController?.EnablePlayerTurn();

            if (ballInHand && ballInHand.HasPending())
            {
                bool kitchenNow = restrictBIHToKitchenOnBreakScratch && isBreakShotOfRack;
                ballInHand.EnableKitchenMode(kitchenNow);
                ballInHand.BeginBallInHand(cueBall.transform);
                ballInHand.SetBallInHandPending(false);
                ShowRuleMessage(
                    kitchenNow
                        ? "Ball in hand (kitchen) — <b>Click and drag</b> the cue ball within the kitchen."
                        : "Ball in hand — <b>Click and drag</b> the cue ball to place it.",
                    2.0f
                );
            }

            ResetTimerUI();
            if (useTurnTimer) StartTurnTimer(true);
        }
        else
        {
            ApplyTurnVisibility(false);
            cueController?.DisablePlayerTurn();

            if (aiBIHPending)
            {
                AI_PlaceCueBall();
                aiBIHPending = false;
                ShowRuleMessage(restrictBIHToKitchenOnBreakScratch && isBreakShotOfRack
                                ? "Opponent: Ball in hand (kitchen)"
                                : "Opponent: Ball in hand", 1.6f);
            }

            ResetTimerUI();
            if (useTurnTimer) StartTurnTimer(false);

            turnFlow = StartCoroutine(AITurnFlow());
        }
    }

    void AI_PlaceCueBall()
    {
        if (!cueBall) return;

        Vector3 pos = cueBall.transform.position;
        pos.y = cueBall.transform.position.y;

        bool kitchenOnly = restrictBIHToKitchenOnBreakScratch && isBreakShotOfRack;

        if (cueBallResetPoint) pos = cueBallResetPoint.position;
        else pos = new Vector3(0f, pos.y, 0f);

        if (kitchenOnly && kitchen != null && kitchen.zone != null)
        {
            pos = kitchen.ProjectInside(pos);
        }

        cueBall.gameObject.SetActive(true);
        cueBall.RespawnAt(pos);
        cueBall.ForceStopNow();
    }

    IEnumerator AITurnFlow()
    {
        yield return null;
        if (matchEnded) yield break;

        if (ai != null) ai.TakeShot();
        else
        {
            if (!TryFallbackShot())
            {
                StartTurnFor(Shooter.Player);
                yield break;
            }
        }

        const float planTimeout = 5f;
        float t = 0f;

        while (t < planTimeout)
        {
            if (waitingForShot || matchEnded) yield break;

            if (!TableIdle())
            {
                if (!waitingForShot)
                {
                    currentShooter = Shooter.AI;
                    BeginShot();
                    SafeStart(ref turnFlow, ResolveAfterAI());
                }
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (!waitingForShot && !matchEnded)
        {
            if (TryFallbackShot())
            {
                yield return null;
                currentShooter = Shooter.AI;
                BeginShot();
                SafeStart(ref turnFlow, ResolveAfterAI());
            }
            else
            {
                StartTurnFor(Shooter.Player);
            }
        }
    }

    // ===== Timer =====
    void StartTurnTimer(bool forPlayer)
    {
        if (!useTurnTimer || !turnTimerImage || matchEnded) return;
        if (showTimerOnlyOnPlayersTurn) turnTimerImage.gameObject.SetActive(forPlayer);
        SafeStart(ref timerFlow, TurnTimer(forPlayer));
    }

    void StopTurnTimer()
    {
        if (timerFlow != null) StopCoroutine(timerFlow);
        timerFlow = null;
    }

    IEnumerator TurnTimer(bool forPlayer)
    {
        float t = turnTimeSeconds;
        turnTimerImage.fillAmount = 1f;

        while (t > 0f && !matchEnded)
        {
            if (!TableIdle()) yield break; // shot started
            t -= Time.deltaTime;
            turnTimerImage.fillAmount = Mathf.Clamp01(t / turnTimeSeconds);
            yield return null;
        }

        if (matchEnded) yield break;

        if (forPlayer)
        {
            ShowRuleMessage("Shot clock violation — Turn passes", 2.0f);
            StartTurnFor(Shooter.AI);
        }
        else
        {
            ShowRuleMessage("Opponent: Shot clock violation — Your turn", 2.0f);
            StartTurnFor(Shooter.Player);
        }
    }

    void ResetTimerUI()
    {
        if (matchEnded) return;
        if (turnTimerImage)
        {
            turnTimerImage.fillAmount = 1f;
            if (!showTimerOnlyOnPlayersTurn) turnTimerImage.gameObject.SetActive(true);
        }
    }

    // ===== Utilities =====
    IEnumerator WaitTableIdle()
    {
        StopTurnTimer();
        while (!TableIdle() && !matchEnded) yield return new WaitForSeconds(pollInterval);
    }

    bool TableIdle()
    {
        if (cueBall != null) return !cueBall.IsAnyBallMovingByTag(ballTag);
        var balls = GameObject.FindGameObjectsWithTag(ballTag);
        foreach (var go in balls)
        {
            var b = go.GetComponent<CustomCueBall>();
            if (b != null && b.IsMoving()) return false;
        }
        return true;
    }

    bool BeginResolveOnce()
    {
        if (resolving) return false;
        resolving = true; return true;
    }

    void ApplyTurnVisibility(bool playerCanAim)
    {
        if (matchEnded)
        {
            if (playerCueRoot) playerCueRoot.SetActive(false);
            if (trajectory) trajectory.Hide();
            return;
        }
        if (playerCueRoot) playerCueRoot.SetActive(playerCanAim);
        if (!playerCanAim && trajectory) trajectory.Hide();
    }

    void SafeStart(ref Coroutine slot, IEnumerator routine)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    void RespotEight()
    {
        if (matchEnded) return;
        if (!eightBall || !footSpot) return;
        eightBall.gameObject.SetActive(true);
        eightBall.RespawnAt(footSpot.position);
        ShowRuleMessage("8-ball spotted", 1.4f);
        ForceDimBothEightIcons();
    }

    void Rerack(Shooter nextBreaker)
    {
        if (matchEnded) return;

        isBreakShotOfRack = true;
        ClearBreakCushionTracker();

        if (useOpenTableRules)
        {
            playerGroup = Group.Unknown;
            aiGroup = Group.Unknown;
            groupsLocked = false;
        }
        else
        {
            playerGroup = Group.Solids;
            aiGroup = Group.Stripes;
            groupsLocked = true;
        }

        ForceDimBothEightIcons();

        ShowRuleMessage("Re-rack", 1.6f);
        StartTurnFor(nextBreaker);
    }

    bool TryFallbackShot()
    {
        if (cueBall == null) return false;
        if (!cueBall.gameObject.activeInHierarchy) cueBall.gameObject.SetActive(true);

        var cueTr = cueBall.transform;
        var balls = GameObject.FindGameObjectsWithTag(ballTag);

        CustomCueBall target = null; float best = float.PositiveInfinity;
        foreach (var go in balls)
        {
            if (!go.activeInHierarchy) continue;
            var b = go.GetComponent<CustomCueBall>();
            if (b == null) continue;
            if (b.ballNumber == 0) continue; // skip cue ball
            float d2 = (b.transform.position - cueTr.position).sqrMagnitude;
            if (d2 < best) { best = d2; target = b; }
        }
        if (target == null) return false;

        Vector3 dir = target.transform.position - cueTr.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
        dir.Normalize();

        float power = Mathf.Lerp(4f, 9f, Mathf.Clamp01((Mathf.Sqrt(best) - 0.3f) / 2.5f));
        cueBall.Shoot(dir, power, Vector3.zero);
        return true;
    }

    // === Match End helpers ===
    void FinalizeWinnerUI(Group groupWon, bool playerWon, string reason)
    {
        if (matchEnded) return;

        winnerGroupLocked = groupWon;
        matchEnded = true;

        StopAllCoroutines();
        StopTurnTimer();
        ApplyTurnVisibility(false);
        trajectory?.Hide();

        // Light once and lock
        SetEightIconsByGroup(groupWon == Group.Solids, groupWon == Group.Stripes);

        ActivateWinLossPanel(playerWon, reason);
        ShowRuleMessage(reason, 2.5f);

        enabled = false; // prevent any new updates from elsewhere
    }

    void ActivateWinLossPanel(bool playerWon, string reasonLine)
    {
        if (!winLossPanel || !winLossTMP) return;

        winLossPanel.SetActive(true);
        string headline = playerWon ? winHeadline : loseHeadline;
        string detail = string.IsNullOrWhiteSpace(reasonLine) ? "" : $"\n<size=80%>{reasonLine}</size>";
        winLossTMP.text = $"<b>{headline}</b>{detail}";
        winLossTMP.color = playerWon ? winColor : loseColor;
    }

    void EndMatch(string message)
    {
        // Optional external call compatibility
        FinalizeWinnerUI(winnerGroupLocked != Group.Unknown ? winnerGroupLocked : Group.Solids, // default
                         false, message);
    }

    // === 8-ball UI (group-based) ===
    void ForceDimBothEightIcons()
    {
        if (solidsEightIcon) solidsEightIcon.color = eightDimColor;
        if (stripesEightIcon) stripesEightIcon.color = eightDimColor;
    }

    void SetEightIconsByGroup(bool solidsLit, bool stripesLit)
    {
        // If locked, enforce locked winner every time.
        if (matchEnded && winnerGroupLocked != Group.Unknown)
        {
            solidsLit = (winnerGroupLocked == Group.Solids);
            stripesLit = (winnerGroupLocked == Group.Stripes);
        }

        if (solidsEightIcon)
        {
            solidsEightIcon.color = solidsLit ? eightLitColor : eightDimColor;
            solidsEightIcon.gameObject.SetActive(solidsEightIcon.color.a > 0f);
        }
        if (stripesEightIcon)
        {
            stripesEightIcon.color = stripesLit ? eightLitColor : eightDimColor;
            stripesEightIcon.gameObject.SetActive(stripesEightIcon.color.a > 0f);
        }
    }

    public void ShowRuleMessage(string msg, float seconds = -1f)
    {
        if (!ruleIndicatorPanel || !ruleIndicatorText) return;
        ruleIndicatorText.text = msg;
        ruleIndicatorPanel.SetActive(true);

        if (uiHideFlow != null) StopCoroutine(uiHideFlow);
        uiHideFlow = StartCoroutine(HideRulePanelAfter(seconds > 0f ? seconds : ruleIndicatorDefaultSeconds));
    }

    IEnumerator HideRulePanelAfter(float t)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, t));
        if (ruleIndicatorPanel) ruleIndicatorPanel.SetActive(false);
        uiHideFlow = null;
    }

    // ===== Optional: Kitchen Constraint helper =====
    public class KitchenConstraint : MonoBehaviour
    {
        [Tooltip("Convex Mesh/Box/CapsuleCollider for the kitchen behind the headstring.")]
        public Collider zone;

        [Header("Debug")]
        public bool drawGizmos = false;
        public Color gizmoColor = new Color(0f, 0.6f, 1f, 0.25f);

        public void EnableKitchenMode(bool on) { /* optional visuals */ }

        public bool Contains(Vector3 worldPos)
        {
            if (!zone) return false;
            if (zone.bounds.Contains(worldPos)) return true;
            Vector3 cp = zone.ClosestPoint(worldPos);
            worldPos.y = 0f; cp.y = 0f;
            return (worldPos - cp).sqrMagnitude <= 0.001f * 0.001f;
        }

        public Vector3 ProjectInside(Vector3 worldPos)
        {
            if (Contains(worldPos) || zone == null) return worldPos;
            Vector3 cp = zone.ClosestPoint(worldPos);
            cp.y = worldPos.y;
            return cp;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !zone) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(zone.bounds.center, zone.bounds.size);
        }
#endif
    }
}
