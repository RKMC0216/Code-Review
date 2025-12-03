using System.Collections; // for IEnumerator
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable; // <-- alias fixes ambiguity

public class PlayerProfileLoader : MonoBehaviourPunCallbacks
{
    public enum SourceMode { LocalAccountSession, PhotonLocal, PhotonRemoteOther, PhotonByActorNumber }

    public const string KeyUIReady = "pf_uiReady"; // local banner ready flag

    [Header("Source")]
    public SourceMode source = SourceMode.PhotonLocal;
    public int actorNumber = 0; // used if Source = PhotonByActorNumber

    [Header("UI Targets")]
    public TextMeshProUGUI displayNameTMP;
    public RawImage avatarRawImage;  // optional
    public Image avatarUIImage;      // optional

    [Header("Fallback visuals")]
    public Sprite avatarFallbackSprite;
    public Texture2D avatarFallbackTexture;
    public bool allowEmailOrUsernameAsName = true;

    [Header("Options")]
    [Tooltip("Sets pf_uiReady=true once this LOCAL banner shows name+avatar/fallback.")]
    public bool markLocalUIReady = true;

    static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();
    Player bound;
    bool subscribedSession = false;
    bool localReadyMarked = false;

    public override void OnEnable()
    {
        Bind(); // may be a no-op pre-join; we re-bind in OnJoinedRoom
    }

    public override void OnJoinedRoom()
    {
        Bind(); // bind once we actually have players
    }

    void Start()
    {
        if (displayNameTMP && string.IsNullOrEmpty(displayNameTMP.text))
            displayNameTMP.text = "Player";

        if (bound == null && (source == SourceMode.PhotonLocal || source == SourceMode.PhotonRemoteOther || source == SourceMode.PhotonByActorNumber))
            Bind();
    }

    public override void OnDisable()
    {
        if (subscribedSession && AccountSession.Instance != null)
            AccountSession.Instance.OnAvatarReady -= OnSessionAvatarReady;
        subscribedSession = false;
    }

    // ---------------- binding ----------------
    void Bind()
    {
        switch (source)
        {
            case SourceMode.LocalAccountSession:
                BindLocalSession();
                break;
            case SourceMode.PhotonLocal:
            case SourceMode.PhotonRemoteOther:
            case SourceMode.PhotonByActorNumber:
                BindPhoton();
                break;
        }
    }

    void BindLocalSession()
    {
        var sess = AccountSession.Instance;
        if (sess == null) { SetName("Player"); ApplyFallback(); TryMarkLocalReady(); return; }

        subscribedSession = true;

        string name = !string.IsNullOrEmpty(sess.DisplayName) ? sess.DisplayName
                     : (allowEmailOrUsernameAsName ? sess.EmailOrUsername : null);
        SetName(string.IsNullOrEmpty(name) ? "Player" : name);

        if (sess.AvatarTexture != null) { ApplyTexture(sess.AvatarTexture); TryMarkLocalReady(); }
        else
        {
            ApplyFallback(); // temporary
            sess.EnsureAvatarDownloaded();
        }

        AccountSession.Instance.OnAvatarReady -= OnSessionAvatarReady;
        AccountSession.Instance.OnAvatarReady += OnSessionAvatarReady;
    }

    void OnSessionAvatarReady(Texture2D tex)
    {
        ApplyTexture(tex);
        TryMarkLocalReady();
    }

    void BindPhoton()
    {
        bound = ResolvePhoton();
        if (bound == null)
        {
            SetName("Player");
            ApplyFallback();
            TryMarkLocalReady(); // harmless if this banner is remote
            return;
        }
        ApplyFromPhoton(bound);
    }

    Player ResolvePhoton()
    {
        if (!PhotonNetwork.InRoom) return null;

        switch (source)
        {
            case SourceMode.PhotonLocal:
                return PhotonNetwork.LocalPlayer;
            case SourceMode.PhotonRemoteOther:
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p != PhotonNetwork.LocalPlayer) return p;
                return null;
            case SourceMode.PhotonByActorNumber:
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p.ActorNumber == actorNumber) return p;
                return null;
            default:
                return null;
        }
    }

    void ApplyFromPhoton(Player p)
    {
        // name
        string name = null;
        if (p.CustomProperties != null && p.CustomProperties.TryGetValue(PhotonProfileSync.KeyName, out var nObj))
            name = nObj as string;
        if (string.IsNullOrEmpty(name)) name = p.NickName;
        SetName(string.IsNullOrEmpty(name) ? "Player" : name);

        // avatar
        string url = null;
        if (p.CustomProperties != null && p.CustomProperties.TryGetValue(PhotonProfileSync.KeyAvatarUrl, out var aObj))
            url = aObj as string;

        if (!string.IsNullOrEmpty(url))
        {
            if (s_cache.TryGetValue(url, out var cached)) { ApplyTexture(cached); TryMarkLocalReadyIfBoundIsLocal(); return; }
            StartCoroutine(DownloadAndApply(url));
        }
        else
        {
            ApplyFallback();
            TryMarkLocalReadyIfBoundIsLocal();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) // <-- use alias
    {
        if (bound == null) return;
        if (targetPlayer != bound) return;

        if (changedProps.ContainsKey(PhotonProfileSync.KeyName) ||
            changedProps.ContainsKey(PhotonProfileSync.KeyAvatarUrl))
        {
            ApplyFromPhoton(bound);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (source == SourceMode.PhotonRemoteOther && bound == null) Bind();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (source == SourceMode.PhotonRemoteOther && bound == otherPlayer)
        {
            bound = null;
            SetName("Player");
            ApplyFallback();
        }
    }

    // ---------------- downloads & apply ----------------
    IEnumerator DownloadAndApply(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[PlayerProfileLoader] Avatar download failed: " + req.error);
                ApplyFallback();
                TryMarkLocalReadyIfBoundIsLocal();
                yield break;
            }
            var tex = DownloadHandlerTexture.GetContent(req);
            s_cache[url] = tex;
            ApplyTexture(tex);
            TryMarkLocalReadyIfBoundIsLocal();
        }
    }

    void SetName(string name)
    {
        if (displayNameTMP) displayNameTMP.text = name ?? "Player";
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
    }

    // ---------------- ready flag ----------------
    bool IsThisBannerForLocal()
    {
        if (source == SourceMode.PhotonLocal) return true;
        if (source == SourceMode.PhotonByActorNumber && PhotonNetwork.InRoom)
            return actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        if (source == SourceMode.LocalAccountSession) return true;
        return false;
    }

    void TryMarkLocalReadyIfBoundIsLocal()
    {
        if (!IsThisBannerForLocal()) return;
        TryMarkLocalReady();
    }

    void TryMarkLocalReady()
    {
        if (localReadyMarked) return;
        if (!markLocalUIReady) return;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;

        var props = new PhotonHashtable { [KeyUIReady] = true }; // <-- use alias
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        localReadyMarked = true;
    }
}
