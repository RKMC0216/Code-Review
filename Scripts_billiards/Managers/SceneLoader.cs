using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
#endif

/// Robust scene navigation helper.
/// Call SceneLoader.EnsureAndGoBack() to leave any Photon room and load the login scene.
/// Also provides LoadNetworkScene(scene) to safely load forward while pausing the Photon queue.
[DisallowMultipleComponent]
public class SceneLoader : MonoBehaviour
{
    [Header("Optional Loading UI")]
    public GameObject loadingPanel;
    public UnityEngine.UI.Slider progressBar;

    [Header("Config")]
    [SerializeField] private string loginSceneName = "Login";
    [SerializeField] private float leaveTimeoutSeconds = 6f;
    [SerializeField] private float disconnectTimeoutSeconds = 6f;
    [SerializeField] private bool disconnectOnBack = true;        // strongly recommended
    [SerializeField] private bool destroyMatchmakerOnBack = true; // kill matchmaker(s) so they can't reload the game
    [SerializeField] private bool reenableQueueAfterLoad = true;  // re-enable after we’re safely in Login
    [SerializeField] private bool verbose = true;

    /// True while transitioning back to Login (legacy guard some scripts check).
    public static bool ReturningToLogin = false;

    // Global, cross-instance guard to avoid re-entrancy.
    private static bool _globalInProgress = false;

    private bool _localInProgress = false;

    // -------- NEW: forward-load guard --------
    private static bool _forwardLoadInProgress = false;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // ----------------- PUBLIC API -----------------

    /// <summary>
    /// Load a gameplay scene safely while pausing Photon’s message queue
    /// to avoid "Failed to find PhotonView with ID=.." during spawn.
    /// Master can call this; if AutomaticallySyncScene is true, others follow.
    /// If you're not using Photon scene sync, this still pauses/resumes correctly.
    /// </summary>
    public static void LoadNetworkScene(string sceneName)
    {
        SceneLoader runner = GetOrCreateRunner();
        if (_forwardLoadInProgress)
        {
            if (runner.verbose) Debug.Log("[SceneLoader] Forward load already in progress.");
            return;
        }
        runner.StartCoroutine(runner.ForwardLoadFlow(sceneName));
    }

    public void GoBack()
    {
        if (_localInProgress || _globalInProgress)
        {
            if (verbose) Debug.Log("[SceneLoader] Already busy.");
            return;
        }

        if (verbose) Debug.Log("[SceneLoader] GoBack() invoked.");
        gameObject.SetActive(true);
        enabled = true;
        StartCoroutine(GoBackFlow());
    }

    /// Safe one-liner (creates a runner if none exists, even if the original is inactive).
    public static void EnsureAndGoBack(string overrideLoginScene = null, bool strongDisconnect = true)
    {
        SceneLoader loader = GetOrCreateRunner();

        if (!string.IsNullOrEmpty(overrideLoginScene))
            loader.loginSceneName = overrideLoginScene;

        loader.disconnectOnBack = strongDisconnect;
        loader.GoBack();
    }

    /// Clear both global guards (call this when user explicitly clicks a match button in Login).
    public static void ClearExitGuards()
    {
        ReturningToLogin = false;
        try { MatchContext.ExitingToLogin = false; } catch { }
    }

    // ----------------- FORWARD LOAD (NEW) -----------------
    private IEnumerator ForwardLoadFlow(string sceneName)
    {
        _forwardLoadInProgress = true;

#if PHOTON_UNITY_NETWORKING
        // Let Photon sync scenes if enabled. We still pause queue locally to suppress early Ownership/RPC.
        // (If Master calls PhotonNetwork.LoadLevel, remotes will also load the same scene.)
        if (PhotonNetwork.AutomaticallySyncScene && PhotonNetwork.IsMasterClient)
        {
            if (verbose) Debug.Log($"[SceneLoader] Master loading scene via PhotonNetwork.LoadLevel('{sceneName}')");
            PhotonNetwork.IsMessageQueueRunning = false; // pause immediately on master
            PhotonNetwork.LoadLevel(sceneName);
            yield return ResumeQueueAndAnnounceReady();
            _forwardLoadInProgress = false;
            yield break;
        }
#endif
        // Generic path (works even if not using Photon scene sync)
#if PHOTON_UNITY_NETWORKING
        PhotonNetwork.IsMessageQueueRunning = false;
#endif
        yield return LoadSceneAsync(sceneName);
        yield return ResumeQueueAndAnnounceReady();
        _forwardLoadInProgress = false;
    }

    private IEnumerator ResumeQueueAndAnnounceReady()
    {
        // Wait one frame so all PhotonViews in the new scene have Awakened/Enabled.
        yield return null;

#if PHOTON_UNITY_NETWORKING
        PhotonNetwork.IsMessageQueueRunning = true;
#endif
        // Inform the TurnManager that this client’s scene is ready.
        var tm = FindFirstObjectByType<TurnManager8Ball_PUN>();
        if (tm != null)
        {
            if (verbose) Debug.Log("[SceneLoader] Announcing scene ready to TurnManager8Ball_PUN.");
            tm.ClientSceneReady();
        }
    }

    // ----------------- BACK TO LOGIN (your original) -----------------
    private IEnumerator GoBackFlow()
    {
        _localInProgress = true;
        _globalInProgress = true;

        if (Time.timeScale <= 0f) Time.timeScale = 1f;

        // Coordinate with other systems that check these.
        ReturningToLogin = true;
        try { MatchContext.ExitingToLogin = true; } catch { /* ignored if class not present */ }

#if PHOTON_UNITY_NETWORKING
        // Prevent being yanked by any incoming scene syncs.
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.IsMessageQueueRunning = false;

        // Destroy any lingering matchmaker(s) that might auto-load the game.
        if (destroyMatchmakerOnBack)
        {
            DestroyAll<PhotonMatchmaker>();
            // Add other loaders if you have them:
            // DestroyAll<YourOtherMatchmaker>();
        }

        // Leave the room (so master's scene loads won't pull us back).
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.NetworkClientState != ClientState.Leaving)
                PhotonNetwork.LeaveRoom(false);

            float t = leaveTimeoutSeconds;
            while (PhotonNetwork.InRoom && t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Optionally disconnect to be absolutely safe.
        if (disconnectOnBack && PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            float td = disconnectTimeoutSeconds;
            while (PhotonNetwork.IsConnected && td > 0f)
            {
                td -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
#endif

        // Load Login scene.
        string sceneName = string.IsNullOrEmpty(loginSceneName) ? "Login" : loginSceneName;
        yield return LoadSceneAsync(sceneName);

        // After load, do a short sweep that kills any late-spawned matchmakers.
        yield return PostLoginGuardSweep();

#if PHOTON_UNITY_NETWORKING
        // Safe to resume message queue now that we're out of any room (or disconnected).
        if (reenableQueueAfterLoad)
            PhotonNetwork.IsMessageQueueRunning = true;
#endif

        // We intentionally leave the guards ON here. Clear them only when user clicks a match button.
        _localInProgress = false;
        _globalInProgress = false;
    }

    // Keep for any places that still call it (sync load).
    public void load(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (loadingPanel) loadingPanel.SetActive(true);
        if (progressBar) progressBar.value = 0f;

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            if (verbose) Debug.LogWarning("[SceneLoader] LoadSceneAsync returned null. Using synchronous load.");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield break;
        }

        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            if (progressBar) progressBar.value = Mathf.Clamp01(op.progress / 0.9f);
            if (op.progress >= 0.9f)
                op.allowSceneActivation = true;

            yield return null; // unscaled
        }
    }

    /// Guard: for a short period after landing in Login, keep nuking any matchmaker that might appear.
    private IEnumerator PostLoginGuardSweep()
    {
        float guardFor = 1.25f; // seconds
        float t = 0f;
        while (t < guardFor)
        {
            t += Time.unscaledDeltaTime;
#if PHOTON_UNITY_NETWORKING
            // Keep scene sync off in login.
            PhotonNetwork.AutomaticallySyncScene = false;
#endif
            DestroyAll<PhotonMatchmaker>();
            yield return null;
        }
    }

    // ----------------- Utils -----------------
    private static SceneLoader GetOrCreateRunner()
    {
#if UNITY_2023_1_OR_NEWER
        var runner = Object.FindFirstObjectByType<SceneLoader>(FindObjectsInactive.Include);
#else
        var runner = Object.FindObjectOfType<SceneLoader>();
        if (runner == null)
        {
            var all = Resources.FindObjectsOfTypeAll<SceneLoader>();
            foreach (var s in all)
            {
                if (s.gameObject.scene.IsValid()) { runner = s; break; }
            }
        }
#endif
        if (runner == null)
        {
            var go = new GameObject("SceneLoader_Runtime");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<SceneLoader>();
        }
        return runner;
    }

    private void DestroyAll<T>() where T : Component
    {
#if UNITY_2023_1_OR_NEWER
        var objs = Resources.FindObjectsOfTypeAll<T>();
        foreach (var o in objs)
        {
            if (!o || !o.gameObject) continue;
            if (!o.gameObject.scene.IsValid()) continue; // skip assets/prefabs
            if (verbose) Debug.Log($"[SceneLoader] Destroying lingering {typeof(T).Name} on '{o.gameObject.name}'.");
            Destroy(o.gameObject);
        }
#else
        // Runtime actives
        var runtime = Object.FindObjectsOfType<T>();
        foreach (var o in runtime)
        {
            if (verbose) Debug.Log($"[SceneLoader] Destroying lingering {typeof(T).Name} on '{o.gameObject.name}'.");
            Destroy(o.gameObject);
        }
        // Inactive too
        var all = Resources.FindObjectsOfTypeAll<T>();
        foreach (var o in all)
        {
            if (!o || !o.gameObject) continue;
            if (!o.gameObject.scene.IsValid()) continue;
            if (o.gameObject.hideFlags != HideFlags.None) continue;
            if (verbose) Debug.Log($"[SceneLoader] Destroying lingering (inactive) {typeof(T).Name} on '{o.gameObject.name}'.");
            Destroy(o.gameObject);
        }
#endif
    }
}
