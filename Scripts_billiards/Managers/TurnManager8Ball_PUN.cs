using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class TurnManager8Ball_PUN : MonoBehaviourPunCallbacks
{
    // ==== Shared profile keys (must match PhotonProfileSync) ====
    public const string KeyName = "pf_name";
    public const string KeyAvatarUrl = "pf_avatarUrl";

    [Header("Scene Refs")]
    public CustomCueBall cueBall;
    public GameObject playerCueRoot;
    public CustomTrajectory trajectory;
    public CueStickController cueController;
    public CueBallPlacement ballInHand;          // optional - only using its Kitchen constraint right now
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
    public bool useNetworkedTimer = true;
    double turnEndTimeRoom = 0;
    bool timerActive = false;

    public enum IllegalBreakPolicy { AcceptTable, Rerack_OpponentBreaks, Rerack_SameShooterBreaks }
    public IllegalBreakPolicy illegalBreakPolicy = IllegalBreakPolicy.AcceptTable;

    public enum EightOnBreakPolicy { RespotAndContinue, Rebreak }
    public EightOnBreakPolicy eightOnBreak_NoFoul = EightOnBreakPolicy.RespotAndContinue;
    public EightOnBreakPolicy eightOnBreak_WithScratch = EightOnBreakPolicy.RespotAndContinue;

    [Header("Groups")]
    public bool useOpenTableRules = true;

    [Header("Call Shot")]
    public bool requireCallOnEight = true;
    public bool lastShotCalledPocketOk = true;

    [Header("Kitchen / BIH")]
    public bool restrictBIHToKitchenOnBreakScratch = true;
    public KitchenConstraint kitchen;

    [Header("Rule Indicator UI (synced)")]
    public GameObject ruleIndicatorPanel;
    public TMP_Text ruleIndicatorText;
    public float ruleIndicatorDefaultSeconds = 2.5f;

    [System.Serializable]
    public enum Group { Unknown, Solids, Stripes }

    [Header("Ball UI (top rows)")]
    [SerializeField] private RectTransform leftSlot;
    [SerializeField] private RectTransform rightSlot;
    [SerializeField] private GameObject solidsBar;
    [SerializeField] private GameObject stripesBar;
    [SerializeField] private GameObject playerFrameGlow;
    [SerializeField] private GameObject opponentFrameGlow;
    [SerializeField] private bool hideBarsOnStart = true;

    [Header("8-Ball Winner Icons (Group-Based)")]
    [Tooltip("Drag the 8-ball icon that sits inside the SOLIDS bar.")]
    public Image solidsEightIcon;
    [Tooltip("Drag the 8-ball icon that sits inside the STRIPES bar.")]
    public Image stripesEightIcon;
    [Tooltip("Lit color (winner icon).")]
    public Color eightLitColor = Color.white;
    [Tooltip("Dim color (default / loser / inactive).")]
    public Color eightDimColor = new Color(1, 1, 1, 0.15f);

    [Header("Win/Loss Panel")]
    public GameObject winLossPanel;
    public TMP_Text winLossTMP;
    public string winHeadline = "YOU WIN!";
    public string loseHeadline = "YOU LOSE!";
    public Color winColor = new Color(0.15f, 0.85f, 0.3f, 1f);
    public Color loseColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("Waiting Panel")]
    public GameObject waitingPanel;

    [Header("Pocket UI (optional)")]
    public BallIconsUI ballIconsUI;

    [Header("Player Banners (drag in)")]
    [SerializeField] PlayerProfileBanner_PUN leftBanner;   // Source = PhotonLocal
    [SerializeField] PlayerProfileBanner_PUN rightBanner;  // Source = PhotonRemoteOther

    // --- profile gate ---
    [SerializeField] private float profilesGateTimeout = 10f;
    private Coroutine profilesGateCo;

    // --- Forfeit / Navigation ---
    [Header("Forfeit / Navigation")]
    public float rpcBeforeLeaveDelay = 0.25f;
    public string fallbackLoginSceneName = "Login";
    [SerializeField] private SceneLoader sceneLoader;   // auto-found, even if inactive
    private bool pendingForfeitLeave = false;

    // ===== Internal =====
    enum Shooter { Host, Client }
    Shooter currentShooter;
    public bool hostStarts = true;

    Group hostGroup = Group.Unknown;
    Group clientGroup = Group.Unknown;
    bool groupsLocked = false;
    bool isBreakShotOfRack = true;

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

    bool hostBIHPending = false;
    bool clientBIHPending = false;

    Coroutine timerFlow, turnFlow, uiHideFlow;
    bool resolving = false;
    bool matchStarted = false;
    bool matchFinished = false;     // ignore OnPlayerLeftRoom after finish

    // ===== scene-ready handshake =====
    private HashSet<int> readyActors = new HashSet<int>();
    private bool iSentReadyFlag = false;

    // ===== 8-ball UI lock at finish =====
    Group winnerGroupLocked = Group.Unknown;

    PhotonView photonView;

    void Awake()
    {
        photonView = GetComponent<PhotonView>();

#if UNITY_2023_1_OR_NEWER
        if (!cueBall) cueBall = FindFirstObjectByType<CustomCueBall>();
        if (!trajectory) trajectory = FindFirstObjectByType<CustomTrajectory>();
        if (!cueController) cueController = FindFirstObjectByType<CueStickController>();
        if (!ballInHand) ballInHand = FindFirstObjectByType<CueBallPlacement>();
        if (!sceneLoader) sceneLoader = FindFirstObjectByType<SceneLoader>(FindObjectsInactive.Include);
#else
        if (!cueBall) cueBall = FindObjectOfType<CustomCueBall>();
        if (!trajectory) trajectory = FindObjectOfType<CustomTrajectory>();
        if (!cueController) cueController = FindObjectOfType<CueStickController>();
        if (!ballInHand) ballInHand = FindObjectOfType<CueBallPlacement>();
        if (!sceneLoader) sceneLoader = SafeFindSceneLoaderLegacy();
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
        ForceDimBothEightIcons();
    }

#if !UNITY_2023_1_OR_NEWER
    SceneLoader SafeFindSceneLoaderLegacy()
    {
        var sl = FindObjectOfType<SceneLoader>();
        if (sl) return sl;
        var all = Resources.FindObjectsOfTypeAll<SceneLoader>();
        foreach (var s in all)
            if (s.gameObject.scene.IsValid()) return s;
        return null;
    }
#endif

    public override void OnEnable()
    {
        base.OnEnable();
        CustomCueBall.OnBallPocketed += OnBallPocketed_Authoritative;
        CustomCueBall.OnCueScratch += OnCueScratch_Authoritative;
        CustomCueBall.OnAnyCushionContact += OnCushionContact_Authoritative;
        CustomCueBall.OnFirstHitBall += OnFirstHitBall_Authoritative;
    }

    public override void OnDisable()
    {
        CustomCueBall.OnBallPocketed -= OnBallPocketed_Authoritative;
        CustomCueBall.OnCueScratch -= OnCueScratch_Authoritative;
        CustomCueBall.OnAnyCushionContact -= OnCushionContact_Authoritative;
        CustomCueBall.OnFirstHitBall -= OnFirstHitBall_Authoritative;
        base.OnDisable();
    }

    void Start()
    {
        SetWaiting(true);
        if (PhotonNetwork.IsConnectedAndReady)
            StartCoroutine(PublishLocalProfileBursts());

        StartCoroutine(AnnounceReadyNextFrame());
    }

    void LateUpdate()
    {
        // Keep winner icons DIM while rack active (prevents “flash”)
        if (!matchFinished)
        {
            if (solidsEightIcon) solidsEightIcon.color = eightDimColor;
            if (stripesEightIcon) stripesEightIcon.color = eightDimColor;
        }
    }

    // 1) Make this a small wait-until helper so we don't miss the window.
    IEnumerator AnnounceReadyNextFrame()
    {
        yield return null; // let scene finish a frame
        while (!PhotonNetwork.InRoom)
            yield return null; // wait until actually joined

        ClientSceneReady();
    }


    // ===== Photon Room Callbacks =====
    // 2) Belt-and-suspenders: if we just joined, announce readiness now.
    public override void OnJoinedRoom()
    {
        if (pendingForfeitLeave) return;
        StartCoroutine(PublishLocalProfileBursts());
        photonView.RPC(nameof(RPC_RequestProfilePublish), RpcTarget.All);
        ForceBannerRefresh();
        if (PhotonNetwork.IsMasterClient) BeginProfilesGate();

        // NEW: ensure we flag readiness once we're in the room.
        ClientSceneReady();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (pendingForfeitLeave) return;
        photonView.RPC(nameof(RPC_RequestProfilePublish), RpcTarget.All);
        ForceBannerRefresh();
        if (PhotonNetwork.IsMasterClient) BeginProfilesGate();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (matchFinished) return;
        if (pendingForfeitLeave) return;

        SetWaiting(true);
        matchStarted = false;
        readyActors.Clear();
        ShowRuleMessage("Opponent left — waiting for a player...", 2.5f);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        if (pendingForfeitLeave) return;
        ForceBannerRefresh();
        if (PhotonNetwork.IsMasterClient && !matchStarted) BeginProfilesGate();
    }

    public override void OnLeftRoom()
    {
        if (pendingForfeitLeave && sceneLoader == null && !string.IsNullOrEmpty(fallbackLoginSceneName))
            SceneManager.LoadScene(fallbackLoginSceneName);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (pendingForfeitLeave)
        {
            if (sceneLoader != null) sceneLoader.GoBack();
            else if (!string.IsNullOrEmpty(fallbackLoginSceneName)) SceneManager.LoadScene(fallbackLoginSceneName);
        }
    }

    // ===== Scene-ready handshake =====
    public void ClientSceneReady()
    {
        if (!PhotonNetwork.InRoom || iSentReadyFlag) { Debug.Log("[TurnMgr] Skip ClientSceneReady: not in room or already sent."); return; }
        iSentReadyFlag = true;
        Debug.Log("[TurnMgr] Sending ClientSceneReady to Master.");
        photonView.RPC(nameof(RPC_ClientSceneReady), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    void RPC_ClientSceneReady(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log($"[TurnMgr] Master received ready from actor {actorNumber}");
        if (!readyActors.Contains(actorNumber)) readyActors.Add(actorNumber);
        MaybeStartMatch();
    }

    bool AllClientsSceneReady()
    {
        if (!PhotonNetwork.InRoom) return false;
        return readyActors.Count >= PhotonNetwork.CurrentRoom.PlayerCount;
    }

    void MaybeStartMatch()
    {
        if (!PhotonNetwork.IsMasterClient || matchStarted) return;

        if (AreProfilesReady() && AllClientsSceneReady())
        {
            Master_BroadcastStartMatch();
        }
    }

    // ===== Waiting / Match start =====
    void SetWaiting(bool waiting)
    {
        if (waitingPanel) waitingPanel.SetActive(waiting);
        ApplyTurnVisibility(false);
        StopTurnTimer();
    }

    void BeginProfilesGate()
    {
        if (profilesGateCo != null) StopCoroutine(profilesGateCo);
        profilesGateCo = StartCoroutine(GateProfilesThenStart());
    }

    IEnumerator GateProfilesThenStart()
    {
        SetWaiting(true);
        while (!(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount >= 2))
            yield return null;

        float t = profilesGateTimeout;
        while (!AreProfilesReady())
        {
            ForceBannerRefresh();
            photonView.RPC(nameof(RPC_RequestProfilePublish), RpcTarget.All);

            if (profilesGateTimeout > 0f)
            {
                t -= 0.25f;
                if (t <= 0f) break;
            }
            yield return new WaitForSeconds(0.25f);
        }

        if (PhotonNetwork.IsMasterClient && !matchStarted)
            MaybeStartMatch();

        profilesGateCo = null;
    }

    bool AreProfilesReady()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount < 2) return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (p == null) return false;

            string name = null;
            if (p.CustomProperties != null &&
                p.CustomProperties.TryGetValue(KeyName, out var obj))
                name = obj as string;

            if (string.IsNullOrEmpty(name)) name = p.NickName;
            if (string.IsNullOrEmpty(name)) return false;
        }
        return true;
    }

    [PunRPC]
    void RPC_RequestProfilePublish()
    {
        if (pendingForfeitLeave) return;
        StartCoroutine(PublishLocalProfileBursts());
    }

    IEnumerator PublishLocalProfileBursts()
    {
        for (int i = 0; i < 3; i++)
        {
            if (pendingForfeitLeave) yield break;
            PublishLocalProfile();
            if (i == 0) yield return null;
            else yield return new WaitForSeconds(i == 1 ? 0.25f : 0.5f);
        }
    }

    void PublishLocalProfile()
    {
        if (pendingForfeitLeave) return;

        string name = null;
        string avatarUrl = "";

        var s = AccountSession.Instance;
        if (s != null)
        {
            if (!string.IsNullOrEmpty(s.DisplayName)) name = s.DisplayName;
            else if (!string.IsNullOrEmpty(s.EmailOrUsername)) name = s.EmailOrUsername;
            if (!string.IsNullOrEmpty(s.AvatarUrl)) avatarUrl = s.AvatarUrl;
        }

        if (string.IsNullOrEmpty(name))
            name = string.IsNullOrEmpty(PhotonNetwork.NickName) ? "Player" : PhotonNetwork.NickName;

        if (!CanSendProps()) return;

        if (!string.IsNullOrEmpty(name) && PhotonNetwork.NickName != name)
            PhotonNetwork.NickName = name;

        var props = new PhotonHashtable
        {
            [KeyName] = name,
            [KeyAvatarUrl] = avatarUrl ?? ""
        };

        bool changed = false;
        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(KeyName, out var nObj) || (string)nObj != name) changed = true;
        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(KeyAvatarUrl, out var aObj) || (string)aObj != avatarUrl) changed = true;

        if (changed) PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    void Master_BroadcastStartMatch()
    {
        isBreakShotOfRack = true;
        groupsLocked = !useOpenTableRules;
        hostGroup = useOpenTableRules ? Group.Unknown : Group.Solids;
        clientGroup = useOpenTableRules ? Group.Unknown : Group.Stripes;
        hostBIHPending = clientBIHPending = false;

        ForceDimBothEightIcons();
        winnerGroupLocked = Group.Unknown;

        var whoStarts = hostStarts ? Shooter.Host : Shooter.Client;
        photonView.RPC(nameof(RPC_StartMatch), RpcTarget.AllBuffered, (int)whoStarts,
                       (int)hostGroup, (int)clientGroup, groupsLocked, isBreakShotOfRack);

        photonView.RPC(nameof(RPC_UI_ResetAllPocketed), RpcTarget.All);
        photonView.RPC(nameof(RPC_RefreshBallUIOrientation), RpcTarget.All);
    }

    [PunRPC]
    void RPC_StartMatch(int shooter, int hostG, int clientG, bool locked, bool breakShot)
    {
        matchStarted = true;
        matchFinished = false;
        if (waitingPanel) waitingPanel.SetActive(false);

        currentShooter = (Shooter)shooter;
        hostGroup = (Group)hostG;
        clientGroup = (Group)clientG;
        groupsLocked = locked;
        isBreakShotOfRack = breakShot;
        hostBIHPending = clientBIHPending = false;

        UpdateBallUIFromLocalPerspective();

        ShowRuleBoth(
            hostText: currentShooter == Shooter.Host ? "Your break" : "Opponent's break",
            clientText: currentShooter == Shooter.Client ? "Your break" : "Opponent's break",
            seconds: 1.4f
        );

        StartTurnLocal(currentShooter);
    }

    // ===== Forfeit =====
    public void OnForfeitButton()
    {
        bool hostForfeited = PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient;
        photonView.RPC(nameof(RPC_OnForfeit_ShowPanelOnly), RpcTarget.All, hostForfeited);

        if (!PhotonNetwork.IsConnected)
        {
            StartCoroutine(LoadLoginImmediate());
            return;
        }

        if (pendingForfeitLeave) return;
        pendingForfeitLeave = true;
        StartCoroutine(ExitToLoginAfterForfeit());
    }

    [PunRPC]
    void RPC_OnForfeit_ShowPanelOnly(bool hostForfeited)
    {
        bool hostWon = !hostForfeited;
        bool iAmHost = PhotonNetwork.IsMasterClient;
        string reason = (iAmHost == hostForfeited) ? "You forfeited" : "Opponent forfeited";
        FinishLocal(hostWon, reason);
    }

    IEnumerator ExitToLoginAfterForfeit()
    {
        yield return new WaitForSeconds(rpcBeforeLeaveDelay);

        MatchContext.ExitingToLogin = true;
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.IsMessageQueueRunning = false;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(false);
            float t = 6f;
            while (PhotonNetwork.InRoom && t > 0f) { t -= Time.unscaledDeltaTime; yield return null; }
        }

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            float td = 6f;
            while (PhotonNetwork.IsConnected && td > 0f) { td -= Time.unscaledDeltaTime; yield return null; }
        }

        var mm = FindFirstObjectByType<PhotonMatchmaker>();
        if (mm) Destroy(mm.gameObject);

        MatchContext.RoomCode = null;
        MatchContext.IsHost = false;
        MatchContext.NickName = null;

#if UNITY_2023_1_OR_NEWER
        if (sceneLoader == null) sceneLoader = FindFirstObjectByType<SceneLoader>(FindObjectsInactive.Include);
#else
        if (sceneLoader == null) sceneLoader = SafeFindSceneLoaderLegacy();
#endif
        if (sceneLoader != null) sceneLoader.GoBack();
        else if (!string.IsNullOrEmpty(fallbackLoginSceneName)) SceneManager.LoadScene(fallbackLoginSceneName);
    }

    IEnumerator LoadLoginImmediate()
    {
        yield return null;
#if UNITY_2023_1_OR_NEWER
        if (sceneLoader == null) sceneLoader = FindFirstObjectByType<SceneLoader>(FindObjectsInactive.Include);
#else
        if (sceneLoader == null) sceneLoader = SafeFindSceneLoaderLegacy();
#endif
        if (sceneLoader != null) sceneLoader.GoBack();
        else if (!string.IsNullOrEmpty(fallbackLoginSceneName)) SceneManager.LoadScene(fallbackLoginSceneName);
    }

    void ForceBannerRefresh()
    {
        leftBanner?.ForceRebind();
        rightBanner?.ForceRebind();
    }

    Shooter LocalShooter() => PhotonNetwork.IsMasterClient ? Shooter.Host : Shooter.Client;
    Shooter RemoteShooter() => PhotonNetwork.IsMasterClient ? Shooter.Client : Shooter.Host;
    bool IsLocalPlayersTurn() => currentShooter == LocalShooter();

    // ===== Events (Master) =====
    void OnFirstHitBall_Authoritative(int number)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (firstHitBallNumber < 0) firstHitBallNumber = number;
        cueBallContacted = true;
    }

    void OnCushionContact_Authoritative()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        cushionAfterContact = true;
    }

    void OnBallPocketed_Authoritative(int number)
    {
        if (!PhotonNetwork.IsMasterClient || !waitingForShot) return;

        photonView.RPC(nameof(RPC_UI_MarkPocketed), RpcTarget.All, number);

        if (number == 8) { eightDownThisShot = true; return; }

        ballsPocketedThisShot++;
        if (number >= 1 && number <= 7) { solidsDown++; shotPocketedSolids++; }
        if (number >= 9 && number <= 15) { stripesDown++; shotPocketedStripes++; }

        // IMPORTANT: Do NOT assign groups here; wait until the shot resolves.
        // (Doing it here causes the "assigned mid-shot" bug when both groups fall.)
    }

    void OnCueScratch_Authoritative()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        shooterScratchedThisShot = true;

        if (cueBall)
        {
            Vector3 resp = cueBallResetPoint ? cueBallResetPoint.position
                                             : new Vector3(0f, cueBall.transform.position.y, 0f);
            photonView.RPC(nameof(RPC_RespawnCueBallAt), RpcTarget.All, resp);
        }

        if (currentShooter == Shooter.Host)
        {
            clientBIHPending = true;
            ShowRuleBoth("Foul: Scratch — Opponent has ball in hand",
                         "Foul: Scratch — You have ball in hand", 2.2f);
        }
        else
        {
            hostBIHPending = true;
            ShowRuleBoth("Foul: Scratch — You have ball in hand",
                         "Foul: Scratch — Opponent has ball in hand", 2.2f);
        }

        photonView.RPC(nameof(RPC_SyncBIHFlags), RpcTarget.All, hostBIHPending, clientBIHPending);
    }

    [PunRPC]
    void RPC_RespawnCueBallAt(Vector3 worldPos) { StartCoroutine(Co_RespawnCueBallAt(worldPos)); }

    IEnumerator Co_RespawnCueBallAt(Vector3 worldPos)
    {
        yield return new WaitForEndOfFrame();

        if (cueBall)
        {
            if (!cueBall.gameObject.activeSelf) cueBall.gameObject.SetActive(true);
            cueBall.RespawnAt(worldPos);
            cueBall.ForceStopNow();
        }
    }

    // ===== Shot entry =====
    public void OnLocalShotFired()
    {
        if (!PhotonNetwork.IsMasterClient && !IsLocalPlayersTurn()) return;
        if (waitingForShot) return;

        photonView.RPC(nameof(RPC_BeginShot), RpcTarget.All, (int)currentShooter);

        if (PhotonNetwork.IsMasterClient)
            SafeStart(ref turnFlow, ResolveAfter(currentShooter));
    }
    public void OnPlayerShotFired() => OnLocalShotFired();

    [PunRPC]
    void RPC_BeginShot(int shooterInt)
    {
        var s = (Shooter)shooterInt;
        if (s != currentShooter) currentShooter = s;

        waitingForShot = true;
        resolving = false;
        StopTurnTimer();
        ApplyTurnVisibility(false);

        cueBallContacted = false;
        firstHitBallNumber = -1;
        cushionAfterContact = false;
        ballsPocketedThisShot = 0;
        shotPocketedSolids = 0;
        shotPocketedStripes = 0;
        shooterScratchedThisShot = false;
        eightDownThisShot = false;
    }

    IEnumerator ResolveAfter(Shooter shooterWhoFired)
    {
        yield return WaitTableIdle_Authoritative();
        if (!BeginResolveOnce()) yield break;
        waitingForShot = false;

        if (isBreakShotOfRack) { PostBreakResolution_Authoritative(shooterWhoFired); yield break; }

        bool foul = EvaluateFoul_Authoritative(shooterWhoFired);
        if (shooterScratchedThisShot) foul = true;

        // ===== Group assignment now happens here (end-of-shot), not in OnBallPocketed =====
        if (!foul) TryAssignGroupsAfterCleanShot_Authoritative(shooterWhoFired);

        bool lose8 = EvaluateEightLoss_Authoritative(shooterWhoFired, foul);
        bool win8 = EvaluateEightWin_Authoritative(shooterWhoFired, foul);

        if (lose8)
        {
            photonView.RPC(nameof(RPC_FinishMatch), RpcTarget.All,
                shooterWhoFired == Shooter.Client, "8-ball pocketed early / foul / wrong pocket");
            yield break;
        }
        if (win8)
        {
            photonView.RPC(nameof(RPC_FinishMatch), RpcTarget.All,
                shooterWhoFired == Shooter.Host, "Legal 8-ball pocket");
            yield break;
        }

        bool keep = KeepsTable_Authoritative(shooterWhoFired, foul);
        Master_StartTurnFor(keep ? shooterWhoFired : (shooterWhoFired == Shooter.Host ? Shooter.Client : Shooter.Host));
    }

    void PostBreakResolution_Authoritative(Shooter shooterWas)
    {
        bool breakLegal = (ballsPocketedThisShot > 0) || cushionAfterContact;

        if (eightDownThisShot)
        {
            if (!shooterScratchedThisShot)
            {
                if (eightOnBreak_NoFoul == EightOnBreakPolicy.Rebreak)
                {
                    ShowRuleBoth("8 on the break — Re-rack", "8 on the break — Re-rack", 2.2f);
                    Master_Rerack(shooterWas);
                    return;
                }

                ShowRuleBoth("8 on the break — Spotted. Shooter continues.",
                             "8 on the break — Spotted. Shooter continues.", 2.4f);
                RespotEight_Authoritative();
                isBreakShotOfRack = false;
                Master_StartTurnFor(shooterWas);
                return;
            }
            else
            {
                if (eightOnBreak_WithScratch == EightOnBreakPolicy.Rebreak)
                {
                    ShowRuleBoth("8 on the break with scratch — Re-rack",
                                 "8 on the break with scratch — Re-rack", 2.2f);
                    Master_Rerack(shooterWas);
                    return;
                }

                ShowRuleBoth("8 on the break with scratch — Spotted. Turn passes, BIH.",
                             "8 on the break with scratch — Spotted. Turn passes, BIH.", 2.4f);
                RespotEight_Authoritative();
                isBreakShotOfRack = false;
                Master_StartTurnFor(shooterWas == Shooter.Host ? Shooter.Client : Shooter.Host);
                return;
            }
        }

        if (!breakLegal)
        {
            switch (illegalBreakPolicy)
            {
                case IllegalBreakPolicy.Rerack_OpponentBreaks:
                    ShowRuleBoth("Illegal break — Re-rack (opponent breaks)",
                                 "Illegal break — Re-rack (opponent breaks)", 2.2f);
                    Master_Rerack(shooterWas == Shooter.Host ? Shooter.Client : Shooter.Host);
                    return;
                case IllegalBreakPolicy.Rerack_SameShooterBreaks:
                    ShowRuleBoth("Illegal break — Re-rack (same shooter)",
                                 "Illegal break — Re-rack (same shooter)", 2.2f);
                    Master_Rerack(shooterWas);
                    return;
                default:
                    isBreakShotOfRack = false;
                    ShowRuleBoth("Illegal break — Turn passes", "Illegal break — Turn passes", 1.8f);
                    Master_StartTurnFor(shooterWas == Shooter.Host ? Shooter.Client : Shooter.Host);
                    return;
            }
        }

        isBreakShotOfRack = false;

        if (ballsPocketedThisShot > 0 && !shooterScratchedThisShot)
        {
            ShowRuleBoth("Legal break — Shooter continues", "Legal break — Shooter continues", 1.6f);
            Master_StartTurnFor(shooterWas);
        }
        else
        {
            ShowRuleBoth("Legal break — Turn passes", "Legal break — Turn passes", 1.4f);
            Master_StartTurnFor(shooterWas == Shooter.Host ? Shooter.Client : Shooter.Host);
        }
    }

    bool EvaluateFoul_Authoritative(Shooter s)
    {
        if (!cueBallContacted)
        {
            photonView.RPC(nameof(RPC_ShowFoulLocalized), RpcTarget.All, (int)s, 0); // No contact
            MarkFoul_Authoritative(s);
            return true;
        }

        Group gShooter = (s == Shooter.Host) ? hostGroup : clientGroup;
        bool openTable = !groupsLocked || gShooter == Group.Unknown;
        bool legallyOnEight = ClearedGroup_Authoritative(gShooter);

        // On open table: no wrong-ball-first requirement (except 8 first, handled elsewhere)
        if (!openTable && !legallyOnEight)
        {
            if (!IsBallInGroup_Authoritative(firstHitBallNumber, gShooter))
            {
                photonView.RPC(nameof(RPC_ShowFoulLocalized), RpcTarget.All, (int)s, 1); // Wrong ball first
                MarkFoul_Authoritative(s);
                return true;
            }
        }

        if (!cushionAfterContact && ballsPocketedThisShot == 0)
        {
            photonView.RPC(nameof(RPC_ShowFoulLocalized), RpcTarget.All, (int)s, 2); // No rail after contact
            MarkFoul_Authoritative(s);
            return true;
        }

        if (shooterScratchedThisShot)
        {
            MarkFoul_Authoritative(s);
            return true;
        }
        return false;
    }

    // === Fixed group assignment: happens AFTER the shot, only if exactly one group pocketed ===
    void TryAssignGroupsAfterCleanShot_Authoritative(Shooter shooter)
    {
        if (!useOpenTableRules || groupsLocked || isBreakShotOfRack) return;

        bool pocketedSolids = shotPocketedSolids > 0;
        bool pocketedStripes = shotPocketedStripes > 0;

        // If both or none — stay open.
        if (pocketedSolids && pocketedStripes)
        {
            ShowRuleBoth("Both groups pocketed on open table — Table remains open.",
                         "Both groups pocketed on open table — Table remains open.", 2.0f);
            return;
        }
        if (!(pocketedSolids ^ pocketedStripes)) return;

        Group chosen = pocketedSolids ? Group.Solids : Group.Stripes;

        if (shooter == Shooter.Host)
        {
            hostGroup = chosen;
            clientGroup = (chosen == Group.Solids) ? Group.Stripes : Group.Solids;
        }
        else
        {
            clientGroup = chosen;
            hostGroup = (chosen == Group.Solids) ? Group.Stripes : Group.Solids;
        }

        groupsLocked = true;

        photonView.RPC(nameof(RPC_UpdateGroups), RpcTarget.All, (int)hostGroup, (int)clientGroup, groupsLocked);
        photonView.RPC(nameof(RPC_ShowGroupAssignment), RpcTarget.All, (int)shooter, (int)chosen);
        photonView.RPC(nameof(RPC_RefreshBallUIOrientation), RpcTarget.All);
    }

    bool EvaluateEightLoss_Authoritative(Shooter s, bool foul)
    {
        if (!eightDownThisShot || isBreakShotOfRack) return false;

        Group gShooter = (s == Shooter.Host) ? hostGroup : clientGroup;
        bool cleared = ClearedGroup_Authoritative(gShooter);

        if (!cleared) { ShowRuleBoth("Loss: 8-ball pocketed early", "Loss: 8-ball pocketed early", 2.6f); return true; }
        if (foul) { ShowRuleBoth("Loss: Foul while pocketing 8-ball", "Loss: Foul while pocketing 8-ball", 2.6f); return true; }
        if (requireCallOnEight && !lastShotCalledPocketOk)
        {
            ShowRuleBoth("Loss: 8-ball in wrong pocket (call required)", "Loss: 8-ball in wrong pocket (call required)", 2.6f);
            return true;
        }
        return false;
    }

    bool EvaluateEightWin_Authoritative(Shooter s, bool foul)
    {
        if (!eightDownThisShot || isBreakShotOfRack) return false;

        Group gShooter = (s == Shooter.Host) ? hostGroup : clientGroup;
        bool cleared = ClearedGroup_Authoritative(gShooter);

        return cleared && !foul && (!requireCallOnEight || lastShotCalledPocketOk);
    }

    bool KeepsTable_Authoritative(Shooter s, bool foul)
    {
        if (foul) return false;

        if (!groupsLocked) return ballsPocketedThisShot > 0 && !shooterScratchedThisShot;

        bool solidsShot = shotPocketedSolids > 0 && !shooterScratchedThisShot;
        bool stripesShot = shotPocketedStripes > 0 && !shooterScratchedThisShot;

        if (s == Shooter.Host && hostGroup == Group.Solids) return solidsShot;
        if (s == Shooter.Host && hostGroup == Group.Stripes) return stripesShot;
        if (s == Shooter.Client && clientGroup == Group.Stripes) return stripesShot;
        if (s == Shooter.Client && clientGroup == Group.Solids) return solidsShot;
        return false;
    }

    bool ClearedGroup_Authoritative(Group g)
    {
        if (g == Group.Solids) return solidsDown >= 7;
        if (g == Group.Stripes) return stripesDown >= 7;
        return false;
    }

    bool IsBallInGroup_Authoritative(int n, Group g)
    {
        if (n <= 0 || n == 8 || n > 15) return false;
        if (g == Group.Solids) return n >= 1 && n <= 7;
        if (g == Group.Stripes) return n >= 9 && n <= 15;
        return false;
    }

    bool MarkFoul_Authoritative(Shooter s)
    {
        if (s == Shooter.Host) clientBIHPending = true; else hostBIHPending = true;
        photonView.RPC(nameof(RPC_SyncBIHFlags), RpcTarget.All, hostBIHPending, clientBIHPending);
        return true;
    }

    bool CanSendProps()
    {
        if (pendingForfeitLeave) return false;
        if (!PhotonNetwork.IsConnected) return false;
        if (!PhotonNetwork.InRoom) return false;
        return PhotonNetwork.NetworkClientState == ClientState.Joined;
    }

    // ===== Turn transitions / timer =====
    void Master_StartTurnFor(Shooter s)
    {
        waitingForShot = false;
        resolving = false;
        currentShooter = s;

        double endTs = useNetworkedTimer && PhotonNetwork.InRoom
                       ? PhotonNetwork.Time + turnTimeSeconds
                       : 0.0;

        turnEndTimeRoom = endTs;

        photonView.RPC(nameof(RPC_StartTurnFor), RpcTarget.All,
            (int)s, hostBIHPending, clientBIHPending, isBreakShotOfRack, endTs);
    }

    [PunRPC]
    void RPC_StartTurnFor(int shooterInt, bool hostBih, bool clientBih, bool breakShot, double endTs)
    {
        currentShooter = (Shooter)shooterInt;
        hostBIHPending = hostBih;
        clientBIHPending = clientBih;
        isBreakShotOfRack = breakShot;
        turnEndTimeRoom = endTs;

        StartTurnLocal(currentShooter);
    }

    void StartTurnLocal(Shooter s)
    {
        StopTurnTimer();

        bool iAmShooter = IsLocalPlayersTurn();

        cueController?.ForceExitBallInHandLocal();

        cueController?.SetLocalAuthority(iAmShooter);
        ApplyTurnVisibility(iAmShooter);

        UpdateBallUIFromLocalPerspective();

        if (iAmShooter)
        {
            bool myBih = PhotonNetwork.IsMasterClient ? hostBIHPending : clientBIHPending;
            if (myBih)
            {
                if (cueBall && !cueBall.gameObject.activeSelf) cueBall.gameObject.SetActive(true);
                cueBall?.ForceStopNow();

                bool useKitchenNow = restrictBIHToKitchenOnBreakScratch && isBreakShotOfRack;
                if (ballInHand) ballInHand.EnableKitchenMode(useKitchenNow);
                kitchen?.EnableKitchenMode(useKitchenNow);

                cueController?.SetBallInHand(true);

                if (LocalShooter() == Shooter.Host)
                {
                    ShowRuleBoth(
                        hostText: useKitchenNow
                            ? "Ball in hand (kitchen) — <b>Click and drag</b> the cue ball within the kitchen."
                            : "Ball in hand — <b>Click and drag</b> the cue ball to place it.",
                        clientText: useKitchenNow
                            ? "Opponent: Ball in hand (kitchen)."
                            : "Opponent: Ball in hand.",
                        seconds: useKitchenNow ? 3.2f : 3.0f
                    );
                }
                else
                {
                    ShowRuleBoth(
                        hostText: useKitchenNow
                            ? "Opponent: Ball in hand (kitchen)."
                            : "Opponent: Ball in hand.",
                        clientText: useKitchenNow
                            ? "Ball in hand (kitchen) — <b>Click and drag</b> the cue ball within the kitchen."
                            : "Ball in hand — <b>Click and drag</b> the cue ball to place it.",
                        seconds: useKitchenNow ? 3.2f : 3.0f
                    );
                }
            }
            else
            {
                ShowRuleBoth(
                    hostText: s == Shooter.Host ? "Your turn" : "Opponent's turn",
                    clientText: s == Shooter.Client ? "Your turn" : "Opponent's turn",
                    seconds: 1.2f
                );
            }
        }
        else
        {
            ShowRuleBoth(
                hostText: s == Shooter.Host ? "Your turn" : "Opponent's turn",
                clientText: s == Shooter.Client ? "Your turn" : "Opponent's turn",
                seconds: 1.2f
            );
        }

        if (useTurnTimer)
        {
            if (useNetworkedTimer && PhotonNetwork.InRoom && turnEndTimeRoom > 0)
                StartTurnTimerNetworked(iAmShooter);
            else
                StartTurnTimer_LocalFallback(iAmShooter);
        }
    }

    public void OnBallInHandPlacedLocal()
    {
        // NETWORKED: tell Master I just placed the cue ball (don’t clear locally)
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            photonView.RPC(nameof(RPC_OnBallInHandPlacedByShooter),
                           RpcTarget.MasterClient,
                           (int)LocalShooter());
            return;
        }

        // OFFLINE: keep current behavior
        if (IsLocalPlayersTurn())
        {
            if (PhotonNetwork.IsMasterClient) hostBIHPending = false; else clientBIHPending = false;
        }
    }

    public void OnBallInHandPlacedNetwork()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (currentShooter == Shooter.Host) hostBIHPending = false;
        else clientBIHPending = false;

        photonView.RPC(nameof(RPC_SyncBIHFlags), RpcTarget.All, hostBIHPending, clientBIHPending);
        photonView.RPC(nameof(RPC_ExitBIHOnShooter), RpcTarget.All, (int)currentShooter);
    }

    [PunRPC]
    void RPC_OnBallInHandPlacedByShooter(int shooterInt)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var s = (Shooter)shooterInt;
        // Guard against stale / wrong-turn messages
        if (s != currentShooter) return;

        if (s == Shooter.Host) hostBIHPending = false;
        else clientBIHPending = false;

        photonView.RPC(nameof(RPC_SyncBIHFlags), RpcTarget.All, hostBIHPending, clientBIHPending);
        photonView.RPC(nameof(RPC_ExitBIHOnShooter), RpcTarget.All, shooterInt);
    }

    [PunRPC] void RPC_SyncBIHFlags(bool hostBih, bool clientBih) { hostBIHPending = hostBih; clientBIHPending = clientBih; }

    [PunRPC]
    void RPC_ExitBIHOnShooter(int shooterInt)
    {
        if ((Shooter)shooterInt == LocalShooter())
        {
            cueController?.ExitBallInHandLocal();
        }
    }

    void StartTurnTimerNetworked(bool forLocal)
    {
        timerActive = true;
        if (turnTimerImage)
        {
            turnTimerImage.fillAmount = 1f;
            if (showTimerOnlyOnPlayersTurn) turnTimerImage.gameObject.SetActive(forLocal);
            else turnTimerImage.gameObject.SetActive(true);
        }
        SafeStart(ref timerFlow, TurnTimerNetworked(forLocal));
    }

    IEnumerator TurnTimerNetworked(bool forLocal)
    {
        while (timerActive)
        {
            double now = PhotonNetwork.Time;
            double rem = turnEndTimeRoom - now;
            if (rem <= 0.0) break;

            if (turnTimerImage)
            {
                float frac = Mathf.Clamp01((float)(rem / turnTimeSeconds));
                turnTimerImage.fillAmount = frac;
            }
            yield return null;
        }

        if (!timerActive) yield break;
        timerActive = false;

        if (!PhotonNetwork.IsMasterClient) yield break;
        if (waitingForShot) yield break;

        if (LocalShooter() == Shooter.Host)
            ShowRuleBoth("Shot clock violation — Turn passes", "Opponent: Shot clock violation — Your turn", 2.0f);
        else
            ShowRuleBoth("Opponent: Shot clock violation — Your turn", "Shot clock violation — Turn passes", 2.0f);

        Master_StartTurnFor(IsLocalPlayersTurn() ? RemoteShooter() : LocalShooter());
    }

    void StartTurnTimer_LocalFallback(bool forLocal)
    {
        if (!turnTimerImage) return;
        if (showTimerOnlyOnPlayersTurn) turnTimerImage.gameObject.SetActive(forLocal);
        SafeStart(ref timerFlow, TurnTimer_Local(forLocal));
    }

    IEnumerator TurnTimer_Local(bool forLocal)
    {
        float t = turnTimeSeconds;
        if (turnTimerImage) turnTimerImage.fillAmount = 1f;

        while (t > 0f)
        {
            if (waitingForShot) yield break;
            t -= Time.deltaTime;
            if (turnTimerImage) turnTimerImage.fillAmount = Mathf.Clamp01(t / turnTimeSeconds);
            yield return null;
        }

        if (!PhotonNetwork.IsMasterClient) yield break;

        if (LocalShooter() == Shooter.Host)
            ShowRuleBoth("Shot clock violation — Turn passes", "Opponent: Shot clock violation — Your turn", 2.0f);
        else
            ShowRuleBoth("Opponent: Shot clock violation — Your turn", "Shot clock violation — Turn passes", 2.0f);

        Master_StartTurnFor(IsLocalPlayersTurn() ? RemoteShooter() : LocalShooter());
    }

    void StopTurnTimer()
    {
        timerActive = false;
        if (timerFlow != null) StopCoroutine(timerFlow);
        timerFlow = null;

        if (turnTimerImage)
        {
            turnTimerImage.fillAmount = 1f;
            if (!showTimerOnlyOnPlayersTurn)
                turnTimerImage.gameObject.SetActive(true);
        }
    }

    void ResetTimerUI()
    {
        if (turnTimerImage)
        {
            turnTimerImage.fillAmount = 1f;
            if (!showTimerOnlyOnPlayersTurn) turnTimerImage.gameObject.SetActive(true);
        }
    }

    // ===== Utilities =====
    IEnumerator WaitTableIdle_Authoritative()
    {
        StopTurnTimer();
        while (!TableIdle()) yield return new WaitForSeconds(0.25f);
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

    bool BeginResolveOnce() { if (resolving) return false; resolving = true; return true; }

    void ApplyTurnVisibility(bool localCanAim)
    {
        if (!localCanAim)
        {
            trajectory?.Hide();
            cueController?.DisablePlayerTurn();
            cueController?.ForceExitBallInHandLocal();
        }
        else
        {
            cueController?.EnablePlayerTurn();
        }
    }

    void SafeStart(ref Coroutine slot, IEnumerator routine)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    void RespotEight_Authoritative()
    {
        if (!eightBall || !footSpot) return;
        eightBall.gameObject.SetActive(true);
        eightBall.RespawnAt(footSpot.position);
        photonView.RPC(nameof(RPC_UI_UnmarkPocketed), RpcTarget.All, 8);
        ForceDimBothEightIcons();
    }

    void Master_Rerack(Shooter nextBreaker)
    {
        isBreakShotOfRack = true;
        groupsLocked = !useOpenTableRules;
        hostGroup = useOpenTableRules ? Group.Unknown : Group.Solids;
        clientGroup = useOpenTableRules ? Group.Unknown : Group.Stripes;
        hostBIHPending = clientBIHPending = false;

        ForceDimBothEightIcons();
        winnerGroupLocked = Group.Unknown;

        ShowRuleBoth("Re-rack", "Re-rack", 1.6f);
        photonView.RPC(nameof(RPC_UI_ResetAllPocketed), RpcTarget.All);
        Master_StartTurnFor(nextBreaker);
        photonView.RPC(nameof(RPC_UpdateGroups), RpcTarget.All, (int)hostGroup, (int)clientGroup, groupsLocked);
        photonView.RPC(nameof(RPC_RefreshBallUIOrientation), RpcTarget.All);
    }

    // ===== UI =====
    public void ShowRuleMessage(string msg, float seconds = -1f)
    {
        if (!ruleIndicatorPanel || !ruleIndicatorText) return;
        ruleIndicatorText.text = msg;
        ruleIndicatorPanel.SetActive(true);

        if (uiHideFlow != null) StopCoroutine(uiHideFlow);
        uiHideFlow = StartCoroutine(HideRulePanelAfter(seconds > 0f ? seconds : ruleIndicatorDefaultSeconds));
    }
    [PunRPC] void RPC_ShowRuleMessage(string msg, float seconds) => ShowRuleMessage(msg, seconds);

    public void ShowRuleBoth(string hostText, string clientText, float seconds = -1f)
    {
        photonView.RPC(nameof(RPC_ShowRuleDual), RpcTarget.All, hostText, clientText, seconds);
    }

    [PunRPC]
    void RPC_ShowRuleDual(string hostText, string clientText, float seconds)
    {
        string msg = PhotonNetwork.IsMasterClient ? hostText : clientText;
        ShowRuleMessage(msg, seconds);
    }

    [PunRPC]
    void RPC_ShowFoulLocalized(int shooterInt, int kind)
    {
        var s = (Shooter)shooterInt;
        bool shooterIsLocal = (s == LocalShooter());
        string baseMsg = kind == 0 ? "Foul: No contact"
                       : kind == 1 ? "Foul: Wrong ball first"
                       : "Foul: No rail after contact";

        string msg = shooterIsLocal
            ? $"{baseMsg} — Opponent has ball in hand"
            : $"{baseMsg} — You have ball in hand";

        ShowRuleMessage(msg, 2.2f);
    }

    [PunRPC]
    void RPC_ShowGroupAssignment(int shooterInt, int gInt)
    {
        var s = (Shooter)shooterInt;
        var g = (Group)gInt;
        bool shooterIsLocal = (s == LocalShooter());

        Group localGroup = shooterIsLocal ? g : (g == Group.Solids ? Group.Stripes : Group.Solids);

        if (localGroup == Group.Unknown)
        {
            ShowRuleMessage("Groups assigned", 2.0f);
            return;
        }

        ShowRuleMessage($"Groups assigned: You are <b>{(localGroup == Group.Solids ? "Solids" : "Stripes")}</b>", 2.0f);
    }

    IEnumerator HideRulePanelAfter(float t)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, t));
        if (ruleIndicatorPanel) ruleIndicatorPanel.SetActive(false);
        uiHideFlow = null;
    }

    [PunRPC]
    void RPC_UpdateGroups(int hostG, int clientG, bool locked)
    {
        hostGroup = (Group)hostG;
        clientGroup = (Group)clientG;
        groupsLocked = locked;
        UpdateBallUIFromLocalPerspective();
    }

    void UpdateBallUIFromLocalPerspective()
    {
        Group myGroup = PhotonNetwork.IsMasterClient ? hostGroup : clientGroup;
        SetPlayerGroupUI(myGroup);
    }

    // ===== Pocket UI RPCs =====
    [PunRPC]
    void RPC_UI_MarkPocketed(int ballNumber)
    {
        if (ballIconsUI) { ballIconsUI.MarkPocketed(ballNumber); return; }
#if UNITY_2023_1_OR_NEWER
        var uis = FindObjectsByType<BallIconsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var uis = FindObjectsOfType<BallIconsUI>(true);
#endif
        foreach (var ui in uis) ui.MarkPocketed(ballNumber);
    }

    [PunRPC]
    void RPC_UI_UnmarkPocketed(int ballNumber)
    {
        if (ballIconsUI) { ballIconsUI.UnmarkPocketed(ballNumber); return; }
#if UNITY_2023_1_OR_NEWER
        var uis = FindObjectsByType<BallIconsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var uis = FindObjectsOfType<BallIconsUI>(true);
#endif
        foreach (var ui in uis) ui.UnmarkPocketed(ballNumber);
    }

    [PunRPC]
    void RPC_UI_ResetAllPocketed()
    {
        if (ballIconsUI) { ballIconsUI.ResetAll(); return; }
#if UNITY_2023_1_OR_NEWER
        var uis = FindObjectsByType<BallIconsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var uis = FindObjectsOfType<BallIconsUI>(true);
#endif
        foreach (var ui in uis) ui.ResetAll();
    }

    [PunRPC] void RPC_RefreshBallUIOrientation() { UpdateBallUIFromLocalPerspective(); }

    // ===== Finish =====
    [PunRPC]
    void RPC_FinishMatch(bool hostWon, string reasonLine)
    {
        FinishLocal(hostWon, reasonLine);
    }

    void FinishLocal(bool hostWon, string reasonLine)
    {
        matchFinished = true;

        StopAllCoroutines();
        StopTurnTimer();
        ApplyTurnVisibility(false);
        trajectory?.Hide();
        enabled = false;

        Group groupWon = hostWon ? hostGroup : clientGroup;
        winnerGroupLocked = groupWon;
        SetEightIconsByGroup(groupWon == Group.Solids, groupWon == Group.Stripes);

        if (!winLossPanel || !winLossTMP) return;
        winLossPanel.SetActive(true);
        string headline = hostWon == PhotonNetwork.IsMasterClient ? winHeadline : loseHeadline;
        string detail = string.IsNullOrWhiteSpace(reasonLine) ? "" : $"\n<size=80%>{reasonLine}</size>";
        winLossTMP.text = $"<b>{headline}</b>{detail}";
        winLossTMP.color = hostWon == PhotonNetwork.IsMasterClient ? winColor : loseColor;

        ShowRuleBoth(reasonLine, reasonLine, 2.5f);
    }

    // ===== Ball UI =====
    Group _playerGroupUI = Group.Unknown;
    Group _opponentGroupUI = Group.Unknown;

    void InitializeBallUI()
    {
        if (!leftSlot || !rightSlot) return;

        if (hideBarsOnStart)
        {
            if (solidsBar) solidsBar.SetActive(false);
            if (stripesBar) stripesBar.SetActive(false);
        }
        if (playerFrameGlow) playerFrameGlow.SetActive(false);
        if (opponentFrameGlow) opponentFrameGlow.SetActive(false);
    }

    public void SetPlayerGroupUI(Group localPlayersGroup)
    {
        _playerGroupUI = localPlayersGroup;
        _opponentGroupUI = (localPlayersGroup == Group.Solids) ? Group.Stripes :
                           (localPlayersGroup == Group.Stripes) ? Group.Solids : Group.Unknown;

        ApplyBallUI();
    }

    void ApplyBallUI()
    {
        if (!leftSlot || !rightSlot || !solidsBar || !stripesBar) return;

        if (_playerGroupUI == Group.Unknown || _opponentGroupUI == Group.Unknown)
        {
            solidsBar.SetActive(false);
            stripesBar.SetActive(false);
            if (playerFrameGlow) playerFrameGlow.SetActive(false);
            if (opponentFrameGlow) opponentFrameGlow.SetActive(false);
            return;
        }

        if (playerFrameGlow) playerFrameGlow.SetActive(true);
        if (opponentFrameGlow) opponentFrameGlow.SetActive(true);

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

    // ===== 8-ball winner icon helpers =====
    void ForceDimBothEightIcons()
    {
        if (solidsEightIcon) solidsEightIcon.color = eightDimColor;
        if (stripesEightIcon) stripesEightIcon.color = eightDimColor;
    }

    void SetEightIconsByGroup(bool solidsLit, bool stripesLit)
    {
        if (matchFinished && winnerGroupLocked != Group.Unknown)
        {
            solidsLit = (winnerGroupLocked == Group.Solids);
            stripesLit = (winnerGroupLocked == Group.Stripes);
        }

        if (solidsEightIcon) solidsEightIcon.color = solidsLit ? eightLitColor : eightDimColor;
        if (stripesEightIcon) stripesEightIcon.color = stripesLit ? eightLitColor : eightDimColor;
    }

    // ===== BIH + Kitchen constraint helper =====
    public class KitchenConstraint : MonoBehaviour
    {
        public Collider zone;
        [Header("Debug")] public bool drawGizmos = false;
        public Color gizmoColor = new Color(0f, 0.6f, 1f, 0.25f);

        public void EnableKitchenMode(bool on) { }

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

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!GetComponent<PhotonView>())
            Debug.LogWarning("[TurnMgr] Missing PhotonView on TurnManager8Ball_PUN object. RPCs will fail.");
        if (!cueController)
            Debug.LogWarning("[TurnMgr] cueController is not assigned.");
        if (!waitingPanel)
            Debug.LogWarning("[TurnMgr] waitingPanel is not assigned.");
        if (!sceneLoader)
            Debug.LogWarning("[TurnMgr] SceneLoader reference not set; will be auto-searched at runtime (incl. inactive objects).");
    }
#endif
}
