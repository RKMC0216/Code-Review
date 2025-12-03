using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;

/// <summary>
/// Persisted session for PlayFab login details and avatar across scenes.
/// - Stores email/username, password (lightly obfuscated), and avatar URL/texture
/// - Singleton + DontDestroyOnLoad
/// - Can auto-login when coming back to the Login scene
/// </summary>
public class AccountSession : MonoBehaviour
{
    public static AccountSession Instance { get; private set; }

    // Public read-only properties other scripts can consume
    public string EmailOrUsername { get; private set; }
    public string DisplayName { get; private set; }
    public string AvatarUrl { get; private set; }
    public Texture2D AvatarTexture { get; private set; }

    // If you want to know when the avatar is ready for UI binding.
    public event Action<Texture2D> OnAvatarReady;
    public event Action OnAutoLoginSucceeded;
    public event Action<string> OnAutoLoginFailed;

    // ---- Settings ----
    [Header("Auto-Login")]
    [Tooltip("Set true to auto-login the next time a login scene is shown (set via PrepareAutoLoginBeforeGoingBack()).")]
    public bool autoLoginNextTime = false;

    [Tooltip("Name of your login scene. If left empty, we will attempt auto-login when we detect a PlayFab login panel in the scene.")]
    public string loginSceneName = ""; // optional

    [Tooltip("If true, we will trigger auto-login on scene load when autoLoginNextTime==true and we are in the login scene.")]
    public bool triggerAutoLoginOnSceneLoad = true;

    // ---- PlayerPrefs Keys (simple) ----
    const string PP_EMAIL_OR_USERNAME = "pf_last_user";
    const string PP_PASSWORD_OBFUSC = "pf_last_pass_obf";
    const string PP_AVATAR_URL = "pf_last_avatar_url";

    // NOTE: This is ONLY light obfuscation, not real encryption.
    // For production, use a proper secure store (platform-specific keychain/keystore).
    const string OBFUSCATION_KEY = "bada-billiards-key";

    // Cached flag that auto-login has been attempted in this scene load
    private bool _attemptedAutoLoginThisScene = false;

    // ---- NEW: throttle-safe auto-login guards ----
    private bool _autoLoginInFlight = false;
    private int _throttleRetryCount = 0;
    private float _nextAutoLoginAllowedTime = 0f; // gate via Time.unscaledTime

    // ------------- Unity lifecycle -------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCached();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If configured with a specific login scene name, check it.
        bool isLoginSceneMatch = !string.IsNullOrEmpty(loginSceneName) &&
                                 string.Equals(scene.name, loginSceneName, StringComparison.Ordinal);

        // Heuristic fallback: If you didn’t specify a scene name, we can still try to auto-login once per scene load.
        if (triggerAutoLoginOnSceneLoad && autoLoginNextTime && !_attemptedAutoLoginThisScene && (isLoginSceneMatch || string.IsNullOrEmpty(loginSceneName)))
        {
            _attemptedAutoLoginThisScene = true;
            TryAutoLogin();
        }
        else
        {
            _attemptedAutoLoginThisScene = false;
        }
    }

    // ------------- Public API -------------

    /// <summary>
    /// Call this right after a successful login in your PlayfabManager:
    /// AccountSession.Instance.OnPlayfabLoginSuccess(inputUser, inputPass, loginResult);
    /// </summary>
    public void OnPlayfabLoginSuccess(string emailOrUsername, string plainPassword, LoginResult result)
    {
        EmailOrUsername = emailOrUsername;
        SavePassword(plainPassword);

        // Capture display name (if present)
        DisplayName = result?.InfoResultPayload?.PlayerProfile?.DisplayName;
        // Capture avatar URL (if present)
        string url = result?.InfoResultPayload?.PlayerProfile?.AvatarUrl;
        if (!string.IsNullOrEmpty(url))
        {
            AvatarUrl = url;
            PlayerPrefs.SetString(PP_AVATAR_URL, AvatarUrl);
            PlayerPrefs.Save();
            StartCoroutine(DownloadAvatarCoroutine(AvatarUrl));
        }

        // We’re logged in now; no need to autoLogin on the very next scene by default
        autoLoginNextTime = false;
    }

    /// <summary>
    /// Call this when you press "Back" to go to the login menu (BEFORE loading that scene).
    /// This will make the next appearance of the login scene automatically log in.
    /// Optionally pass the scene name you'll load; if omitted, we rely on heuristic.
    /// </summary>
    public void PrepareAutoLoginBeforeGoingBack(string loginScene = "")
    {
        autoLoginNextTime = true;
        if (!string.IsNullOrEmpty(loginScene))
            loginSceneName = loginScene;
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Manually trigger auto-login (e.g., from a UI button in the login scene).
    /// </summary>
    public void TryAutoLogin()
    {
        // Debounce + cooldown gate
        if (_autoLoginInFlight || Time.unscaledTime < _nextAutoLoginAllowedTime)
        {
            OnAutoLoginFailed?.Invoke("Auto-login suppressed (cooldown/in-flight).");
            return;
        }

        if (!HasSavedCredentials())
        {
            OnAutoLoginFailed?.Invoke("No saved credentials.");
            return;
        }

        string user = PlayerPrefs.GetString(PP_EMAIL_OR_USERNAME, "");
        string pass = LoadPassword();

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            OnAutoLoginFailed?.Invoke("Saved credentials are empty.");
            return;
        }

        _autoLoginInFlight = true;

        // Build combined info like your PlayfabManager uses
        var info = new GetPlayerCombinedInfoRequestParams
        {
            GetPlayerProfile = true,
            GetUserInventory = true,
            GetUserVirtualCurrency = true,
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            }
        };

        if (IsValidEmail(user))
        {
            var req = new LoginWithEmailAddressRequest
            {
                Email = user,
                Password = pass,
                InfoRequestParameters = info
            };
            PlayFabClientAPI.LoginWithEmailAddress(req, OnAutoLoginSuccessInternal, OnAutoLoginErrorInternal);
        }
        else
        {
            var req = new LoginWithPlayFabRequest
            {
                Username = user,
                Password = pass,
                InfoRequestParameters = info
            };
            PlayFabClientAPI.LoginWithPlayFab(req, OnAutoLoginSuccessInternal, OnAutoLoginErrorInternal);
        }
    }

    /// <summary>
    /// Clear the cached credentials (e.g., on explicit Logout).
    /// </summary>
    public void ClearSavedCredentials()
    {
        PlayerPrefs.DeleteKey(PP_EMAIL_OR_USERNAME);
        PlayerPrefs.DeleteKey(PP_PASSWORD_OBFUSC);
        PlayerPrefs.Save();
        EmailOrUsername = null;
    }

    /// <summary>
    /// For use in other scenes: fetch the avatar texture if we have a URL but texture is not downloaded yet.
    /// </summary>
    public void EnsureAvatarDownloaded()
    {
        if (AvatarTexture == null && !string.IsNullOrEmpty(AvatarUrl))
        {
            StartCoroutine(DownloadAvatarCoroutine(AvatarUrl));
        }
    }

    // ------------- Internal helpers -------------

    private void OnAutoLoginSuccessInternal(LoginResult res)
    {
        _autoLoginInFlight = false;
        _throttleRetryCount = 0;
        _nextAutoLoginAllowedTime = Time.unscaledTime + 5f; // small post-success cooldown

        // Keep internal cache up-to-date
        EmailOrUsername = PlayerPrefs.GetString(PP_EMAIL_OR_USERNAME, EmailOrUsername);
        DisplayName = res?.InfoResultPayload?.PlayerProfile?.DisplayName;

        string url = res?.InfoResultPayload?.PlayerProfile?.AvatarUrl;
        if (!string.IsNullOrEmpty(url))
        {
            AvatarUrl = url;
            PlayerPrefs.SetString(PP_AVATAR_URL, AvatarUrl);
            PlayerPrefs.Save();
            StartCoroutine(DownloadAvatarCoroutine(AvatarUrl));
        }

        autoLoginNextTime = false; // we've just logged in
        OnAutoLoginSucceeded?.Invoke();
    }

    private void OnAutoLoginErrorInternal(PlayFab.PlayFabError err)
    {
        _autoLoginInFlight = false;
        autoLoginNextTime = false; // stop regular retries unless we decide to backoff-retry

        // throttling detection
        bool isThrottled =
            err.Error == PlayFab.PlayFabErrorCode.APIClientRequestRateLimitExceeded ||
            err.Error == PlayFab.PlayFabErrorCode.APIConcurrentRequestLimitExceeded ||
            (err.ErrorMessage != null &&
             err.ErrorMessage.IndexOf("throttl", StringComparison.OrdinalIgnoreCase) >= 0); // covers message variants

        if (isThrottled && _throttleRetryCount < 2)
        {
            _throttleRetryCount++;
            float delay = Mathf.Pow(2f, _throttleRetryCount) + UnityEngine.Random.Range(0f, 0.75f); // jittered backoff
            _nextAutoLoginAllowedTime = Time.unscaledTime + delay;
            Debug.LogWarning($"[AccountSession] Throttled during auto-login. Retrying in {delay:0.00}s (attempt #{_throttleRetryCount}).");
            StartCoroutine(RetryAutoLoginAfter(delay));
            return;
        }

        OnAutoLoginFailed?.Invoke(err.ErrorMessage);
        Debug.LogError("[AccountSession] Auto-Login failed: " + err.GenerateErrorReport());
    }


    private IEnumerator RetryAutoLoginAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        autoLoginNextTime = true; // allow a single backoff retry
        TryAutoLogin();
    }

    private IEnumerator DownloadAvatarCoroutine(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[AccountSession] Avatar download failed: " + req.error);
                yield break;
            }
            AvatarTexture = DownloadHandlerTexture.GetContent(req);
            OnAvatarReady?.Invoke(AvatarTexture);
        }
    }

    private void LoadCached()
    {
        if (PlayerPrefs.HasKey(PP_EMAIL_OR_USERNAME))
            EmailOrUsername = PlayerPrefs.GetString(PP_EMAIL_OR_USERNAME, "");

        if (PlayerPrefs.HasKey(PP_AVATAR_URL))
            AvatarUrl = PlayerPrefs.GetString(PP_AVATAR_URL, "");

        // Do not pre-populate password in memory unless needed
    }

    private bool HasSavedCredentials()
    {
        return PlayerPrefs.HasKey(PP_EMAIL_OR_USERNAME) && PlayerPrefs.HasKey(PP_PASSWORD_OBFUSC);
    }

    private void SavePassword(string plain)
    {
        if (!string.IsNullOrEmpty(EmailOrUsername))
            PlayerPrefs.SetString(PP_EMAIL_OR_USERNAME, EmailOrUsername);

        // Light obfuscation
        string obf = Obfuscate(plain, OBFUSCATION_KEY);
        PlayerPrefs.SetString(PP_PASSWORD_OBFUSC, obf);
        PlayerPrefs.Save();
    }

    private string LoadPassword()
    {
        if (!PlayerPrefs.HasKey(PP_PASSWORD_OBFUSC)) return "";
        string obf = PlayerPrefs.GetString(PP_PASSWORD_OBFUSC, "");
        return Deobfuscate(obf, OBFUSCATION_KEY);
    }

    private static bool IsValidEmail(string email)
    {
        // Basic check
        return !string.IsNullOrEmpty(email) && email.Contains("@") && email.Contains(".");
    }

    // --------- very simple XOR+Base64 obfuscation (not secure!) ---------
    private static string Obfuscate(string input, string key)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var bytes = Encoding.UTF8.GetBytes(input);
        var k = Encoding.UTF8.GetBytes(key);
        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= k[i % k.Length];
        return Convert.ToBase64String(bytes);
    }

    private static string Deobfuscate(string input, string key)
    {
        if (string.IsNullOrEmpty(input)) return "";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input); }
        catch { return ""; }
        var k = Encoding.UTF8.GetBytes(key);
        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= k[i % k.Length];
        return Encoding.UTF8.GetString(bytes);
    }
}
