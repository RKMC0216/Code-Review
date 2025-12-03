using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;
using System.Text;

// Aliases
using PF = PlayFab.ClientModels;     // PF.FriendInfo, PlayerLeaderboardEntry, etc.
using PR = Photon.Realtime;          // PR.RoomInfo, PR.ClientState, etc.

public class PlayfabManager : MonoBehaviourPunCallbacks
{
    // ===================== Inputs =====================
    [Header("Register / Shared Inputs")]
    public TMP_InputField usernameInput;   // reused by reset flow (username)
    public TMP_InputField emailInput;      // reused by reset flow (email)
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    [Header("Login Inputs (email or username/display name)")]
    public TMP_InputField loginEmailInput;     // accepts either email or username (not display name!)
    public TMP_InputField loginPasswordInput;

    // ===================== UI =====================
    [Header("Common UI")]
    public Text messageText;
    public TextMeshProUGUI usernameTMP;
    public TextMeshProUGUI coinTMP;

    [Header("Waiting Panel")]
    public GameObject waitingPanel;
    public TextMeshProUGUI waitingMessageTMP;

    private bool _userWaitingActive = false;
    private void ShowWaiting(string msg, bool userInitiated)
    {
        if (userInitiated) { _userWaitingActive = true; PhotonNetwork.IsMessageQueueRunning = true; }
        if (!_userWaitingActive) return;
        if (waitingMessageTMP) waitingMessageTMP.text = msg ?? "";
        if (waitingPanel && !waitingPanel.activeSelf) waitingPanel.SetActive(true);
    }

    // ===================== Avatar UI =====================
    [Header("Avatar UI (either or both)")]
    public RawImage avatarRawImage;
    public Image avatarUIImage;
    public Sprite avatarFallback;

    [Header("Avatar source")]
    public string supabaseBaseUrl = "https://qcmdxlbkpjxklkcgcygd.supabase.co/storage/v1/object/public/avatars/";
    public string avatarFileExtension = ".png";
    public string backupAvatarUrl = "https://qcmdxlbkpjxklkcgcygd.supabase.co/storage/v1/object/public/avatars/nft-sample-singer.jpg";

    [Header("Avatar selection UI")]
    public Image selectedAvatarImage;
    public TMP_InputField avatarUrlInput;

    // ===================== Leaderboard =====================
    [Header("Leaderboard")]
    public string leaderboardStatistic = "totalGamesWon";
    public Transform leaderboardContentRoot;
    public GameObject leaderboardEntryPrefab;
    public bool autoRefreshLeaderboardOnLogin = true;

    // ===================== Friends =====================
    [Header("Friends")]
    public TMP_InputField friendUsernameInput;
    public Transform friendsContentRoot;
    public GameObject friendEntryPrefab;
    public int onlineWindowMinutes = 10;
    public bool autoRefreshFriendsOnLogin = true;

    // ===================== Invites =====================
    [Header("Invites (Optional List UI)")]
    public Transform invitesContentRoot;
    public GameObject inviteEntryPrefab;

    [Header("Invites: Simple Panel")]
    public GameObject invitesPanel;
    public TextMeshProUGUI inviteMessageText;
    public Button inviteAcceptButton;
    public Button inviteDeclineButton;

    private const string InviteRoomKey = "invite_room";
    private const string InviteToKey = "invite_to";
    private const string InviteExpireKey = "invite_expireUtc";
    private const int InviteTTLMinutes = 10;

    // ===================== Panels =====================
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject mainMenuPanel;

    // ===================== Presence =====================
    [Header("Presence Heartbeat")]
    public float presenceHeartbeatSeconds = 30f;
    public float presenceStaleSeconds = 90f;
    private const string PresenceOnlineKey = "presence_isOnline";
    private const string PresenceLastSeenKey = "presence_lastSeenUtc";

    // ===================== Multiplayer =====================
    [Header("Multiplayer")]
    public string gameSceneName = "Pool_3dGame_Photon";
    public Button hostButton;
    public Button joinByCodeButton;
    public TMP_InputField joinRoomCodeTMP;

    [Header("Find Match (Quickmatch)")]
    public Button findMatchButton;
    public float connectTimeoutSeconds = 10f;

    [Header("Photon Connection Settings")]
    [Tooltip("Optional region code (e.g., 'asia', 'us', 'eu'). Leave empty for best region.")]
    public string regionOverride = "";
    [Tooltip("Retries for EnsureConnectedToMaster().")]
    public int connectRetries = 2;

    // ===================== Password Reset (uses usernameInput & emailInput) =====================
    [Header("Password Reset UI Status")]
    public GameObject passwordResetPanel;      // optional: show/hide
    public TextMeshProUGUI resetStatusTMP;
    public Color resetInfoColor = new Color(0.85f, 0.85f, 0.9f);
    public Color resetErrorColor = new Color(1.00f, 0.40f, 0.40f);
    public Color resetSuccessColor = new Color(0.40f, 0.90f, 0.55f);

    // ===================== State =====================
    private string _myPlayFabId = null;
    private bool _isLoggedIn = false;
    private string pendingDisplayName = "";
    private string _pendingInviterId = null;
    private string _pendingRoomCode = null;

    private bool _qmBusy = false;
    private bool _qmFinalized = false;
    private Coroutine _qmFlow = null;

    private readonly List<PR.RoomInfo> _cachedRooms = new List<PR.RoomInfo>();
    private bool _lobbySeenOnce = false;

    [Header("Auto Refresh")]
    public bool enableAutoRefresh = true;
    [Range(5f, 300f)] public float autoRefreshInterval = 15f;
    private Coroutine _autoRefreshCo;
    private Coroutine _presenceHeartbeatCo;

    private string _lastLoginUser = "";
    private string _lastLoginPass = "";

    // ===== Legacy login compatibility =====
    private bool _loginAttemptLegacyTried = false;
    private string _pendingLoginUser = null;
    private string _pendingLoginPass = null;

    const string PlayerPrefsDismissedInvitesKey = "pf_dismissed_invites_v1";
    readonly HashSet<string> _dismissedInvites = new HashSet<string>();

    // Scene-load guard
    private bool _gameSceneLoadIssued = false;

    // Pacing gates
    private bool _refreshInFlight = false;
    private Coroutine _postLoginWarmupCo;

    private string GenerateRoomCode() => $"ROOM-{UnityEngine.Random.Range(100000, 999999)}";
    private string MakeInviteKey(string inviterPlayFabId, string roomCode) => $"{inviterPlayFabId}|{roomCode}";

    // ===================== Unity Lifecycle =====================
    public override void OnEnable()
    {
        if (AccountSession.Instance != null)
        {
            AccountSession.Instance.OnAutoLoginSucceeded += OnAutoLoginSucceededFromSession;
            AccountSession.Instance.OnAutoLoginFailed += OnAutoLoginFailedFromSession;
            AccountSession.Instance.OnAvatarReady += OnAvatarReadyHandler;
        }
    }

    public override void OnDisable()
    {
        if (AccountSession.Instance != null)
        {
            AccountSession.Instance.OnAutoLoginSucceeded -= OnAutoLoginSucceededFromSession;
            AccountSession.Instance.OnAutoLoginFailed -= OnAutoLoginFailedFromSession;
            AccountSession.Instance.OnAvatarReady -= OnAvatarReadyHandler;
        }
    }

    private void Awake()
    {
        const string PUN_GAME_VERSION = "billiards-v1";
        PhotonNetwork.GameVersion = PUN_GAME_VERSION;

        PhotonNetwork.AutomaticallySyncScene = !(MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin);

        if (hostButton) hostButton.onClick.AddListener(HostRandomRoom);
        if (joinByCodeButton) joinByCodeButton.onClick.AddListener(JoinByCode);
        if (findMatchButton) findMatchButton.onClick.AddListener(FindMatch);
        if (inviteAcceptButton) inviteAcceptButton.onClick.AddListener(AcceptInvite);
        if (inviteDeclineButton) inviteDeclineButton.onClick.AddListener(DeclineInvite);
    }

    private void Start()
    {
        LogTitleIdSanity();
        StartCoroutine(TryLateBindIfAlreadyLoggedIn());
    }

    private void LogTitleIdSanity()
    {
        string titleId = null;
        try { titleId = PlayFabSettings.staticSettings?.TitleId; } catch { }
        if (string.IsNullOrEmpty(titleId)) { try { titleId = PlayFabSettings.TitleId; } catch { } }
        Debug.Log($"[PlayFab] Using TitleId: '{titleId}'");
    }

    private IEnumerator TryLateBindIfAlreadyLoggedIn()
    {
        yield return new WaitForSeconds(0.1f);

        if (!IsPlayFabLoggedInSafely()) yield break;

        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(),
            res =>
            {
                _myPlayFabId = res.AccountInfo?.PlayFabId;
                HandlePostLoginUIFromSession();
            },
            err => { });
    }

    private void OnApplicationQuit()
    {
        TrySetOfflineOnExit("OnApplicationQuit");
    }

    private void TrySetOfflineOnExit(string reason)
    {
        if (_isLoggedIn)
        {
            Debug.Log($"[Presence] Attempt set offline due to {reason}");
            SetOnlineStatus(false);
        }
    }

    // ===================== Auth =====================
    public void Register()
    {
        string username = (usernameInput?.text ?? "").Trim();
        string email = (emailInput?.text ?? "").Trim();
        string password = passwordInput ? passwordInput.text : "";
        string confirmPassword = confirmPasswordInput ? confirmPasswordInput.text : "";

        if (string.IsNullOrEmpty(username)) { ShowMessage("Username is required."); return; }
        if (username.Length < 3 || username.Length > 20) { ShowMessage("Username must be 3–20 characters."); return; }
        if (string.IsNullOrEmpty(email)) { ShowMessage("Email is required."); return; }
        if (!IsValidEmail(email)) { ShowMessage("Invalid email format."); return; }
        if (string.IsNullOrEmpty(password)) { ShowMessage("Password is required."); return; }
        if (password.Length < 6) { ShowMessage("Password must be at least 6 characters."); return; }
        if (password != confirmPassword) { ShowMessage("Passwords do not match."); return; }

        var info = new GetPlayerCombinedInfoRequestParams
        {
            GetPlayerProfile = true,
            GetUserInventory = true,
            GetUserVirtualCurrency = true,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true, ShowAvatarUrl = true }
        };

        var req = new RegisterPlayFabUserRequest
        {
            Username = username,
            Email = email,
            Password = password,
            RequireBothUsernameAndEmail = true,
            InfoRequestParameters = info
        };

        pendingDisplayName = username;
        PlayFabClientAPI.RegisterPlayFabUser(req, OnRegisterSuccess, OnError);
    }

    public void Login()
    {
        string raw = loginEmailInput ? loginEmailInput.text : "";
        string userInput = (raw ?? "").Trim();
        userInput = Regex.Replace(userInput, @"\s+", " ");
        string password = loginPasswordInput ? loginPasswordInput.text : "";

        if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Email/Username and password are required.");
            return;
        }

        _lastLoginUser = userInput;
        _lastLoginPass = password;

        _pendingLoginUser = userInput;
        _pendingLoginPass = password;
        _loginAttemptLegacyTried = false;

        AttemptLoginInternal(userInput, password, legacy: false);
    }

    private void AttemptLoginInternal(string userInput, string password, bool legacy)
    {
        var info = new GetPlayerCombinedInfoRequestParams
        {
            GetPlayerProfile = true,
            GetUserInventory = true,
            GetUserVirtualCurrency = true,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true, ShowAvatarUrl = true }
        };

        bool inputIsEmail = IsValidEmail(userInput);

        if (legacy)
        {
            string md5 = LegacyMd5(password);
            if (inputIsEmail)
            {
                PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                {
                    Email = userInput,
                    Password = md5,
                    InfoRequestParameters = info
                }, OnLoginSuccess, OnLegacyLoginError);
            }
            else
            {
                PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest
                {
                    Username = userInput,
                    Password = md5,
                    InfoRequestParameters = info
                }, OnLoginSuccess, OnLegacyLoginError);
            }
        }
        else
        {
            if (inputIsEmail)
            {
                PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                {
                    Email = userInput,
                    Password = password,
                    InfoRequestParameters = info
                }, OnLoginSuccess, OnPrimaryLoginError);
            }
            else
            {
                PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest
                {
                    Username = userInput,
                    Password = password,
                    InfoRequestParameters = info
                }, OnLoginSuccess, OnPrimaryLoginError);
            }
        }
    }

    private void OnPrimaryLoginError(PlayFabError error)
    {
        // If the plain-text login failed with bad creds, try legacy MD5 once
        if (!_loginAttemptLegacyTried &&
            (error.Error == PlayFabErrorCode.InvalidUsernameOrPassword || error.Error == PlayFabErrorCode.InvalidParams))
        {
            _loginAttemptLegacyTried = true;
            Debug.LogWarning("[Login] Plain-text failed; trying legacy MD5 fallback…");
            AttemptLoginInternal(_pendingLoginUser, _pendingLoginPass, legacy: true);
            return;
        }

        OnError(error);
    }

    private void OnLegacyLoginError(PlayFabError error)
    {
        Debug.LogWarning("[Login] Legacy MD5 fallback failed as well.");
        OnError(error);
    }

    private string LegacyMd5(string pass)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(pass ?? "");
            var hash = md5.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public void Logout()
    {
        if (_isLoggedIn) SetOnlineStatus(false, FinishLocalLogout);
        else FinishLocalLogout();

        StopAutoRefresh();
        StopPresenceHeartbeat();
    }

    private void FinishLocalLogout()
    {
        PlayFabClientAPI.ForgetAllCredentials();
        _isLoggedIn = false;
        _myPlayFabId = null;
        _pendingInviterId = null;
        _pendingRoomCode = null;

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (loginPanel) loginPanel.SetActive(true);
        if (invitesPanel) invitesPanel.SetActive(false);

        ShowMessage("You have been logged out.");
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        ShowMessage("Registration successful!");

        // Welcome coins
        PlayFabClientAPI.AddUserVirtualCurrency(
            new AddUserVirtualCurrencyRequest { VirtualCurrency = "BC", Amount = 10 }, _ => { }, OnError);

        // Make DisplayName = Username for your UX
        SetDisplayName(pendingDisplayName, () =>
        {
            string firstUrl = BuildAvatarUrlFromSelectedOrBackup();
            UpdatePlayFabAvatarUrl(firstUrl, () => { StartCoroutine(DownloadAndApplyAvatar(firstUrl)); });
        });

        if (registerPanel) registerPanel.SetActive(false);
        if (loginPanel) loginPanel.SetActive(true);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        ShowMessage("Login successful!");
        _isLoggedIn = true;

        if (loginPanel) loginPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        _myPlayFabId = result?.PlayFabId;

        // Self-repair: If user logged in via EMAIL, attach a Username if missing.
        if (IsValidEmail(_lastLoginUser))
        {
            EnsureUsernameIfMissingAfterEmailLogin(_lastLoginUser, _lastLoginPass);
        }

        if (AccountSession.Instance != null)
        {
            AccountSession.Instance.OnPlayfabLoginSuccess(_lastLoginUser, _lastLoginPass, result);

            string nameToShow = !string.IsNullOrEmpty(AccountSession.Instance.DisplayName)
                                ? AccountSession.Instance.DisplayName
                                : AccountSession.Instance.EmailOrUsername;

            if (usernameTMP && !string.IsNullOrEmpty(nameToShow))
                usernameTMP.text = nameToShow;

            MatchContext.NickName = nameToShow;

            if (!string.IsNullOrEmpty(MatchContext.NickName))
                PhotonNetwork.NickName = MatchContext.NickName;

            AccountSession.Instance.OnAvatarReady -= OnAvatarReadyHandler;
            AccountSession.Instance.OnAvatarReady += OnAvatarReadyHandler;

            if (AccountSession.Instance.AvatarTexture != null)
                ApplyAvatarTexture(AccountSession.Instance.AvatarTexture);
            else
                AccountSession.Instance.EnsureAvatarDownloaded();
        }

        // Presence first (cheap)
        SetOnlineStatus(true);
        StartPresenceHeartbeat();

        // Stagger the rest to avoid request bursts
        if (_postLoginWarmupCo != null) StopCoroutine(_postLoginWarmupCo);
        _postLoginWarmupCo = StartCoroutine(PostLoginWarmup(result));
    }

    private void EnsureUsernameIfMissingAfterEmailLogin(string emailUsedToLogin, string passwordUsedToLogin)
    {
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(),
            res =>
            {
                var info = res?.AccountInfo;
                bool hasUsername = !string.IsNullOrEmpty(info?.Username);
                if (hasUsername) return;

                string suggested = info?.TitleInfo?.DisplayName;
                if (string.IsNullOrWhiteSpace(suggested))
                {
                    var at = emailUsedToLogin.IndexOf('@');
                    suggested = at > 0 ? emailUsedToLogin.Substring(0, at) : "player";
                }

                suggested = suggested.Trim();
                if (suggested.Length < 3) suggested = $"player{UnityEngine.Random.Range(1000, 9999)}";

                var addReq = new AddUsernamePasswordRequest
                {
                    Email = emailUsedToLogin,
                    Password = passwordUsedToLogin,
                    Username = suggested
                };

                PlayFabClientAPI.AddUsernamePassword(addReq,
                    _ => Debug.Log($"[Login Repair] Username '{suggested}' attached to account."),
                    err => Debug.LogWarning("[Login Repair] Could not attach Username: " + err.Error + " / " + err.ErrorMessage));
            },
            err => Debug.LogWarning("[Login Repair] GetAccountInfo failed: " + err.Error + " / " + err.ErrorMessage));
    }

    private IEnumerator PostLoginWarmup(LoginResult loginResult)
    {
        yield return new WaitForSecondsRealtime(0.15f);
        GetDisplayName();

        yield return new WaitForSecondsRealtime(0.15f);
        GetUserCoins();

        bool avatarAlreadyApplied = AccountSession.Instance != null && AccountSession.Instance.AvatarTexture != null;
        string urlFromLogin = loginResult?.InfoResultPayload?.PlayerProfile?.AvatarUrl;

        yield return new WaitForSecondsRealtime(0.20f);
        if (!avatarAlreadyApplied)
        {
            if (!string.IsNullOrEmpty(urlFromLogin)) StartCoroutine(DownloadAndApplyAvatar(urlFromLogin));
            else FallbackToBackupAvatar("No AvatarUrl in login payload.");
        }

        yield return new WaitForSecondsRealtime(0.25f);
        if (autoRefreshLeaderboardOnLogin) RefreshLeaderboardTop10();

        yield return new WaitForSecondsRealtime(0.30f);
        if (autoRefreshFriendsOnLogin) RefreshFriends();

        yield return new WaitForSecondsRealtime(0.30f);
        RefreshInvites();

        yield return new WaitForSecondsRealtime(0.25f);
        StartAutoRefreshIfEnabled();
    }

    private void OnAutoLoginSucceededFromSession()
    {
        HandlePostLoginUIFromSession();

        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(),
            res => { _myPlayFabId = res.AccountInfo?.PlayFabId; },
            _ => { });
    }

    private void OnAutoLoginFailedFromSession(string reason)
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (loginPanel) loginPanel.SetActive(true);
        ShowMessage(string.IsNullOrEmpty(reason) ? "Auto-login failed." : $"Auto-login failed: {reason}");
    }

    private void HandlePostLoginUIFromSession()
    {
        _isLoggedIn = true;

        if (loginPanel) loginPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        if (AccountSession.Instance != null)
        {
            string nameToShow = !string.IsNullOrEmpty(AccountSession.Instance.DisplayName)
                                ? AccountSession.Instance.DisplayName
                                : AccountSession.Instance.EmailOrUsername;

            if (usernameTMP && !string.IsNullOrEmpty(nameToShow))
                usernameTMP.text = nameToShow;

            MatchContext.NickName = nameToShow;

            if (!string.IsNullOrEmpty(MatchContext.NickName))
                PhotonNetwork.NickName = MatchContext.NickName;

            AccountSession.Instance.OnAvatarReady -= OnAvatarReadyHandler;
            AccountSession.Instance.OnAvatarReady += OnAvatarReadyHandler;

            if (AccountSession.Instance.AvatarTexture != null)
                ApplyAvatarTexture(AccountSession.Instance.AvatarTexture);
            else
                AccountSession.Instance.EnsureAvatarDownloaded();
        }

        SetOnlineStatus(true);
        StartPresenceHeartbeat();

        if (_postLoginWarmupCo != null) StopCoroutine(_postLoginWarmupCo);
        _postLoginWarmupCo = StartCoroutine(PostLoginWarmup(null));

        ShowMessage("Auto-login successful!");
    }

    // ===================== Presence =====================
    private void StartPresenceHeartbeat()
    {
        StopPresenceHeartbeat();
        _presenceHeartbeatCo = StartCoroutine(PresenceHeartbeatLoop());
    }

    private void StopPresenceHeartbeat()
    {
        if (_presenceHeartbeatCo != null)
        {
            StopCoroutine(_presenceHeartbeatCo);
            _presenceHeartbeatCo = null;
        }
    }

    private IEnumerator PresenceHeartbeatLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(10f, presenceHeartbeatSeconds));
        while (_isLoggedIn)
        {
            var data = new Dictionary<string, string> {
                { PresenceOnlineKey, "1" },
                { PresenceLastSeenKey, DateTime.UtcNow.ToString("o") }
            };
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
            {
                Data = data,
                Permission = UserDataPermission.Public
            },
            _ => { }, err => { });
            yield return wait;
        }
    }

    private void SetOnlineStatus(bool online, Action onSuccess = null)
    {
        var data = new Dictionary<string, string>
        {
            { PresenceOnlineKey, online ? "1" : "0" },
            { PresenceLastSeenKey, DateTime.UtcNow.ToString("o") }
        };
        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
        {
            Data = data,
            Permission = UserDataPermission.Public
        },
        _ => onSuccess?.Invoke(),
        err => onSuccess?.Invoke());
    }

    // ===================== Profile / Avatar =====================
    private void SetDisplayName(string displayName, Action onSuccess = null, int retry = 1)
    {
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest
        {
            DisplayName = displayName
        },
        _ => onSuccess?.Invoke(),
        err =>
        {
            if (err.Error == PlayFabErrorCode.EntityProfileVersionMismatch && retry > 0)
                StartCoroutine(RetryAfter(0.3f, () => SetDisplayName(displayName, onSuccess, retry - 1)));
            else OnError(err);
        });
    }

    private void UpdatePlayFabAvatarUrl(string url, Action onSuccess = null, int retry = 1)
    {
        PlayFabClientAPI.UpdateAvatarUrl(new UpdateAvatarUrlRequest { ImageUrl = url },
        _ => onSuccess?.Invoke(),
        err =>
        {
            if (err.Error == PlayFabErrorCode.EntityProfileVersionMismatch && retry > 0)
                StartCoroutine(RetryAfter(0.3f, () => UpdatePlayFabAvatarUrl(url, onSuccess, retry - 1)));
            else OnError(err);
        });
    }

    private IEnumerator RetryAfter(float seconds, Action action)
    {
        yield return new WaitForSeconds(seconds);
        action?.Invoke();
    }

    private void GetDisplayName()
    {
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(),
        result =>
        {
            string displayName = result.AccountInfo?.TitleInfo?.DisplayName;
            string username = result.AccountInfo?.Username;

            if (!string.IsNullOrEmpty(displayName))
            {
                ShowMessage("Welcome, " + displayName + "!");
                if (usernameTMP) usernameTMP.text = displayName;
            }
            else if (!string.IsNullOrEmpty(username))
            {
                SetDisplayName(username);
                ShowMessage("Welcome, " + username + "!");
                if (usernameTMP) usernameTMP.text = username;
            }
            else
            {
                ShowMessage("Welcome!");
                if (usernameTMP) usernameTMP.text = "Guest";
            }

            MatchContext.NickName = username;
        },
        OnError);
    }

    private void GetUserCoins()
    {
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(),
        result =>
        {
            int balance = (result.VirtualCurrency != null && result.VirtualCurrency.ContainsKey("BC"))
                ? result.VirtualCurrency["BC"] : 0;
            if (coinTMP) coinTMP.text = balance.ToString();
        },
        OnError);
    }

    public void UpdateProfileAvatarFromSelected()
    {
        string url = BuildAvatarUrlFromSelectedOrBackup();
        UpdatePlayFabAvatarUrl(url, () =>
        {
            StartCoroutine(DownloadAndApplyAvatar(url));
            ShowMessage("Profile picture updated.");
        });
    }

    public void SetAvatarFromInput()
    {
        if (avatarUrlInput == null || string.IsNullOrWhiteSpace(avatarUrlInput.text))
        {
            ShowMessage("Paste a valid image URL first.");
            return;
        }
        string url = avatarUrlInput.text.Trim();
        UpdatePlayFabAvatarUrl(url, () =>
        {
            StartCoroutine(DownloadAndApplyAvatar(url));
            ShowMessage("Avatar updated.");
        });
    }

    private string BuildAvatarUrlFromSelectedOrBackup()
    {
        if (selectedAvatarImage != null && selectedAvatarImage.sprite != null)
            return BuildAvatarUrlFromSprite(selectedAvatarImage.sprite);
        return backupAvatarUrl;
    }

    private string BuildAvatarUrlFromSprite(Sprite sprite)
    {
        if (sprite == null) return backupAvatarUrl;
        string name = sprite.name ?? "avatar";
        name = name.Replace("(Clone)", "").Trim();
        string fileName = Path.HasExtension(name) ? name
            : name + (avatarFileExtension.StartsWith(".") ? avatarFileExtension : "." + avatarFileExtension);
        string encoded = Uri.EscapeDataString(fileName);
        return CombineUrl(supabaseBaseUrl, encoded);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path;
        if (string.IsNullOrEmpty(path)) return baseUrl;
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private IEnumerator DownloadAndApplyAvatar(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                FallbackToBackupAvatar($"Avatar download failed: {req.error}");
                yield break;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            ApplyAvatarTexture(tex);
        }
    }

    private void ApplyAvatarTexture(Texture2D tex)
    {
        if (tex == null) return;

        if (avatarRawImage)
            avatarRawImage.texture = tex;

        if (avatarUIImage)
        {
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            sp.name = "avatar";
            avatarUIImage.sprite = sp;
            avatarUIImage.preserveAspect = true;
        }
    }

    private void FallbackToBackupAvatar(string reason)
    {
        if (string.IsNullOrEmpty(backupAvatarUrl))
        {
            if (avatarUIImage && avatarFallback) avatarUIImage.sprite = avatarFallback;
            return;
        }
        UpdatePlayFabAvatarUrl(backupAvatarUrl, () => { StartCoroutine(DownloadAndApplyAvatar(backupAvatarUrl)); });
    }

    // ===================== Leaderboard =====================
    public void SubmitTotalGamesWon(int totalWins)
    {
        PlayFabClientAPI.UpdatePlayerStatistics(new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate> {
                new StatisticUpdate { StatisticName = leaderboardStatistic, Value = totalWins }
            }
        },
        _ => RefreshLeaderboardTop10(),
        OnError);
    }

    public void RefreshLeaderboardTop10()
    {
        if (leaderboardContentRoot == null || leaderboardEntryPrefab == null)
        {
            Debug.LogWarning("Leaderboard UI not wired.");
            return;
        }

        PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
        {
            StatisticName = leaderboardStatistic,
            StartPosition = 0,
            MaxResultsCount = 10
        },
        res => RenderLeaderboard(res.Leaderboard),
        OnError);
    }

    private void RenderLeaderboard(List<PF.PlayerLeaderboardEntry> entries)
    {
        for (int i = leaderboardContentRoot.childCount - 1; i >= 0; i--)
            Destroy(leaderboardContentRoot.GetChild(i).gameObject);

        foreach (var e in entries)
        {
            int place = e.Position + 1;
            string name = string.IsNullOrEmpty(e.DisplayName) ? e.PlayFabId : e.DisplayName;
            int wins = e.StatValue;

            var row = Instantiate(leaderboardEntryPrefab, leaderboardContentRoot);
            var rowUI = row.GetComponent<LeaderboardRowUI>();
            if (rowUI != null) rowUI.Set(place, name, wins);
            else
            {
                var tmps = row.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (tmps.Length >= 3)
                {
                    tmps[0].text = $"#{place}";
                    tmps[1].text = name;
                    tmps[2].text = wins.ToString();
                }
            }
        }
    }

    // ===================== Friends (presence) =====================
    public void AddFriendByUsername()
    {
        if (friendUsernameInput == null) { ShowMessage("Friend username input not assigned."); return; }
        string friendUsername = friendUsernameInput.text.Trim();
        if (string.IsNullOrEmpty(friendUsername)) { ShowMessage("Type a username first."); return; }

        PlayFabClientAPI.AddFriend(new AddFriendRequest { FriendUsername = friendUsername },
        _ =>
        {
            ShowMessage($"Friend '{friendUsername}' added.");
            friendUsernameInput.text = "";
            RefreshFriends();
        },
        OnError);
    }

    public void RefreshFriends()
    {
        if (friendsContentRoot == null || friendEntryPrefab == null)
        {
            Debug.LogWarning("Friends UI not wired.");
            return;
        }

        var constraints = new PlayerProfileViewConstraints
        {
            ShowDisplayName = true,
            ShowLastLogin = true,
            ShowAvatarUrl = true
        };

        PlayFabClientAPI.GetFriendsList(new GetFriendsListRequest { ProfileConstraints = constraints },
        res => FetchPresenceAndRender(res.Friends),
        OnError);
    }

    private void FetchPresenceAndRender(List<PF.FriendInfo> friends)
    {
        for (int i = friendsContentRoot.childCount - 1; i >= 0; i--)
            Destroy(leaderboardContentRoot.GetChild(i).gameObject);

        if (friends == null || friends.Count == 0) return;

        const int MAX_LOOKUPS = 8;
        int lookups = 0;

        int remaining = friends.Count;
        var displayList = new List<FriendDisplay>(friends.Count);

        foreach (var f in friends)
        {
            var view = new FriendDisplay
            {
                PlayFabId = f.FriendPlayFabId,
                Name =
                    !string.IsNullOrEmpty(f.TitleDisplayName) ? f.TitleDisplayName :
                    !string.IsNullOrEmpty(f.Username) ? f.Username :
                    f.FriendPlayFabId,
                Status = ComputeStatusFromLastSeenOrLogin(f)
            };
            displayList.Add(view);

            if (lookups < MAX_LOOKUPS)
            {
                lookups++;
                StartCoroutine(FetchPresenceForFriendCo(view, () =>
                {
                    if (--remaining == 0) RenderFriends(displayList);
                }));
            }
            else
            {
                if (--remaining == 0) RenderFriends(displayList);
            }
        }
    }

    private IEnumerator FetchPresenceForFriendCo(FriendDisplay view, Action done)
    {
        yield return new WaitForSecondsRealtime(0.05f);

        PlayFabClientAPI.GetUserData(new GetUserDataRequest
        {
            PlayFabId = view.PlayFabId,
            Keys = new List<string> { PresenceOnlineKey, PresenceLastSeenKey }
        },
        dataRes =>
        {
            if (dataRes.Data != null && dataRes.Data.ContainsKey(PresenceOnlineKey))
                view.Status = (dataRes.Data[PresenceOnlineKey].Value == "1") ? "Online" : "Offline";

            if (dataRes.Data != null && dataRes.Data.TryGetValue(PresenceLastSeenKey, out var lastSeenRec))
            {
                if (DateTime.TryParse(lastSeenRec.Value, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out var last))
                {
                    var ageSeconds = (float)(DateTime.UtcNow - last).TotalSeconds;
                    if (ageSeconds > presenceStaleSeconds) view.Status = "Offline";
                }
            }
            done?.Invoke();
        },
        _ => { done?.Invoke(); });
    }

    private string ComputeStatusFromLastSeenOrLogin(PF.FriendInfo f)
    {
        if (f.Profile != null && f.Profile.LastLogin.HasValue)
        {
            var last = f.Profile.LastLogin.Value.ToUniversalTime();
            return (DateTime.UtcNow - last).TotalMinutes <= onlineWindowMinutes ? "Online" : "Offline";
        }
        return "Offline";
    }

    private void RenderFriends(List<FriendDisplay> items)
    {
        foreach (var item in items)
        {
            var go = Instantiate(friendEntryPrefab, friendsContentRoot);
            var ui = go.GetComponent<FriendRowUI>();
            if (ui != null)
            {
                ui.Set(item.Name, item.Status, onInvite: () =>
                {
                    var room = GenerateRoomCode();
                    SendInviteToFriendId(item.PlayFabId, room);
                });
            }
            else
            {
                var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (tmps.Length >= 2)
                {
                    tmps[0].text = item.Name;
                    tmps[1].text = item.Status;
                }
            }
        }
    }

    private class FriendDisplay
    {
        public string PlayFabId;
        public string Name;
        public string Status;
    }

    // ===================== Invites (send + discover) =====================
    public void SendInviteToFriendByUsername()
    {
        if (!_isLoggedIn) { ShowMessage("Please log in first."); return; }
        if (friendUsernameInput == null || string.IsNullOrWhiteSpace(friendUsernameInput.text))
        {
            ShowMessage("Type a friend's username first.");
            return;
        }
        string targetName = friendUsernameInput.text.Trim();

        var constraints = new PlayerProfileViewConstraints { ShowDisplayName = true };
        PlayFabClientAPI.GetFriendsList(new GetFriendsListRequest { ProfileConstraints = constraints },
        res =>
        {
            string targetId = null;
            foreach (var f in res.Friends)
            {
                string name = !string.IsNullOrEmpty(f.TitleDisplayName) ? f.TitleDisplayName :
                              !string.IsNullOrEmpty(f.Username) ? f.Username :
                              f.FriendPlayFabId;
                if (string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    targetId = f.FriendPlayFabId; break;
                }
            }
            if (string.IsNullOrEmpty(targetId))
            {
                ShowMessage($"Friend '{targetName}' not found in your friends.");
                return;
            }

            string roomCode = GenerateRoomCode();
            SendInviteToFriendId(targetId, roomCode);
        },
        OnError);
    }

    public void SendInviteToFriendId(string friendPlayFabId, string roomCode)
    {
        if (!_isLoggedIn) { ShowMessage("Please log in first."); return; }
        if (string.IsNullOrEmpty(friendPlayFabId) || string.IsNullOrEmpty(roomCode))
        {
            ShowMessage("Missing friend ID or room code.");
            return;
        }

        SceneLoader.ClearExitGuards();
        PhotonNetwork.IsMessageQueueRunning = true;

        var data = new Dictionary<string, string>
        {
            { InviteRoomKey, roomCode },
            { InviteToKey, friendPlayFabId },
            { InviteExpireKey, DateTime.UtcNow.AddMinutes(InviteTTLMinutes).ToString("o") }
        };

        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
        {
            Data = data,
            Permission = UserDataPermission.Public
        },
        _ =>
        {
            ShowMessage($"Invite sent! Room: {roomCode}");
            ShowWaiting("Creating private room…\nWaiting for your friend to join…", true);
            LoadGameAsHost(roomCode);
            RefreshInvites();
        },
        OnError);
    }

    public void RefreshInvites()
    {
        if (!_isLoggedIn) return;

        _pendingInviterId = null;
        _pendingRoomCode = null;
        if (invitesPanel) invitesPanel.SetActive(false);
        if (inviteMessageText) inviteMessageText.text = "";

        if (invitesContentRoot != null)
        {
            for (int i = invitesContentRoot.childCount - 1; i >= 0; i--)
                Destroy(invitesContentRoot.GetChild(i).gameObject);
        }

        var constraints = new PlayerProfileViewConstraints { ShowDisplayName = true };
        PlayFabClientAPI.GetFriendsList(new GetFriendsListRequest { ProfileConstraints = constraints },
        res => FetchInvitesFromFriends(res.Friends),
        OnError);
    }

    private void FetchInvitesFromFriends(List<PF.FriendInfo> friends)
    {
        if (friends == null || friends.Count == 0)
        {
            if (invitesPanel) invitesPanel.SetActive(false);
            return;
        }
        int remaining = friends.Count;
        int found = 0;

        foreach (var f in friends)
        {
            string friendId = f.FriendPlayFabId;
            string friendName = !string.IsNullOrEmpty(f.TitleDisplayName) ? f.TitleDisplayName :
                                !string.IsNullOrEmpty(f.Username) ? f.Username : friendId;

            PlayFabClientAPI.GetUserData(new GetUserDataRequest
            {
                PlayFabId = friendId,
                Keys = new List<string> { InviteRoomKey, InviteToKey, InviteExpireKey }
            },
            dataRes =>
            {
                if (TryRenderInviteItem(friendName, friendId, dataRes.Data)) found++;
                if (--remaining == 0) { if (invitesPanel) invitesPanel.SetActive(found > 0); }
            },
            err =>
            {
                if (--remaining == 0) { if (invitesPanel) invitesPanel.SetActive(found > 0); }
            });
        }
    }

    private bool TryRenderInviteItem(string friendName, string friendId, Dictionary<string, UserDataRecord> data)
    {
        if (data == null || string.IsNullOrEmpty(_myPlayFabId)) return false;
        if (!data.ContainsKey(InviteRoomKey) || !data.ContainsKey(InviteToKey)) return false;

        string toId = data[InviteToKey].Value;
        if (!string.Equals(toId, _myPlayFabId, StringComparison.Ordinal)) return false;

        if (data.ContainsKey(InviteExpireKey) &&
            DateTime.TryParse(data[InviteExpireKey].Value, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal, out var exp) &&
            DateTime.UtcNow > exp)
            return false;

        string roomCode = data[InviteRoomKey].Value;

        if (_dismissedInvites.Contains(MakeInviteKey(friendId, roomCode)))
            return false;

        if (invitesContentRoot != null && inviteEntryPrefab != null)
        {
            var go = Instantiate(inviteEntryPrefab, invitesContentRoot);
            var ui = go.GetComponent<InviteRowUI>();
            if (ui != null)
            {
                ui.Set($"{friendName} invited you", roomCode,
                    onAccept: () => { AcceptInvite(friendId, roomCode); },
                    onDecline: () => { DeclineInvite(friendId, roomCode); });
            }
        }

        if (_pendingInviterId == null)
        {
            _pendingInviterId = friendId;
            _pendingRoomCode = roomCode;

            if (inviteMessageText)
                inviteMessageText.text = $"{friendName} invited you to play\nCode: {roomCode}";

            if (inviteAcceptButton)
            {
                inviteAcceptButton.onClick.RemoveAllListeners();
                inviteAcceptButton.onClick.AddListener(() => AcceptInvite(friendId, roomCode));
            }
            if (inviteDeclineButton)
            {
                inviteDeclineButton.onClick.RemoveAllListeners();
                inviteDeclineButton.onClick.AddListener(() => DeclineInvite(friendId, roomCode));
            }
        }
        return true;
    }

    public void AcceptInvite()
    {
        if (string.IsNullOrEmpty(_pendingInviterId) || string.IsNullOrEmpty(_pendingRoomCode))
        {
            ShowMessage("No invite selected.");
            return;
        }

        SceneLoader.ClearExitGuards();
        PhotonNetwork.IsMessageQueueRunning = true;

        ShowWaiting("Joining your friend’s room…", true);

        DismissInvite(_pendingInviterId, _pendingRoomCode);
        LoadGameAsGuest(_pendingRoomCode);

        _pendingInviterId = null;
        _pendingRoomCode = null;
        if (invitesPanel) invitesPanel.SetActive(false);
        RefreshInvites();
    }

    public void AcceptInvite(string inviterPlayFabId, string roomCode)
    {
        _pendingInviterId = inviterPlayFabId;
        _pendingRoomCode = roomCode;
        AcceptInvite();
    }

    public void DeclineInvite()
    {
        if (!string.IsNullOrEmpty(_pendingInviterId) && !string.IsNullOrEmpty(_pendingRoomCode))
            DismissInvite(_pendingInviterId, _pendingRoomCode);

        _pendingInviterId = null;
        _pendingRoomCode = null;
        if (invitesPanel) invitesPanel.SetActive(false);

        RefreshInvites();
    }

    public void DeclineInvite(string inviterPlayFabId, string roomCode)
    {
        DismissInvite(inviterPlayFabId, roomCode);
        DeclineInvite();
    }

    // ===================== Auto Refresh UI =====================
    public void RefreshUI()
    {
        if (!_isLoggedIn)
        {
            ShowMessage("Please log in first.");
            return;
        }
        if (_refreshInFlight) return;

        StartCoroutine(DoRefreshUiStaggered());
    }

    private IEnumerator DoRefreshUiStaggered()
    {
        _refreshInFlight = true;

        GetDisplayName();
        yield return new WaitForSecondsRealtime(0.10f);

        GetUserCoins();
        yield return new WaitForSecondsRealtime(0.10f);

        RefreshLeaderboardTop10();
        yield return new WaitForSecondsRealtime(0.15f);

        RefreshFriends();
        yield return new WaitForSecondsRealtime(0.15f);

        RefreshInvites();
        yield return new WaitForSecondsRealtime(0.10f);

        RefreshAvatarFromServer();

        _refreshInFlight = false;
    }

    private void RefreshAvatarFromServer()
    {
        PlayFabClientAPI.GetPlayerCombinedInfo(new GetPlayerCombinedInfoRequest
        {
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                ProfileConstraints = new PlayerProfileViewConstraints { ShowAvatarUrl = true }
            }
        },
        res =>
        {
            var url = res.InfoResultPayload?.PlayerProfile?.AvatarUrl;
            if (!string.IsNullOrEmpty(url)) StartCoroutine(DownloadAndApplyAvatar(url));
            else FallbackToBackupAvatar("No AvatarUrl on refresh.");
        },
        OnError);
    }

    private void StartAutoRefreshIfEnabled()
    {
        if (!enableAutoRefresh) return;

        if (_autoRefreshCo != null) StopCoroutine(_autoRefreshCo);
        _autoRefreshCo = StartCoroutine(AutoRefreshLoop());
    }

    private void StopAutoRefresh()
    {
        if (_autoRefreshCo != null)
        {
            StopCoroutine(_autoRefreshCo);
            _autoRefreshCo = null;
        }
    }

    private IEnumerator AutoRefreshLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(30f, autoRefreshInterval));
        while (_isLoggedIn && enableAutoRefresh)
        {
            if (!_refreshInFlight) RefreshUI();
            yield return wait;
        }
    }

    // ===================== NEW: Reset Password using usernameInput + emailInput =====================
    private enum ResetStatusType { Info, Success, Error }
    private void ShowResetStatus(string msg, ResetStatusType type = ResetStatusType.Info)
    {
        if (resetStatusTMP)
        {
            resetStatusTMP.text = msg;
            switch (type)
            {
                case ResetStatusType.Success: resetStatusTMP.color = resetSuccessColor; break;
                case ResetStatusType.Error: resetStatusTMP.color = resetErrorColor; break;
                default: resetStatusTMP.color = resetInfoColor; break;
            }
        }
        else
        {
            ShowMessage(msg);
        }
    }

    /// <summary>
    /// Use existing usernameInput + emailInput.
    /// If logged in: update contact email to provided email, then send recovery email.
    /// If not logged in: just send recovery email to provided email (if that email belongs to an account in this title).
    /// </summary>
    public void ResetPasswordWithUsernameAndEmail()
    {
        string username = usernameInput ? usernameInput.text.Trim() : "";
        string emailRaw = emailInput ? emailInput.text : "";
        string email = NormalizeEmail(emailRaw);

        if (string.IsNullOrEmpty(username))
        {
            ShowResetStatus("Please enter your username.", ResetStatusType.Error);
            return;
        }
        if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
        {
            ShowResetStatus("Please enter a valid email (e.g., you@example.com).", ResetStatusType.Error);
            return;
        }

        bool isLoggedIn = false;
        try { isLoggedIn = PlayFabClientAPI.IsClientLoggedIn(); } catch { isLoggedIn = false; }

        if (isLoggedIn)
        {
            // Optional: quick existence check for nicer errors
            PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest { Username = username },
                res =>
                {
                    // 1) Update contact email
                    PlayFabClientAPI.AddOrUpdateContactEmail(
                        new AddOrUpdateContactEmailRequest { EmailAddress = email },
                        _ =>
                        {
                            ShowResetStatus($"Contact email updated to {email}. Sending reset link…", ResetStatusType.Success);
                            SendRecoveryEmail(email);
                        },
                        err =>
                        {
                            if (err.Error == PlayFabErrorCode.EmailAddressNotAvailable)
                            {
                                ShowResetStatus("That email is already used by another account. Try a different email.", ResetStatusType.Error);
                            }
                            else
                            {
                                ShowResetStatus("Couldn’t update contact email: " + err.ErrorMessage, ResetStatusType.Error);
                            }
                            Debug.LogError("[AddOrUpdateContactEmail] " + err.GenerateErrorReport());
                        });
                },
                err =>
                {
                    if (err.Error == PlayFabErrorCode.AccountNotFound)
                        ShowResetStatus("No account found with that username. Please double-check and try again.", ResetStatusType.Error);
                    else
                        ShowResetStatus("Unable to verify username: " + err.ErrorMessage, ResetStatusType.Error);
                });
        }
        else
        {
            ShowResetStatus("Not logged in — sending reset link to the provided email (if it’s on an account).", ResetStatusType.Info);
            SendRecoveryEmail(email);
        }

        if (passwordResetPanel) passwordResetPanel.SetActive(true);
    }

    private void SendRecoveryEmail(string email)
    {
        string titleId = null;
        try { titleId = PlayFabSettings.staticSettings?.TitleId; } catch { }
        if (string.IsNullOrEmpty(titleId)) { try { titleId = PlayFabSettings.TitleId; } catch { } }

        if (string.IsNullOrEmpty(titleId))
        {
            ShowResetStatus("PlayFab TitleId is not configured. Set it in PlayFabSettings.", ResetStatusType.Error);
            return;
        }

        var req = new SendAccountRecoveryEmailRequest
        {
            Email = email,
            TitleId = titleId
        };

        PlayFabClientAPI.SendAccountRecoveryEmail(req,
            _ =>
            {
                ShowResetStatus($"We sent a password reset link to {email}. Check your inbox (and spam).", ResetStatusType.Success);
            },
            err =>
            {
                switch (err.Error)
                {
                    case PlayFabErrorCode.InvalidEmailAddress:
                        ShowResetStatus("That email isn’t valid for this title.", ResetStatusType.Error);
                        break;
                    case PlayFabErrorCode.AccountNotFound:
                        ShowResetStatus("No account uses that email in this game.", ResetStatusType.Error);
                        break;
                    default:
                        ShowResetStatus("Failed to send recovery email: " + err.ErrorMessage, ResetStatusType.Error);
                        break;
                }
                Debug.LogError("[SendAccountRecoveryEmail] " + err.GenerateErrorReport());
            });
    }

    // ===================== Errors / Utils =====================
    private void OnError(PlayFabError error)
    {
        Debug.LogError("PlayFab Error: " + error.GenerateErrorReport());
        ShowMessage("Error: " + error.ErrorMessage);

        var attempted = loginEmailInput ? loginEmailInput.text : "";
        bool wasUsernameAttempt = !string.IsNullOrEmpty(attempted) && attempted.IndexOf('@') < 0;

        if (wasUsernameAttempt)
        {
            switch (error.Error)
            {
                case PlayFabErrorCode.AccountNotFound:
                case PlayFabErrorCode.InvalidUsernameOrPassword:
                case PlayFabErrorCode.InvalidParams:
                    ShowMessage(
                        "Login failed with that username.\n" +
                        "• Reminder: Display Name is not always the account Username.\n" +
                        "• Try logging in with your Email once — we’ll sync your Username to match your display name.\n" +
                        "• Or use the password recovery to send a link to your email."
                    );
                    break;
                case PlayFabErrorCode.AccountBanned:
                    ShowMessage("This account is banned. Contact support if you believe this is an error.");
                    break;
            }
        }
    }

    private void ShowMessage(string msg)
    {
        if (messageText) messageText.text = msg;
        Debug.Log(msg);
    }

    private bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    }

    private string NormalizeEmail(string input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        var noWs = Regex.Replace(input, @"\s+", "");
        int at = noWs.IndexOf('@');
        if (at > 0 && at < noWs.Length - 1)
        {
            string local = noWs.Substring(0, at);
            string domain = noWs.Substring(at + 1).ToLowerInvariant();
            return $"{local}@{domain}";
        }
        return noWs;
    }

    private void OnAvatarReadyHandler(Texture2D tex) => ApplyAvatarTexture(tex);

    // ===================== Photon Matchmaking Helpers =====================
    private PhotonMatchmaker RequireMatchmaker()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return null;

#if UNITY_2023_1_OR_NEWER
        var mm = FindFirstObjectByType<PhotonMatchmaker>(FindObjectsInactive.Include);
#else
        var mm = FindObjectOfType<PhotonMatchmaker>();
#endif
        if (mm == null)
        {
            var go = new GameObject("PhotonMatchmaker");
            mm = go.AddComponent<PhotonMatchmaker>();
            mm.gameSceneName = gameSceneName;
        }
        return mm;
    }

    public void LoadGameAsHost(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        MatchContext.RoomCode = roomCode;
        MatchContext.IsHost = true;

        KillMatchmakerIfAny();
        var mm = RequireMatchmaker();
        if (mm != null) mm.HostMatch(roomCode);
    }

    public void LoadQuickMatchAsHost(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        MatchContext.RoomCode = roomCode;
        MatchContext.IsHost = true;

        KillMatchmakerIfAny();
        var mm = RequireMatchmaker();
        if (mm != null) mm.HostQuickMatch(roomCode);
    }

    public void LoadGameAsGuest(string roomCode)
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        MatchContext.RoomCode = roomCode;
        MatchContext.IsHost = false;

        KillMatchmakerIfAny();
        var mm = RequireMatchmaker();
        if (mm != null) mm.JoinMatch(roomCode);
    }

    public void BackToLogin(string loginSceneName)
    {
        SceneLoader.ReturningToLogin = true;
        try { MatchContext.ExitingToLogin = true; } catch { }

        if (_qmFlow != null) { StopCoroutine(_qmFlow); _qmFlow = null; }
        _qmBusy = false; _qmFinalized = false;

        KillMatchmakerIfAny();

        StopAutoRefresh();

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.IsMessageQueueRunning = false;

        AccountSession.Instance?.PrepareAutoLoginBeforeGoingBack(loginSceneName);

        ClearMatchContext();

        SceneLoader.EnsureAndGoBack(loginSceneName);

        ShowMessage("Returning to login…");
    }

    private void ClearMatchContext()
    {
        MatchContext.RoomCode = null;
        MatchContext.IsHost = false;
        MatchContext.NickName = null;
    }

    private void KillMatchmakerIfAny()
    {
        var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb.GetType().Name == "PhotonMatchmaker" && mb.gameObject && mb.gameObject.scene.IsValid())
            {
                Debug.Log("[PlayfabManager] Destroy lingering PhotonMatchmaker.");
                Destroy(mb.gameObject);
            }
        }
    }

    // ===================== QUICKMATCH =====================
    public void FindMatch()
    {
        if (_qmBusy) return;
        if (!_isLoggedIn) { ShowMessage("Please log in first."); return; }

        SceneLoader.ClearExitGuards();
        PhotonNetwork.IsMessageQueueRunning = true;

        ShowWaiting("Connecting to matchmaking…", true);

        _qmFlow = StartCoroutine(QuickMatchFlow());
    }

    private IEnumerator QuickMatchFlow()
    {
        _qmBusy = true;
        _qmFinalized = false;
        ShowMessage("Finding match…");

        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) { _qmBusy = false; yield break; }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(false);
            float t = 5f;
            while (PhotonNetwork.InRoom && t > 0f)
            {
                if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) { _qmBusy = false; yield break; }
                t -= Time.unscaledDeltaTime; yield return null;
            }
        }

        bool connected = false;
        yield return EnsureConnectedToMaster(connectTimeoutSeconds, connectRetries, "QuickMatch", r => connected = r);
        if (!connected)
        {
            ShowMessage("Matchmaking failed: could not connect to Master.");
            _qmBusy = false;
            yield break;
        }

        ShowWaiting("Searching for an open room…", false);

        bool joinCallAccepted = false;
        if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
        {
            joinCallAccepted = PhotonNetwork.JoinRandomRoom();
            Debug.Log($"[QuickMatch] JoinRandomRoom() call accepted: {joinCallAccepted}");
        }

        if (!joinCallAccepted)
        {
            yield return HostAfterBackoffOrRetry();
            _qmBusy = false; yield break;
        }

        float waitJoin = 4f;
        while (!_qmFinalized && waitJoin > 0f)
        {
            if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) { _qmBusy = false; yield break; }
            waitJoin -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_qmFinalized)
        {
            yield return HostAfterBackoffOrRetry();
        }

        _qmBusy = false;
    }

    private IEnumerator HostAfterBackoffOrRetry()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.6f));

        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) yield break;

        if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer &&
            PhotonNetwork.JoinRandomRoom())
        {
            ShowWaiting("Found an opponent! Joining…", false);

            float wait = 3f;
            while (!_qmFinalized && wait > 0f)
            {
                if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) yield break;
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (_qmFinalized) yield break;
        }

        var code = GenerateRoomCode();
        ShowMessage($"No rooms found. Hosting {code}…");
        ShowWaiting("No rooms found.\nHosting a public room…\nWaiting for an opponent…", false);

        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) yield break;

        _qmFinalized = true;
        LoadQuickMatchAsHost(code);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"[QuickMatch] JoinRandom failed ({returnCode}): {message}");
    }

    public override void OnJoinedRoom()
    {
        if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin) return;

        if (_qmBusy && !_qmFinalized)
        {
            _qmFinalized = true;
            ShowMessage($"Joined room {PhotonNetwork.CurrentRoom.Name}.");
            ShowWaiting("Match found! Joining game…", false);
        }

        if (PhotonNetwork.IsMasterClient && !_gameSceneLoadIssued)
        {
            _gameSceneLoadIssued = true;
            string sceneToLoad = string.IsNullOrEmpty(gameSceneName) ? "Pool_3dGame_Photon" : gameSceneName;
            SceneLoader.LoadNetworkScene(sceneToLoad);
        }
    }

    public override void OnRoomListUpdate(List<PR.RoomInfo> roomList)
    {
        _cachedRooms.Clear();
        _cachedRooms.AddRange(roomList);
        _lobbySeenOnce = true;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"[Photon] ConnectedToMaster ({PhotonNetwork.CloudRegion})");
        if (_userWaitingActive) ShowWaiting("Connected! Matching…", false);
    }

    public override void OnDisconnected(PR.DisconnectCause cause)
    {
        Debug.LogWarning($"[Photon] Disconnected: {cause}");
    }

    // ===================== Manual host/join =====================
    public void HostRandomRoom()
    {
        SceneLoader.ClearExitGuards();
        PhotonNetwork.IsMessageQueueRunning = true;

        var room = GenerateRoomCode();
        ShowMessage($"Hosting room {room}…");
        ShowWaiting("Hosting a public room…\nWaiting for an opponent…", true);

        LoadQuickMatchAsHost(room);
    }

    public void JoinByCode()
    {
        SceneLoader.ClearExitGuards();
        PhotonNetwork.IsMessageQueueRunning = true;

        var code = joinRoomCodeTMP ? joinRoomCodeTMP.text : "";
        if (string.IsNullOrEmpty(code))
        {
            ShowMessage("Enter a room code first.");
            return;
        }

        ShowMessage($"Joining {code}…");
        ShowWaiting("Joining room…", true);

        LoadGameAsGuest(code);
    }

    // ===================== Invite de-dupe =====================
    private void LoadDismissedInvites()
    {
        _dismissedInvites.Clear();
        var raw = PlayerPrefs.GetString(PlayerPrefsDismissedInvitesKey, "");
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var item in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                _dismissedInvites.Add(item);
        }
    }

    private void SaveDismissedInvites()
    {
        PlayerPrefs.SetString(PlayerPrefsDismissedInvitesKey, string.Join(";", _dismissedInvites));
        PlayerPrefs.Save();
    }

    private void DismissInvite(string inviterId, string roomCode)
    {
        if (string.IsNullOrEmpty(inviterId) || string.IsNullOrEmpty(roomCode)) return;
        _dismissedInvites.Add(MakeInviteKey(inviterId, roomCode));
        SaveDismissedInvites();
    }

    //====================== Helpers =====================
    private bool IsPlayFabLoggedInSafely()
    {
        try { return PlayFabClientAPI.IsClientLoggedIn(); }
        catch { return false; }
    }

    private bool HasValidPhotonAppId()
    {
        var settings = PhotonNetwork.PhotonServerSettings;
        if (settings == null) { Debug.LogError("[Photon] PhotonServerSettings asset is missing."); return false; }
        var appId = settings.AppSettings.AppIdRealtime;
        if (string.IsNullOrEmpty(appId) || appId.Equals("YOUR_APP_ID", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[Photon] AppIdRealtime is not set in PhotonServerSettings.");
            return false;
        }
        return true;
    }

    private IEnumerator EnsureConnectedToMaster(float timeoutSeconds, int retries, string context, Action<bool> finished)
    {
        if (!string.IsNullOrEmpty(MatchContext.NickName)) PhotonNetwork.NickName = MatchContext.NickName;

        if (!HasValidPhotonAppId())
        {
            ShowMessage("Photon AppId not configured. Set it in PhotonServerSettings.");
            finished?.Invoke(false);
            yield break;
        }

        if (!string.IsNullOrEmpty(regionOverride))
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = regionOverride.Trim().ToLowerInvariant();
            Debug.Log($"[Photon] Using region override: {PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion}");
        }

        int attempts = Mathf.Max(1, retries + 1);
        while (attempts-- > 0)
        {
            if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
            {
                finished?.Invoke(true);
                yield break;
            }

            if (!PhotonNetwork.IsConnected || PhotonNetwork.NetworkClientState == PR.ClientState.Disconnected)
            {
                Debug.Log($"[Photon] Connecting to Master… (context={context})");
                bool started = PhotonNetwork.ConnectUsingSettings();
                if (!started) { Debug.LogWarning("[Photon] ConnectUsingSettings() returned false, will retry."); }
            }

            float t = Mathf.Max(2f, timeoutSeconds);
            while (t > 0f &&
                   PhotonNetwork.NetworkClientState != PR.ClientState.ConnectedToMasterServer &&
                   PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
            {
                if (MatchContext.ExitingToLogin || SceneLoader.ReturningToLogin)
                {
                    finished?.Invoke(false);
                    yield break;
                }
                t -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (PhotonNetwork.NetworkClientState == PR.ClientState.ConnectedToMasterServer)
            {
                Debug.Log("[Photon] Connected to Master.");
                finished?.Invoke(true);
                yield break;
            }

            Debug.LogWarning($"[Photon] Connect attempt failed/timed out (state={PhotonNetwork.NetworkClientState}).");
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.25f, 0.75f));

            if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
            {
                PhotonNetwork.Disconnect();
                float settle = 0.25f;
                while (settle > 0f && PhotonNetwork.NetworkClientState != PR.ClientState.Disconnected)
                {
                    settle -= Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        finished?.Invoke(false);
    }

    // ======== UI helpers to render leaderboard/friends rows (your prefabs) ========
    private class LeaderboardRowUI : MonoBehaviour
    {
        public TextMeshProUGUI pos, name, value;
        public void Set(int place, string displayName, int wins)
        {
            if (pos) pos.text = $"#{place}";
            if (name) name.text = displayName;
            if (value) value.text = wins.ToString();
        }
    }

    private class FriendRowUI : MonoBehaviour
    {
        public TextMeshProUGUI nameTMP, statusTMP;
        public Button inviteBtn;
        public void Set(string displayName, string status, Action onInvite)
        {
            if (nameTMP) nameTMP.text = displayName;
            if (statusTMP) statusTMP.text = status;
            if (inviteBtn)
            {
                inviteBtn.onClick.RemoveAllListeners();
                if (onInvite != null) inviteBtn.onClick.AddListener(() => onInvite());
            }
        }
    }

    private class InviteRowUI : MonoBehaviour
    {
        public TextMeshProUGUI titleTMP, codeTMP;
        public Button acceptBtn, declineBtn;
        public void Set(string title, string code, Action onAccept, Action onDecline)
        {
            if (titleTMP) titleTMP.text = title;
            if (codeTMP) codeTMP.text = code;
            if (acceptBtn)
            {
                acceptBtn.onClick.RemoveAllListeners();
                if (onAccept != null) acceptBtn.onClick.AddListener(() => onAccept());
            }
            if (declineBtn)
            {
                declineBtn.onClick.RemoveAllListeners();
                if (onDecline != null) declineBtn.onClick.AddListener(() => onDecline());
            }
        }
    }
}
