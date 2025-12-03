using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerProfileBanner_PUN : MonoBehaviourPunCallbacks
{
    // Shared keys (match TurnManager)
    public const string KeyName = "pf_name";
    public const string KeyAvatarUrl = "pf_avatarUrl";

    public enum SourceMode { PhotonLocal, PhotonRemoteOther, PhotonByActorNumber, LocalAccountSession }

    [Header("Source")]
    public SourceMode source = SourceMode.PhotonLocal;
    [Tooltip("Used only when SourceMode == PhotonByActorNumber")]
    public int actorNumber = 0;

    [Header("UI")]
    public TextMeshProUGUI displayNameTMP;
    public RawImage avatarRawImage;                 // use RawImage OR Image (not both)
    public Image avatarUIImage;
    public Sprite avatarFallbackSprite;
    public Texture2D avatarFallbackTexture;

    [Header("Behaviour")]
    public bool allowEmailOrUsernameAsName = true;
    public bool debugLogs = false;

    [Header("Retry")]
    [Tooltip("How long we keep polling for the opponent to appear (0 = forever).")]
    public float bindRetrySeconds = 0f;
    [Tooltip("How long we keep polling for pf_name / NickName after binding (0 = forever).")]
    public float profileWaitSeconds = 0f;
    [Tooltip("Polling interval while waiting for player/properties.")]
    public float pollInterval = 0.25f;

    static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();
    Player bound;
    Coroutine runCo;

    // ---------- lifecycle ----------
    public override void OnEnable() { ForceRebind(); }
    void Start() { ForceRebind(); }
    public override void OnJoinedRoom() { ForceRebind(); }
    public override void OnPlayerEnteredRoom(Player _) { ForceRebind(); }
    public override void OnPlayerLeftRoom(Player _) { ForceRebind(); }

    /// <summary>Public hook used by TurnManager to force a refresh.</summary>
    public void ForceRebind()
    {
        if (!isActiveAndEnabled) return;
        if (runCo != null) StopCoroutine(runCo);
        runCo = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (source == SourceMode.LocalAccountSession)
        {
            ApplyFromLocalSession();
            yield break;
        }

        while (!PhotonNetwork.InRoom) yield return null;

        // 1) WAIT FOR TARGET PLAYER
        float tBind = bindRetrySeconds;
        while (true)
        {
            bound = ResolveTargetPlayer();
            if (bound != null) break;

            if (bindRetrySeconds > 0f)
            {
                tBind -= pollInterval;
                if (tBind <= 0f) { ShowFallback("No player bound yet; showing fallback."); yield break; }
            }
            yield return new WaitForSeconds(pollInterval);
        }
        if (debugLogs) Debug.Log($"[Banner] Bound to actor {bound.ActorNumber}");

        // 2) WAIT FOR PROFILE (pf_name or NickName)
        float tProfile = profileWaitSeconds;
        while (!HasAnyName(bound))
        {
            if (profileWaitSeconds > 0f)
            {
                tProfile -= pollInterval;
                if (tProfile <= 0f) break; // apply whatever we have
            }
            yield return new WaitForSeconds(pollInterval);
        }

        ApplyFromTarget(bound);
    }

    Player ResolveTargetPlayer()
    {
        switch (source)
        {
            case SourceMode.PhotonLocal:
                return PhotonNetwork.LocalPlayer;

            case SourceMode.PhotonRemoteOther:
                var others = PhotonNetwork.PlayerListOthers;
                return (others != null && others.Length > 0) ? others[0] : null;

            case SourceMode.PhotonByActorNumber:
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p.ActorNumber == actorNumber) return p;
                return null;
        }
        return null;
    }

    // ---------- apply ----------
    void ApplyFromTarget(Player p)
    {
        // NAME: pf_name → NickName → local AccountSession (if local) → "Player"
        string name = TryGetString(p.CustomProperties, KeyName);
        if (string.IsNullOrEmpty(name)) name = p.NickName;
        if (string.IsNullOrEmpty(name) && source == SourceMode.PhotonLocal && AccountSession.Instance != null)
            name = !string.IsNullOrEmpty(AccountSession.Instance.DisplayName)
                    ? AccountSession.Instance.DisplayName
                    : (allowEmailOrUsernameAsName ? AccountSession.Instance.EmailOrUsername : "Player");
        if (displayNameTMP) displayNameTMP.text = string.IsNullOrEmpty(name) ? "Player" : name;

        // AVATAR: pf_avatarUrl → (local AccountSession if local) → fallback
        string url = TryGetString(p.CustomProperties, KeyAvatarUrl);
        if (!string.IsNullOrEmpty(url))
        {
            if (s_cache.TryGetValue(url, out var cached)) ApplyTexture(cached);
            else StartCoroutine(DownloadAndApply(url));
        }
        else if (source == SourceMode.PhotonLocal && AccountSession.Instance != null && AccountSession.Instance.AvatarTexture != null)
        {
            ApplyTexture(AccountSession.Instance.AvatarTexture);
        }
        else ApplyFallback();

        if (debugLogs) Debug.Log($"[Banner] Applied actor {p.ActorNumber} name='{name}'");
    }

    void ApplyFromLocalSession()
    {
        var s = AccountSession.Instance;
        string name = "Player";
        if (s != null)
        {
            if (!string.IsNullOrEmpty(s.DisplayName)) name = s.DisplayName;
            else if (allowEmailOrUsernameAsName && !string.IsNullOrEmpty(s.EmailOrUsername)) name = s.EmailOrUsername;

            if (s.AvatarTexture != null) ApplyTexture(s.AvatarTexture);
            else if (!string.IsNullOrEmpty(s.AvatarUrl)) StartCoroutine(DownloadAndApply(s.AvatarUrl));
            else ApplyFallback();
        }
        else ApplyFallback();

        if (displayNameTMP) displayNameTMP.text = name;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        if (bound == null || targetPlayer != bound) return;

        if (changedProps.ContainsKey(KeyName) || changedProps.ContainsKey(KeyAvatarUrl))
            ApplyFromTarget(bound);
    }

    // ---------- helpers ----------
    static string TryGetString(PhotonHashtable h, string key)
    {
        if (h == null) return null;
        return h.TryGetValue(key, out var o) ? o as string : null;
    }

    bool HasAnyName(Player p)
    {
        if (p == null) return false;
        string n = TryGetString(p.CustomProperties, KeyName);
        if (!string.IsNullOrEmpty(n)) return true;
        return !string.IsNullOrEmpty(p.NickName);
    }

    IEnumerator DownloadAndApply(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { ApplyFallback(); yield break; }
            var tex = DownloadHandlerTexture.GetContent(req);
            s_cache[url] = tex;
            ApplyTexture(tex);
        }
    }

    void ApplyTexture(Texture2D tex)
    {
        if (avatarRawImage) { avatarRawImage.texture = tex; return; }
        if (avatarUIImage)
        {
            if (tex == null) { ApplyFallback(); return; }
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            sp.name = "avatar";
            avatarUIImage.sprite = sp;
            avatarUIImage.preserveAspect = true;
        }
    }

    void ApplyFallback()
    {
        if (avatarRawImage && avatarFallbackTexture) avatarRawImage.texture = avatarFallbackTexture;
        else if (avatarUIImage && avatarFallbackSprite) { avatarUIImage.sprite = avatarFallbackSprite; avatarUIImage.preserveAspect = true; }
        if (displayNameTMP && string.IsNullOrEmpty(displayNameTMP.text)) displayNameTMP.text = "Player";
    }

    void ShowFallback(string msg)
    {
        ApplyFallback();
        if (displayNameTMP) displayNameTMP.text = "Player";
        if (debugLogs) Debug.Log("[Banner] " + msg);
    }
}
