public class ExpandNotificationBehaviour : UpdateButtonNotificationBehaviour
{
    protected override bool ShouldNotify()
    {
        return Notify();
    }

    public static bool Notify()
    {
        if(!ExpandBehaviour.IsExpandUnlocked())
        {
            return false;
        }

        // Notify when a new expansion can be bought
        foreach (LocationMetaData location in Locations.metaDatas)
        {
            if(!Database.instance.locationDatas.ContainsKey(location.ID) && Database.instance.GetMineralAmount(location.priceType) >= location.priceValue)
            {
                return true;
            }
        }

        return false;
    }
}