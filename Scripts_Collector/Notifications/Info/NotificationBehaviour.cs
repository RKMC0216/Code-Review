using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NotificationBehaviour : MonoBehaviour
{
    [SerializeField]
    private Image img;

    [SerializeField]
    private TMP_Text txt;

    public NotificationInfo info;

    private void Start()
    {
        txt.text = info.text;
        img.sprite = info.sprite;

        if(info.maxFontSize > 0)
        {
            txt.fontSizeMax = info.maxFontSize;
        }

        // Play sound
        AudioManager.instance.Play(info.notificationSound, info.soundDelay);
    }
}