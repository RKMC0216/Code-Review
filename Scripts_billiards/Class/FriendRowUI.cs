using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRowUI : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI statusText;
    public Button inviteButton; // assign in prefab

    public void Set(string username, string status, Action onInvite = null)
    {
        if (usernameText) usernameText.text = username;
        if (statusText) statusText.text = status;

        if (inviteButton)
        {
            inviteButton.onClick.RemoveAllListeners();
            inviteButton.onClick.AddListener(() => onInvite?.Invoke());
            inviteButton.interactable = onInvite != null;
        }
    }
}
