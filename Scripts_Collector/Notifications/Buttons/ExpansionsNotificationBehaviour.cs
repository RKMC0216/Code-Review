using System.Collections.Generic;

public class ExpansionsNotificationBehaviour : WaitForSecondsTillButtonNotificationBehaviour
{
    protected override float NotifyInSeconds()
    {
        float firstEvent = -1;

        foreach(KeyValuePair<int, LocationData> data in Database.instance.locationDatas)
        {
            if(data.Key != Database.instance.activeLocation.metaData.ID)
            {
                // Calc time till boost runs out
                float adboostTimeLeft = data.Value.adBoostTime - (float)(TimeManager.instance.Time() - data.Value.lastOnlineTime).TotalSeconds;

                if (adboostTimeLeft <= 0)
                {
                    // Ran out of ad boost, notify immediately
                    return 0;
                }
                else
                {
                    // Did not run out of ad boost yet, see if this would be the first event
                    if (firstEvent == -1 || adboostTimeLeft < firstEvent)
                    {
                        firstEvent = adboostTimeLeft;
                    }
                }

                if(ExpansionBehaviour.CalcSpinsAvailable(data.Value.spinsViewedToday) == 0)
                {
                    // Time till new spins become available
                    float timeTillNewSpins = (float)(data.Value.spinsViewedToday.Item1.AddHours(SpinBehaviour.RESET_FREE_PER_HOURS) - TimeManager.instance.Time()).TotalSeconds;

                    // Used all spins like a good boy, see if getting new spins would be the first event
                    if (firstEvent == -1 || timeTillNewSpins < firstEvent)
                    {
                        firstEvent = timeTillNewSpins;
                    }
                }
                else
                {
                    // Spins till available, notify immediately
                    return 0;
                }
            }
        }

        // Return -1 by default to not show notification at all
        return firstEvent;
    }
}