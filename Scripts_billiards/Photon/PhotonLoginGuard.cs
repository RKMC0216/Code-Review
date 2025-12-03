using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

/// Prevents auto-resync / auto-join pulling you back into the game.
/// Also kills any lingering matchmaker objects and clears MatchContext hints.
public class PhotonLoginGuard : MonoBehaviour
{
    [Tooltip("Also disconnect from Photon when returning from a match (optional).")]
    public bool disconnectOnReturnFromMatch = false;

    [Tooltip("Max seconds to wait if we need to leave a room right now.")]
    public float leaveTimeout = 5f;

    void Awake()
    {
#if PHOTON_UNITY_NETWORKING
        PhotonNetwork.AutomaticallySyncScene = false;

        // If some object dragged us here while still in a room, leave immediately.
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom(false);

        StartCoroutine(WaitNotInRoom());

        // Nuke any lingering matchmaker that might auto-join again
        KillMatchmakerIfAny();

        // Clear any “resume” hints
        SafeClearMatchContext();

        if (SceneLoader.ReturningToLogin && disconnectOnReturnFromMatch && PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        SceneLoader.ReturningToLogin = false;
#endif
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

#if PHOTON_UNITY_NETWORKING
    System.Collections.IEnumerator WaitNotInRoom()
    {
        float t = leaveTimeout;
        while (PhotonNetwork.InRoom && t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
    }
#endif

    static void KillMatchmakerIfAny()
    {
        var mms = Resources.FindObjectsOfTypeAll<Component>();
        foreach (var c in mms)
        {
            if (c == null || c.gameObject == null) continue;
            if (!c.gameObject.scene.IsValid()) continue;
            if (c.GetType().Name == "PhotonMatchmaker")
            {
                Object.Destroy(c.gameObject);
            }
        }
    }

    static void SafeClearMatchContext()
    {
        var t = System.Type.GetType("MatchContext");
        if (t == null) return;
        var roomField = t.GetField("RoomCode");
        var hostField = t.GetField("IsHost");
        var nickField = t.GetField("NickName");
        if (roomField != null) roomField.SetValue(null, null);
        if (hostField != null) hostField.SetValue(null, false);
        if (nickField != null) nickField.SetValue(null, null);
    }
}
