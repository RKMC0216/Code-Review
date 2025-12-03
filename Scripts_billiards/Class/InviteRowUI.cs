using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InviteRowUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;   // "Alice invited you"
    public TextMeshProUGUI roomText;    // "ROOM-123456"
    public Button acceptButton;
    public Button declineButton;

    public void Set(string title, string room, Action onAccept, Action onDecline)
    {
        if (titleText) titleText.text = title;
        if (roomText) roomText.text = room;

        if (acceptButton)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() => onAccept?.Invoke());
        }
        if (declineButton)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() => onDecline?.Invoke());
        }
    }
}
