using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class ForfeitExitHandler_PUN : MonoBehaviourPunCallbacks
{
    [Header("Scenes")]
    [Tooltip("Login scene name to load after forfeit.")]
    public string loginSceneName = "Login";

    [Header("Optional Win/Loss UI")]
    public GameObject resultPanel;          // winner/loser panel in game scene
    public TextMeshProUGUI resultText;      // "You win" / "Opponent forfeited" / etc.

    [Header("Winner Auto Exit")]
    [Tooltip("If > 0, winner will also return to login after delay.")]
    public float autoExitDelayForWinner = 0f;

    // Hook this up to your Forfeit button
    public void OnClick_Forfeit()
    {
        if (!PhotonNetwork.IsConnected) { ExitToLoginImmediate(); return; }

        // Announce forfeiter to everyone
        photonView.RPC(nameof(RPC_OnForfeit), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RPC_OnForfeit(int forfeiterActor)
    {
        bool iAmForfeiter = (forfeiterActor == PhotonNetwork.LocalPlayer.ActorNumber);

        // Optional result UI
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText)
        {
            if (iAmForfeiter)
                resultText.text = "You forfeited the match.";
            else
                resultText.text = "Opponent forfeited — you win!";
        }

        if (iAmForfeiter)
        {
            StartCoroutine(ExitToLoginAfterForfeit());
        }
        else if (autoExitDelayForWinner > 0f)
        {
            StartCoroutine(ExitWinnerAfterDelay(autoExitDelayForWinner));
        }
    }

    private IEnumerator ExitWinnerAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        StartCoroutine(ExitToLoginAfterForfeit());
    }

    private IEnumerator ExitToLoginAfterForfeit()
    {
        // Strong exit sequence to avoid being pulled back by scene sync or lingering matchmaker
        MatchContext.ExitingToLogin = true;

        // Stop scene sync & incoming messages immediately
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.IsMessageQueueRunning = false;

        // Leave the room first (prevents master scene loads from affecting us)
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom(false);
            float t = 6f;
            while (PhotonNetwork.InRoom && t > 0f) { t -= Time.unscaledDeltaTime; yield return null; }
        }

        // Disconnect to guarantee no more room events are processed
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            float td = 6f;
            while (PhotonNetwork.IsConnected && td > 0f) { td -= Time.unscaledDeltaTime; yield return null; }
        }

        // Kill any lingering matchmaker that might load the game scene
        var mm = FindFirstObjectByType<PhotonMatchmaker>();
        if (mm) Destroy(mm.gameObject);

        // Keep message queue disabled until login scene is up.
        // Your login scene code can enable it when ready.

        // Load login scene using your SceneLoader helper
        SceneLoader.EnsureAndGoBack(loginSceneName);
    }

    private void ExitToLoginImmediate()
    {
        MatchContext.ExitingToLogin = true;
        SceneLoader.EnsureAndGoBack(loginSceneName);
    }

    // If someone leaves, we do nothing special here.
    public override void OnPlayerLeftRoom(Player otherPlayer) { }
}
