using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    [Header("Scene Refs")]
    public CustomCueBall cueBall;                // main cue ball
    public GameObject playerCueRoot;             // parent of cue + UI (enable/disable)
    public CustomTrajectory trajectory;          // to hide while balls are moving
    public SimpleAIOpponent ai;                  // your AI component
    public CueStickController cueController;     // player controller (to re‑enable turn)

    [Header("Settings")]
    public string ballTag = "Ball";
    public bool playerStarts = true;
    public float pollInterval = 0.25f;

    [Header("Turn Timer")]
    public bool useTurnTimer = true;
    public Image turnTimerImage;                // Image (Filled) with fillAmount=1 by default
    public float turnTimeSeconds = 20f;         // total time per turn (aim time)
    public bool showTimerOnlyOnPlayersTurn = false;

    // --- state ---
    bool isPlayerTurn;
    bool waitingForShot;
    bool hasShotThisTurn;
    Coroutine turnFlow;
    Coroutine timerFlow;

    void Awake()
    {
        // Auto‑wire if not assigned
        if (!cueBall) cueBall = FindAnyObjectByType<CustomCueBall>();
        if (!trajectory) trajectory = FindAnyObjectByType<CustomTrajectory>();
        if (!cueController) cueController = FindAnyObjectByType<CueStickController>();
        if (!ai) ai = FindAnyObjectByType<SimpleAIOpponent>();
    }

    void Start()
    {
        isPlayerTurn = playerStarts;
        hasShotThisTurn = false;
        ApplyTurnVisibility(isPlayerTurn);

        ResetTimerUI();           // make sure the bar is full at start
        if (useTurnTimer) StartTurnTimer();  // start the first turn’s timer

        if (!isPlayerTurn && ai != null)
            turnFlow = StartCoroutine(AITurnFlow());
    }

    // --- Public hooks --------------------------------------------------------

    /// Call this **immediately after** cueBall.Shoot(...) from the player controller.
    public void OnPlayerShotFired()
    {
        if (waitingForShot) return;

        hasShotThisTurn = true;
        StopTurnTimer();

        isPlayerTurn = false;           // lock input until turn resolves
        waitingForShot = true;
        ApplyTurnVisibility(false);     // hide cue + trajectory

        if (turnFlow != null) StopCoroutine(turnFlow);
        turnFlow = StartCoroutine(ResolveTurnAndPassToAI());
    }

    /// (Optional) If your AI calls this after it fires, we stop its timer as well.
    public void OnAIShotFired()
    {
        if (waitingForShot) return;
        hasShotThisTurn = true;
        StopTurnTimer();

        waitingForShot = true;
        ApplyTurnVisibility(false);

        if (turnFlow != null) StopCoroutine(turnFlow);
        turnFlow = StartCoroutine(ResolveTurnAndPassToPlayer());
    }

    // --- Turn flows ----------------------------------------------------------

    IEnumerator ResolveTurnAndPassToAI()
    {
        while (AnyBallMoving()) yield return new WaitForSeconds(pollInterval);

        waitingForShot = false;
        hasShotThisTurn = false;

        ApplyTurnVisibility(false);
        ResetTimerUI();
        if (useTurnTimer) StartTurnTimer(false); // start AI timer

        if (turnFlow != null) StopCoroutine(turnFlow);
        turnFlow = StartCoroutine(AITurnFlow());
    }

    IEnumerator AITurnFlow()
    {
        // Small breathing room
        yield return new WaitForSeconds(0.4f);

        if (ai != null)
        {
            ai.TakeShot(); // AI should shoot promptly
            // If your AI calls OnAIShotFired after shooting, we’ll stop the timer there.
        }
        else
        {
            // No AI? give the table back to player
            StopTurnTimer();
            ApplyTurnVisibility(true);
            EnablePlayerTurn();
            yield break;
        }

        while (AnyBallMoving()) yield return new WaitForSeconds(pollInterval);

        waitingForShot = false;
        hasShotThisTurn = false;

        StopTurnTimer();
        ResetTimerUI();

        ApplyTurnVisibility(true);
        EnablePlayerTurn();
        turnFlow = null;
    }

    IEnumerator ResolveTurnAndPassToPlayer()
    {
        while (AnyBallMoving()) yield return new WaitForSeconds(pollInterval);

        waitingForShot = false;
        hasShotThisTurn = false;

        StopTurnTimer();
        ResetTimerUI();

        ApplyTurnVisibility(true);
        EnablePlayerTurn();
        turnFlow = null;
    }

    // --- Timer logic ---------------------------------------------------------

    void StartTurnTimer(bool forPlayer = true)
    {
        if (!useTurnTimer || turnTimerImage == null) return;

        // Show/Hide if requested
        if (showTimerOnlyOnPlayersTurn && turnTimerImage)
            turnTimerImage.gameObject.SetActive(forPlayer);

        if (timerFlow != null) StopCoroutine(timerFlow);
        timerFlow = StartCoroutine(TurnTimerRoutine(forPlayer));
    }

    void StopTurnTimer()
    {
        if (timerFlow != null) StopCoroutine(timerFlow);
        timerFlow = null;
    }

    IEnumerator TurnTimerRoutine(bool forPlayer)
    {
        float t = turnTimeSeconds;
        // Ensure full at start
        if (turnTimerImage) turnTimerImage.fillAmount = 1f;

        while (t > 0f)
        {
            // If balls started moving (someone fired), pause & exit. The shot flow will control turn end.
            if (AnyBallMoving()) yield break;

            t -= Time.deltaTime;
            if (turnTimerImage) turnTimerImage.fillAmount = Mathf.Clamp01(t / turnTimeSeconds);
            yield return null;
        }

        // Time expired: end turn immediately if no shot was taken
        if (!hasShotThisTurn)
        {
            if (forPlayer)
            {
                // Player ran out of time → pass to AI
                ApplyTurnVisibility(false);
                if (trajectory) trajectory.Hide();
                isPlayerTurn = false;
                waitingForShot = false;   // we’re not in a shot; just handover
                hasShotThisTurn = false;

                ResetTimerUI();
                if (useTurnTimer) StartTurnTimer(false);
                if (turnFlow != null) StopCoroutine(turnFlow);
                turnFlow = StartCoroutine(AITurnFlow());
            }
            else
            {
                // AI ran out of time → pass to player
                ApplyTurnVisibility(true);
                EnablePlayerTurn();
            }
        }
    }

    void ResetTimerUI()
    {
        if (turnTimerImage) turnTimerImage.fillAmount = 1f;
    }

    // --- Helpers -------------------------------------------------------------

    void ApplyTurnVisibility(bool playerCanAim)
    {
        if (playerCueRoot) playerCueRoot.SetActive(playerCanAim);
        if (!playerCanAim && trajectory) trajectory.Hide();
    }

    bool AnyBallMoving()
    {
        if (cueBall != null) return cueBall.IsAnyBallMovingByTag(ballTag);

        var balls = GameObject.FindGameObjectsWithTag(ballTag);
        foreach (var go in balls)
        {
            var b = go.GetComponent<CustomCueBall>();
            if (b != null && b.IsMoving()) return true;
        }
        return false;
    }

    // Give control back to the player
    void EnablePlayerTurn()
    {
        isPlayerTurn = true;
        if (cueController != null)
            cueController.EnablePlayerTurn();

        if (useTurnTimer) StartTurnTimer(true);
    }
}
