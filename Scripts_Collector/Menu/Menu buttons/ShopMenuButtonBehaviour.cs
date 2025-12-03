using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class ShopMenuButtonBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text countdownTxt;

    private Game game;

    void Start()
    {
        game = transform.root.GetComponent<Game>();
        StartCoroutine(Countdown());
    }

    public void ResetCountdown()
    {
        StopAllCoroutines();
        StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        while(true)
        {
            // Wait for the pop-up to finish showing before we start updating the countdown
            yield return new WaitUntil(() => !game.showSpecialPackOfferQueued);

            SpecialPack pack = SpecialPack.GetActiveSpecialPack();
            DateTime expireTime = pack.OfferExpireDateTime();

            while (expireTime >= TimeManager.instance.Time())
            {
                countdownTxt.text = TimeManager.FormatTimeSpan(expireTime - TimeManager.instance.Time());
                yield return DelayWait.oneSecond;
            }

            // Pack expired, show a pop-up with the new offer, this will also generate a new special pack
            game.ShowSpecialPackOffer();
        }
    }
}