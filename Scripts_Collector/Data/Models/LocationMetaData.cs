using UnityEngine;
using System.IO;

public class LocationMetaData
{
    public int ID;
    public Translation_Inspector name;
    public Translation_Inspector description;
    public Mineral priceType;
    public double priceValue;
    public ILocation location;

    public LocationMetaData(int ID, Translation_Inspector name, Translation_Inspector description, Mineral priceType, double priceValue, ILocation location)
    {
        this.ID = ID;
        this.name = name;
        this.description = description;
        this.priceType = priceType;
        this.priceValue = priceValue;
        this.location = location;
    }

    public Sprite GetSpriteForLocation()
    {
        return Resources.Load<Sprite>(Path.Combine("Locations", FolderName()));
    }

    public string FolderName()
    {
        return Translations.translationsInspector[name]?[Language.EN];
    }
}