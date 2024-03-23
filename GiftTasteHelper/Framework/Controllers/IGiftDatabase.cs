namespace GiftTasteHelper.Framework
{
    delegate void DataSourceChangedDelegate();

    internal interface IGiftDatabase
    {
        event DataSourceChangedDelegate DatabaseChanged;

        bool AddGift(string npcName, string itemId, GiftTaste taste);
        bool AddGifts(string npcName, GiftTaste taste, string[] itemIds);

        string[] GetGiftsForTaste(string npcName, GiftTaste taste);
    }
}
