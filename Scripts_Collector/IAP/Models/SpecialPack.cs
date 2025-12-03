using System.Collections.Generic;
using System;

public class SpecialPack
{
    // 24 * 60 * 60 = 86400
    private const double HOURS_24 = 86400;
    // 48 * 60 * 60 = 172800
    private const double HOURS_48 = 172800;

    public const string STARTER_PACK = "starter_pack";
    public const string SMALL_TIME_WARPS_PACK = "small_time_warps_pack";
    public const string BIG_TIME_WARPS_PACK = "big_time_warps_pack";
    public const string SUPER_MULTIPLIER_PACK = "super_multiplier_pack";
    public const string JACK_OF_ALL_TRADES_PACK = "jack_of_all_trades_pack";
    public const string MEGA_PACK = "jack_of_all_trades_super_pack";

    public string ID { get; private set; }
    public Translation_Script name { get; private set; }
    public int discount { get; private set; }
    public DateTime creationTime { get; private set; }
    public List<SpecialPackContent> contents { get; private set; } = new List<SpecialPackContent>();

    private SpecialPack(string ID, Translation_Script name, int discount, DateTime creationTime, List<SpecialPackContent> contents)
    {
        this.ID = ID;
        this.name = name;
        this.discount = discount;
        this.creationTime = creationTime;
        this.contents = contents;
    }

    public void GrantPackContents()
    {
        foreach(SpecialPackContent content in contents)
        {
            content.GrantItem();
        }
    }

    public double OfferExpiresInSeconds()
    {
        return (OfferExpireDateTime() - TimeManager.instance.Time()).TotalSeconds;
    }

    public DateTime OfferExpireDateTime()
    {
        return creationTime.AddSeconds(ID.Equals(STARTER_PACK) ? HOURS_48 : HOURS_24);
    }

    public static SpecialPack GetSpecialPackForId(string productId)
    {
        return GetSpecialPackForId(productId, DateTime.UtcNow);
    }

    private static SpecialPack GetSpecialPackForId(string productId, DateTime creationTime)
    {
        switch(productId)
        {
            case STARTER_PACK:
                return new SpecialPack(productId, Translation_Script.STARTER_PACK, 80, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.TIME_WARP_24H, 3),       // 30
                    new SpecialPackContent(Item.MULTIPLIER, 7),         // 35
                    new SpecialPackContent(Item.RUBIES, 50)             // 50
                });                                                     // Total: 115. Price = $7.99
            case SMALL_TIME_WARPS_PACK:
                return new SpecialPack(productId, Translation_Script.TIME_WARPS_PACK, 40, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.TIME_WARP_24H, 10),      // 100
                    new SpecialPackContent(Item.TIME_WARP_7D, 4)        // 100
                });                                                     // Total: 200. Price = $14.99
            case BIG_TIME_WARPS_PACK:
                return new SpecialPack(productId, Translation_Script.LARGE_TIME_WARPS_PACK, 70, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.TIME_WARP_7D, 3),       // 75
                    new SpecialPackContent(Item.TIME_WARP_14D, 2),      // 80
                    new SpecialPackContent(Item.TIME_WARP_30D, 1)       // 65
                });                                                     // Total: 220. Price = $14.99
            case SUPER_MULTIPLIER_PACK:
                return new SpecialPack(productId, Translation_Script.SUPER_MULTIPLIER_PACK, 50, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.MULTIPLIER, 50),        // 200
                });                                                     // Total: 200. Price = $14.99
            case JACK_OF_ALL_TRADES_PACK:
                return new SpecialPack(productId, Translation_Script.JACK_OF_ALL_TRADES_PACK, 30, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.TIME_WARP_24H, 4),       // 40
                    new SpecialPackContent(Item.MULTIPLIER, 15),        // 70
                    new SpecialPackContent(Item.RUBIES, 70)             // 70
                });                                                     // Total: 180. Price = $14.99
            case MEGA_PACK:
                return new SpecialPack(productId, Translation_Script.MEGA_PACK, 75, creationTime, new List<SpecialPackContent>()
                {                                                       // Value
                    new SpecialPackContent(Item.TIME_WARP_24H, 8),       // 80
                    new SpecialPackContent(Item.MULTIPLIER, 40),        // 155
                    new SpecialPackContent(Item.RUBIES, 150)            // 150
                });                                                     // Total: 385. Price = $29.99
            default:
                return null;
        }
    }

    public static bool IsNoSpecialPackActive()
    {
        return Database.instance.currentSpecialPack == null || string.IsNullOrEmpty(Database.instance.currentSpecialPack.Item1) ||
            GetSpecialPackForId(Database.instance.currentSpecialPack.Item1, Database.instance.currentSpecialPack.Item2).OfferExpiresInSeconds() <= 0;
    }

    public static SpecialPack GetActiveSpecialPack()
    {
        if(IsNoSpecialPackActive())
        {
            GenerateNewSpecialPack();
        }

        return GetSpecialPackForId(Database.instance.currentSpecialPack.Item1, Database.instance.currentSpecialPack.Item2);
    }

    public static void GenerateNewSpecialPack()
    {
        if(Database.instance.currentSpecialPack == null || string.IsNullOrEmpty(Database.instance.currentSpecialPack.Item1))
        {
            StoreSpecialPack(STARTER_PACK);
        }
        else
        {
            StoreSpecialPack(GetNextSpecialPack());
        }
    }

    private static string GetNextSpecialPack()
    {
        switch (Database.instance.currentSpecialPack.Item1)
        {
            case STARTER_PACK:
                return SMALL_TIME_WARPS_PACK;

            case SMALL_TIME_WARPS_PACK:
                return SUPER_MULTIPLIER_PACK;

            case SUPER_MULTIPLIER_PACK:
                return JACK_OF_ALL_TRADES_PACK;

            case JACK_OF_ALL_TRADES_PACK:
                return BIG_TIME_WARPS_PACK;

            case BIG_TIME_WARPS_PACK:
                return MEGA_PACK;

            case MEGA_PACK:
                return SMALL_TIME_WARPS_PACK;

            default:
                return STARTER_PACK;
        }
    }

    private static void StoreSpecialPack(string ID)
    {
        Database.instance.currentSpecialPack = Tuple.Create(ID, TimeManager.instance.Time());
    }
}