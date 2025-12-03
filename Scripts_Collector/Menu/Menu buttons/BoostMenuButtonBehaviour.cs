using System.Collections;
using UnityEngine;
using TMPro;

public class BoostMenuButtonBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text countdownTxt;

    private LocationData data;

    private void Start()
    {
        data = Database.instance.activeLocation.data;

        if(data.adBoostTime > 0)
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

        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1);

        while(data.adBoostTime > 0)
        {
            countdownTxt.text = TimeManager.FormatSeconds(data.adBoostTime);
            yield return wait;
            data.adBoostTime--;
        }

        data.adBoostTime = 0;
        countdownTxt.text = "00:00:00";
        countdownTxt.gameObject.SetActive(false);
        transform.root.GetComponent<Game>().UpdateIncomes();

        // This will tell all the observer button notifications to update
        ObserverButtonNotificationBehaviour.ValueChanged();
    }
}