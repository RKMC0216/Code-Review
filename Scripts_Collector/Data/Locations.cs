using System.Collections.Generic;

public static class Locations
{
    public const int EARTH = 1;
    public const int MOON = 2;
    public const int MARS = 3;

    public static List<LocationMetaData> metaDatas { get; private set; } = new List<LocationMetaData>()
    {
        new LocationMetaData(EARTH, Translation_Inspector.LOCATION_EARTH_NAME, Translation_Inspector.LOCATION_EARTH_DESCRIPTION, Mineral.ROCK, 0, new Earth()),
        new LocationMetaData(MOON, Translation_Inspector.LOCATION_MOON_NAME, Translation_Inspector.LOCATION_MOON_DESCRIPTION, Mineral.ROCK, 100E+12, new Moon()),
        new LocationMetaData(MARS, Translation_Inspector.LOCATION_MARS_NAME, Translation_Inspector.LOCATION_MARS_DESCRIPTION, Mineral.RUBY, 50, new Mars()),
    };

    public static Location CreateLocationForId(int ID, LocationData data)
    {
        // Reset these to make sure the IDs are correct
        Collector.GENERATED_ID = 1;
        Milestone.GENERATED_ID = 1;
        NormalUpgrade.GENERATED_ID = 1;
        PrestigeUpgrade.GENERATED_ID = 1;

        return GetMetaDataForLocation(ID)?.location.CreateLocation(data);
    }

    public static LocationMetaData GetMetaDataForLocation(int ID)
    {
        foreach(LocationMetaData loc in metaDatas)
        {
            if(loc.ID == ID)
            {
                return loc;
            }
        }

        return null;
    }
}