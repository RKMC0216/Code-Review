public class SpecialPackContent
{
    public Item item { get; private set; }
    public double value { get; private set; }

    public SpecialPackContent(Item item, double value)
    {
        this.item = item;
        this.value = value;
    }

    public void GrantItem()
    {
        GrantItem(item, value);
    }

    public static void GrantItem(Item item, double value)
    {
        switch (item)
        {
            case Item.ROCKS:
                Database.instance.activeLocation.AddRocks(value);
                break;
            case Item.SAPPHIRES:
                Database.instance.AddSapphires(value);
                break;
            case Item.EMERALDS:
                Database.instance.AddEmeralds(value);
                break;
            case Item.RUBIES:
                Database.instance.AddRubies(value);
                break;
            case Item.DIAMONDS:
                Database.instance.AddDiamonds(value);
                break;
            case Item.MULTIPLIER:
                Database.instance.activeLocation.AddMultiplier(value);
                break;
            case Item.TIME_WARP_24H:
            case Item.TIME_WARP_7D:
            case Item.TIME_WARP_14D:
            case Item.TIME_WARP_30D:
                Database.instance.AddTimeWarp((int)item, (int)value);
                break;
            default:
                break;
        }
    }

    public GrantedResource ConvertToGrantedResource()
    {
        Grant resource;

        switch(item)
        {
            case Item.ROCKS:
                resource = Grant.ROCKS;
                break;
            case Item.SAPPHIRES:
                resource = Grant.SAPPHIRES;
                break;
            case Item.EMERALDS:
                resource = Grant.EMERALDS;
                break;
            case Item.RUBIES:
                resource = Grant.RUBIES;
                break;
            case Item.DIAMONDS:
                resource = Grant.DIAMONDS;
                break;
            case Item.MULTIPLIER:
                resource = Grant.MULTIPLIER;
                break;
            case Item.TIME_WARP_24H:
                resource = Grant.TIME_WARP_24H;
                break;
            case Item.TIME_WARP_7D:
                resource = Grant.TIME_WARP_7D;
                break;
            case Item.TIME_WARP_14D:
                resource = Grant.TIME_WARP_14D;
                break;
            case Item.TIME_WARP_30D:
                resource = Grant.TIME_WARP_30D;
                break;
            default:
                return new GrantedResource(Grant.ROCKS, 0);
        }

        return new GrantedResource(resource, value);
    }
}