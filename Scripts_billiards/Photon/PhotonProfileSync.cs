using System.Collections;                    // IEnumerator
using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable; // alias to avoid ambiguity

/// Publishes local DisplayName & AvatarUrl into Photon Player Custom Props:
///   pf_name, pf_avatarUrl (and also pf_avatar_url for backward-compat).
/// Mirrors name to PhotonNetwork.NickName (only when safe).
/// Skips sending while Leaving/Disconnecting or when ExitingToLogin is true.
[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public class PhotonProfileSync : MonoBehaviourPunCallbacks
{
    public const string KeyName = "pf_name";
    public const string KeyAvatarUrl = "pf_avatarUrl";   // camelCase (existing)
    public const string KeyAvatarUrlAlt = "pf_avatar_url";  // snake_case (compat)

    [Header("Options")]
    [Tooltip("Also mirror the name into PhotonNetwork.NickName when safe.")]
    public bool setPhotonNickName = true;

    [Tooltip("Verbose logs for troubleshooting.")]
    public bool debugLogs = false;

    private string _name = "Player";
    private string _avatarUrl = "";
    private Coroutine _burstCo;
    private bool _leavingOrDisabled; // stop further publishing once we’re leaving / disabled

    public override void OnEnable()
    {
        _leavingOrDisabled = false;

        BuildFromAccountSession(out _name, out _avatarUrl);

        if (AccountSession.Instance != null)
        {
            AccountSession.Instance.OnAutoLoginSucceeded -= OnAccountReady;
            AccountSession.Instance.OnAutoLoginSucceeded += OnAccountReady;

            AccountSession.Instance.OnAvatarReady -= OnAvatarReady;
            AccountSession.Instance.OnAvatarReady += OnAvatarReady;
        }

        if (IsJoinedRoom())
        {
            TryApplyNickName();
            PublishNowIfInRoom();
            StartBurst();
        }
        else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
        {
            TryApplyNickName();
        }
    }

    public override void OnDisable()
    {
        _leavingOrDisabled = true;
        StopBurst();

        if (AccountSession.Instance != null)
        {
            AccountSession.Instance.OnAutoLoginSucceeded -= OnAccountReady;
            AccountSession.Instance.OnAvatarReady -= OnAvatarReady;
        }
    }

    private void OnAccountReady()
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;

        BuildFromAccountSession(out _name, out _avatarUrl);
        TryApplyNickName();
        PublishNowIfInRoom();
    }

    private void OnAvatarReady(Texture2D _)
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;

        BuildFromAccountSession(out _name, out _avatarUrl);
        PublishNowIfInRoom();
    }

    public override void OnConnectedToMaster()
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;
        TryApplyNickName();
    }

    public override void OnJoinedRoom()
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;

        TryApplyNickName();
        PublishNowIfInRoom();
        StartBurst();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;
        PublishNowIfInRoom(); // help late joiners
    }

    public override void OnLeftRoom()
    {
        _leavingOrDisabled = true;
        StopBurst();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _leavingOrDisabled = true;
        StopBurst();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        if (changedProps == null || changedProps.Count == 0) return;

        if (debugLogs)
        {
            var n = changedProps.ContainsKey(KeyName) ? changedProps[KeyName] as string : "(no change)";
            var u = changedProps.ContainsKey(KeyAvatarUrl) ? changedProps[KeyAvatarUrl] as string :
                    changedProps.ContainsKey(KeyAvatarUrlAlt) ? changedProps[KeyAvatarUrlAlt] as string : "(no change)";
            Debug.Log($"[ProfileSync] Remote update actor {targetPlayer.ActorNumber} name='{n}' url='{u}'");
        }
    }

    // ------------- reliability burst -------------
    private void StartBurst()
    {
        StopBurst();
        _burstCo = StartCoroutine(RepublishBurst());
    }

    private void StopBurst()
    {
        if (_burstCo != null) StopCoroutine(_burstCo);
        _burstCo = null;
    }

    private IEnumerator RepublishBurst()
    {
        yield return null;
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) yield break; PublishNowIfInRoom();

        yield return new WaitForSeconds(0.25f);
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) yield break; PublishNowIfInRoom();

        yield return new WaitForSeconds(0.5f);
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) yield break; PublishNowIfInRoom();
    }

    // ------------- internals -------------
    private void BuildFromAccountSession(out string name, out string avatarUrl)
    {
        name = "Player";
        avatarUrl = "";

        var s = AccountSession.Instance;
        if (s != null)
        {
            if (!string.IsNullOrEmpty(s.DisplayName)) name = s.DisplayName;
            else if (!string.IsNullOrEmpty(s.EmailOrUsername)) name = s.EmailOrUsername;

            if (!string.IsNullOrEmpty(s.AvatarUrl)) avatarUrl = s.AvatarUrl;
        }
    }

    private void TryApplyNickName()
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin || !setPhotonNickName || string.IsNullOrEmpty(_name)) return;

        var state = PhotonNetwork.NetworkClientState;
        bool safe =
            state == ClientState.ConnectedToMasterServer ||
            state == ClientState.Joined;

        if (!safe) return;

        if (PhotonNetwork.NickName != _name)
        {
            PhotonNetwork.NickName = _name;
            if (debugLogs) Debug.Log($"[ProfileSync] NickName='{_name}' at {state}");
        }
    }

    private void PublishNowIfInRoom()
    {
        if (_leavingOrDisabled || MatchContext.ExitingToLogin) return;
        if (!CanSendProps()) return;

        var props = new PhotonHashtable
        {
            [KeyName] = _name ?? "Player",
            [KeyAvatarUrl] = _avatarUrl ?? "",
            [KeyAvatarUrlAlt] = _avatarUrl ?? ""
        };

        bool changed = false;

        if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(KeyName, out var nObj) ||
            (nObj as string) != (string)props[KeyName]) changed = true;

        string currentAvatar = null;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(KeyAvatarUrl, out var a1))
            currentAvatar = a1 as string;
        else if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(KeyAvatarUrlAlt, out var a2))
            currentAvatar = a2 as string;

        if (currentAvatar != (string)props[KeyAvatarUrl]) changed = true;

        if (changed)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            if (debugLogs) Debug.Log($"[ProfileSync] Published: name='{props[KeyName]}', avatar='{props[KeyAvatarUrl]}'");
        }
        else if (debugLogs)
        {
            Debug.Log("[ProfileSync] No publish (unchanged).");
        }
    }

    private bool CanSendProps()
    {
        if (!PhotonNetwork.IsConnected) return false;
        if (!PhotonNetwork.InRoom) return false;
        return PhotonNetwork.NetworkClientState == ClientState.Joined && !MatchContext.ExitingToLogin;
    }

    private bool IsJoinedRoom()
    {
        return PhotonNetwork.IsConnected &&
               PhotonNetwork.InRoom &&
               PhotonNetwork.NetworkClientState == ClientState.Joined;
    }

    // ------------- public helpers -------------
    public void ForceRepublish()
    {
        BuildFromAccountSession(out _name, out _avatarUrl);
        TryApplyNickName();
        PublishNowIfInRoom();
        StartBurst();
    }

    [PunRPC]
    private void RPC_RequestProfilePublish()
    {
        ForceRepublish();
    }
}
