using System.Collections;
using UnityEngine;

public abstract class WaitForSecondsTillButtonNotificationBehaviour : ButtonNotificationBehaviour
{
    private bool readyToNotify = false;

    // Return a number lower than 0 if you dont want to notify at all
    protected abstract float NotifyInSeconds();

    private void OnEnable()
    {
        float notifyDelay = NotifyInSeconds();

        if (notifyDelay >= 0)
        {
            StartCoroutine(WaitForSecondsTillNotify(notifyDelay));
        }
    }

    protected override bool ShouldNotify()
    {
        return readyToNotify;
    }

    private IEnumerator WaitForSecondsTillNotify(float seconds)
    {
        yield return null;

        yield return new WaitForSeconds(seconds);
        readyToNotify = true;

        UpdateNotification();
    }
}