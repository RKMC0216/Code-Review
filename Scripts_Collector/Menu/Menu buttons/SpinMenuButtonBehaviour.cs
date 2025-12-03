using System.Collections;
using UnityEngine;
using TMPro;
using System;

public class SpinMenuButtonBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text countdownTxt;

    private void Start()
    {
        if (!SpinBehaviour.IsFreeSpinAvailable())
        {
            StartCountdown();
        }
    }

    public void StartCountdown()
    {
        StopAllCoroutines();
        StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        countdownTxt.gameObject.SetActive(true);

        DateTime goal = Database.instance.activeLocation.data.spinsViewedToday.Item1.AddHours(SpinBehaviour.RESET_FREE_PER_HOURS);
        TimeSpan timeLeft =  goal - TimeManager.instance.Time();

        while (timeLeft.TotalSeconds > 0)
        {
            countdownTxt.text = TimeManager.FormatTimeSpan(timeLeft);
            yield return DelayWait.oneSecond;

            timeLeft = goal - TimeManager.instance.Time();
        }

        countdownTxt.text = "00:00:00";
        countdownTxt.gameObject.SetActive(false);

        // This will tell all the observer button notifications to update
        ObserverButtonNotificationBehaviour.ValueChanged();
    }
}
