using System.Collections;
using UnityEngine;

public abstract class UpdateButtonNotificationBehaviour : ButtonNotificationBehaviour
{
    protected float interval = 1;

    private void OnEnable()
    {
        StartCoroutine(KeepUpdating());
    }

    private IEnumerator KeepUpdating()
    {
        yield return null;
        InitNotification();

        WaitForSeconds wait = interval == 1 ? DelayWait.oneSecond : new WaitForSeconds(interval);
        
        while (true)
        {
            yield return wait;
            UpdateNotification();
        }
    }
}