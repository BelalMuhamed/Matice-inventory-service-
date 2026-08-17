namespace INI.AUB.Inventory.System.API.Enums
{
    [Flags]
    public enum PrintedItems:byte
    {
        CardHolderName=1,
        Cvv=2,
        ExpireDate=3,
        PAN=4
    }
}
