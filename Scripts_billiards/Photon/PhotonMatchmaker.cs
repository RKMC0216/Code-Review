using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PR = Photon.Realtime;

public class PhotonMatchmaker : MonoBehaviourPunCallbacks
{
    [Header("Scene")]
    public string gameSceneName = "Pool_3dGame_Photon";

    [Header("Room")]
    public byte maxPlayers = 2;
    public float connectTimeout = 8f;
    public int connectRetries = 1;
    public Vector2 retryBackoffRange = new Vector2(0.25f, 0.75f);

    private enum PendingAction { None, HostInvite, HostQuick, JoinByName, JoinRandom }
    private PendingAction _next = PendingAction.None;

    private string _pendingRoom;
    private bool _loadingScene = false;
    private bool _connecting = false;
    private bool _pendingActionActive = false; // NEW: prevent re-entry while an action is underway

    private static PhotonMatchmaker _instance;

    void Awake()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) { Destroy(gameObject); return; }
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) Destroy(gameObject);
    }

    // ---------- Public API ----------
    public void HostMatch(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_pendingActionActive) return; // busy
        _pendingRoom = roomCode;
        _next = PendingAction.HostInvite;
        EnsureConnected();
    }

    public void JoinMatch(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_pendingActionActive) return; // busy
        _pendingRoom = roomCode;
        _next = PendingAction.JoinByName;
        EnsureConnected();
    }

    public void HostQuickMatch(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_pendingActionActive) return; // busy
        _pendingRoom = roomCode;
        _next = PendingAction.HostQuick;
        EnsureConnected();
    }

    public void JoinAnyOpenRoom()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_pendingActionActive) return; // busy
        _pendingRoom = null;
        _next = PendingAction.JoinRandom;
        EnsureConnected();
    }

    // ---------- Connection orchestration ----------
    private void EnsureConnected()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        // ✅ If we're already inside a room, do NOT try to connect-to-master again.
        if (PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState == PR.ClientState.Joined)
        {
            Debug.Log("[Matchmaker] Already in a room; skipping EnsureConnected.");
            return;
        }

        // Already at Master? Do the pending action right away.
        if (PhotonNetwork.IsConnectedAndReady &&
            PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
        {
            DoPending();
            return;
        }

        // If connection is already progressing, let it finish.
        if (_connecting ||
            PhotonNetwork.NetworkClientState == PR.ClientState.ConnectingToNameServer ||
            PhotonNetwork.NetworkClientState == PR.ClientState.ConnectingToMasterServer)
        {
            return;
        }

        StartCoroutine(ConnectWithRetries());
    }

    private IEnumerator ConnectWithRetries()
    {
        _connecting = true;

        int attempts = Mathf.Max(1, connectRetries + 1);
        while (attempts-- > 0)
        {
            if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) { _connecting = false; yield break; }

            // ✅ If we entered a room (perhaps from another flow), bail out cleanly.
            if (PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState == PR.ClientState.Joined)
            {
                Debug.Log("[Matchmaker] Connect aborted: already in a room.");
                _connecting = false;
                yield break;
            }

            if (!PhotonNetwork.IsConnected || PhotonNetwork.NetworkClientState == PR.ClientState.Disconnected)
            {
                Debug.Log("[Matchmaker] Connecting using settings…");
                bool started = PhotonNetwork.ConnectUsingSettings();
                if (!started) Debug.LogWarning("[Matchmaker] ConnectUsingSettings() returned false; will retry.");
            }

            float t = Mathf.Max(2f, connectTimeout);
            while (t > 0f &&
                   PhotonNetwork.NetworkClientState != PR.ClientState.ConnectedToMasterServer &&
                   PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
            {
                // ✅ If we got into a room while waiting, that's fine—exit.
                if (PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState == PR.ClientState.Joined)
                {
                    Debug.Log("[Matchmaker] Connect wait ended: now in a room.");
                    _connecting = false;
                    yield break;
                }

                if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin)
                {
                    _connecting = false; yield break;
                }

                t -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
            {
                Debug.Log($"[Matchmaker] Connected to Master ({PhotonNetwork.CloudRegion}).");
                _connecting = false;
                DoPending();
                yield break;
            }

            Debug.LogWarning($"[Matchmaker] Connect attempt failed/timed out (state={PhotonNetwork.NetworkClientState}).");

            // Backoff, then force a clean disconnect before next attempt (if still half-connected)
            yield return new WaitForSeconds(Random.Range(retryBackoffRange.x, retryBackoffRange.y));

            if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
            {
                PhotonNetwork.Disconnect();
                float settle = 0.25f;
                while (settle > 0f && PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
                {
                    settle -= Time.unscaledDeltaTime; yield return null;
                }
            }
        }

        _connecting = false;
        // Only log an error if we STILL aren’t in a room; otherwise it’s harmless.
        if (!PhotonNetwork.InRoom && PhotonNetwork.NetworkClientState != PR.ClientState.Joined)
        {
            Debug.LogError("[Matchmaker] Unable to connect to Master after retries.");
        }
    }

    private void DoPending()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_pendingActionActive) return; // already executing an action

        switch (_next)
        {
            case PendingAction.HostInvite:
                if (string.IsNullOrEmpty(_pendingRoom)) return;
                _pendingActionActive = true;
                PhotonNetwork.CreateRoom(_pendingRoom, BuildRoomOptions(false), TypedLobby.Default);
                _next = PendingAction.None; // ✅ clear
                break;

            case PendingAction.HostQuick:
                if (string.IsNullOrEmpty(_pendingRoom)) return;
                _pendingActionActive = true;
                PhotonNetwork.CreateRoom(_pendingRoom, BuildRoomOptions(true), TypedLobby.Default);
                _next = PendingAction.None; // ✅ clear
                break;

            case PendingAction.JoinByName:
                if (string.IsNullOrEmpty(_pendingRoom)) return;
                _pendingActionActive = true;
                PhotonNetwork.JoinRoom(_pendingRoom);
                _next = PendingAction.None; // ✅ clear
                break;

            case PendingAction.JoinRandom:
                if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
                {
                    _pendingActionActive = true;
                    PhotonNetwork.JoinRandomRoom();
                    _next = PendingAction.None; // ✅ clear
                }
                break;

            case PendingAction.None:
            default: break;
        }
    }

    private RoomOptions BuildRoomOptions(bool isVisible)
    {
        return new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = isVisible,
            IsOpen = true,
            CleanupCacheOnLeave = true,
            PublishUserId = true,
            EmptyRoomTtl = 0,
            PlayerTtl = 0,
            SuppressRoomEvents = false,
        };
    }

    // ---------- Photon callbacks ----------
    public override void OnConnectedToMaster()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        Debug.Log($"[Matchmaker] OnConnectedToMaster ({PhotonNetwork.CloudRegion})");
        _connecting = false;
        DoPending();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Matchmaker] Disconnected: {cause}");
        _connecting = false;
        _pendingActionActive = false; // allow retrying new actions later
    }

    public override void OnCreatedRoom()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        Debug.Log($"[Matchmaker] OnCreatedRoom → {PhotonNetwork.CurrentRoom?.Name}");
        LoadGameIfNeeded();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Matchmaker] CreateRoom failed ({returnCode}): {message}");

        if (_next == PendingAction.HostInvite && !string.IsNullOrEmpty(_pendingRoom))
        {
            Debug.Log("[Matchmaker] Room exists; attempting JoinRoom (invite flow).");
            PhotonNetwork.JoinRoom(_pendingRoom);
            return;
        }

        if (_next == PendingAction.HostQuick && !string.IsNullOrEmpty(_pendingRoom))
        {
            string alt = _pendingRoom + "-" + Random.Range(100, 999);
            Debug.Log($"[Matchmaker] Quick host fallback with alt name: {alt}");
            _pendingRoom = alt;
            PhotonNetwork.CreateRoom(_pendingRoom, BuildRoomOptions(true), TypedLobby.Default);
            return;
        }

        _pendingActionActive = false; // free to try again
    }

    public override void OnJoinedRoom()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        var room = PhotonNetwork.CurrentRoom;
        Debug.Log($"[Matchmaker] OnJoinedRoom: {room?.Name} (players {room?.PlayerCount}/{room?.MaxPlayers})");

        _pendingActionActive = false; // finished

        if (PhotonNetwork.IsMasterClient)
        {
            LoadGameIfNeeded();
            if (room != null && room.PlayerCount >= room.MaxPlayers)
            {
                room.IsOpen = false;
                room.IsVisible = false;
            }
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Matchmaker] JoinRoom failed ({returnCode}): {message}");
        _pendingActionActive = false; // free to try another attempt
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"[Matchmaker] JoinRandom failed: {returnCode} {message}");
        _pendingActionActive = false;
    }

    // ---------- Scene loading with queue pause ----------
    private void LoadGameIfNeeded()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;
        if (_loadingScene) return;
        if (SceneManager.GetActiveScene().name == gameSceneName) return;

        _loadingScene = true;
        PhotonNetwork.IsMessageQueueRunning = false;

        SceneManager.sceneLoaded += OnSceneLoadedResumeQueue;
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    private void OnSceneLoadedResumeQueue(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameSceneName) return;

        SceneManager.sceneLoaded -= OnSceneLoadedResumeQueue;
        StartCoroutine(ResumeQueueNextFrame());
    }

    private IEnumerator ResumeQueueNextFrame()
    {
        yield return null;
        if (!MatchContext.ExitingToLogin && !SceneLoader.ReturningToLogin)
            PhotonNetwork.IsMessageQueueRunning = true;

        _loadingScene = false;
    }
}
