using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace GiftTasteHelper.Framework
{
    public static class Enumerable
    {
        // From https://stackoverflow.com/a/489421
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }

    internal class Utils
    {
        /*********
        ** Properties
        *********/
        private static IMonitor MonitorRef;


        /*********
        ** Public methods
        *********/
        public static void InitLog(IMonitor monitor)
        {
            Utils.MonitorRef = monitor;
        }

        public static void DebugLog(string message, LogLevel level = LogLevel.Trace)
        {
#if WITH_LOGGING
            Debug.Assert(Utils.MonitorRef != null, "Monitor ref is not set.");
            Utils.MonitorRef.Log(message, level);
#else
            // don't spam other developer consoles
            if (level > LogLevel.Debug)
            {
                Debug.Assert(MonitorRef != null, "Monitor ref is not set.");
                MonitorRef.Log(message, level);
            }
#endif
        }

        public static bool Ensure(bool condition, string message)
        {
#if DEBUG
            if (!condition)
            {
                DebugLog($"Failed Ensure: {message}");
            }
#endif
            return !!condition;
        }

        public static int Clamp(int val, int min, int max)
        {
            return Math.Max(Math.Min(val, max), min);
        }

        // TODO: handle more cases
        public static string ParseNameFromHoverText(string text)
        {
            string name = "";
            string[] parts = text.Split('\'', ' ');
            if (parts.Length > 0)
            {
                name = parts[0];
            }
            return name;
        }

        public static Rectangle MakeRect(float x, float y, float width, float height)
        {
            return new Rectangle((int)x, (int)y, (int)width, (int)height);
        }

        public static SVector2 CreateMax(SVector2 a, SVector2 b)
        {
            return new SVector2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        // TODO: Move all the gift stuff elsewhere
        public static readonly Dictionary<string, GiftTaste> UniversalTastes = new Dictionary<string, GiftTaste>
        {
            ["Universal_Love"]      = GiftTaste.Love,
            ["Universal_Loved"]     = GiftTaste.Love,
            ["Universal_Like"]      = GiftTaste.Like,
            ["Universal_Liked"]     = GiftTaste.Like,
            ["Universal_Neutral"]   = GiftTaste.Neutral,
            ["Universal_Dislike"]   = GiftTaste.Dislike,
            ["Universal_Disliked"]  = GiftTaste.Dislike,
            ["Universal_Hate"]      = GiftTaste.Hate,
            ["Universal_Hated"]     = GiftTaste.Hate
        };

        public static readonly Dictionary<GiftTaste, string> UniversalTasteNames = new Dictionary<GiftTaste, string>
        {
            [GiftTaste.Love]        = "Universal_Love",
            [GiftTaste.Like]        = "Universal_Like",
            [GiftTaste.Neutral]     = "Universal_Neutral",
            [GiftTaste.Dislike]     = "Universal_Dislike",
            [GiftTaste.Hate]        = "Universal_Hate"
        };

        public static string[] GetItemsForTaste(string npcName, GiftTaste taste)
        {
            Debug.Assert(taste != GiftTaste.MAX);
            if (npcName == null || !Game1.NPCGiftTastes.TryGetValue(npcName, out var giftTaste))
            {
                return Array.Empty<string>();
            }

            if (UniversalTastes.ContainsKey(npcName))
            {
                // Universal tastes are parsed differently
                return giftTaste.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            // See http://stardewvalleywiki.com/Modding:Gift_taste_data
            int tasteIndex = (int)taste + 1; // Enum value is the even number which is the dialogue, odd is the list of item refs.
            string[] giftTastes = giftTaste.Split('/');
            if (giftTastes.Length > tasteIndex && giftTastes[tasteIndex].Length > 0)
            {
                return giftTastes[tasteIndex].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return Array.Empty<string>();
        }

        // See http://stardewvalleywiki.com/Modding:Gift_taste_data
        public static GiftTaste GetTasteForGift(string npcName, string itemId)
        {
            if (npcName is null || !Game1.NPCGiftTastes.TryGetValue(npcName, out var giftTastesRaw))
            {
                return GiftTaste.MAX;
            }

            if (itemId is null || !Game1.objectData.ContainsKey(itemId))
            {
                // Item is likely a category
                return GiftTaste.MAX;
            }

            GiftTaste taste = GiftTaste.Neutral;

            string[] giftTastes = giftTastesRaw.Split('/');
            Debug.Assert(giftTastes.Length > 0);
            if (giftTastes.Length == 0)
            {
                return taste;
            }

            var itemData = ItemData.MakeItem(itemId);

            // Part I: universal taste by category
            GiftTaste UniversalTasteForCategory(string cat)
            {
                foreach (var (Key, Value) in UniversalTastes)
                {
                    if (GetItemsForTaste(Key, Value).Contains(cat))
                    {
                        return Value;
                    }
                }
                return GiftTaste.Neutral;
            }

            if (itemData.Category.Valid)
            {
                taste = UniversalTasteForCategory(itemData.Category.ID);
            }

            // Part II: universal taste by item ID
            GiftTaste GetUniversalTaste(string id)
            {
                foreach (var (Key, Value) in UniversalTastes)
                {
                    if (GetItemsForTaste(Key, Value).Contains(id))
                    {
                        return Value;
                    }
                }
                return GiftTaste.MAX;
            }

            var universalTaste = GetUniversalTaste(itemData.ID);
            bool hasUniversalId = universalTaste != GiftTaste.MAX;
            bool hasUniversalNeutralId = universalTaste == GiftTaste.Neutral;
            taste = universalTaste != GiftTaste.MAX ? universalTaste : taste;

            // Part III: override neutral if it's from universal category
            if (taste == GiftTaste.Neutral && !hasUniversalNeutralId)
            {
                if (itemData.Edible && itemData.TastesBad)
                {
                    taste = GiftTaste.Hate;
                }
                else if (itemData.Price < 20)
                {
                    taste = GiftTaste.Dislike;
                }
                else if (itemData.Category.Name == "Arch")
                {
                    taste = npcName == "Penny" ? GiftTaste.Like : GiftTaste.Dislike;
                }
            }

            // part IV: sometimes override with personal tastes
            var personalMetadataKeys = new Dictionary<int, GiftTaste>
            {
                // metadata is paired: odd values contain a list of item references, even values contain the reaction dialogue
                [1] = GiftTaste.Love,
                [7] = GiftTaste.Hate, // Hate has precedence
                [3] = GiftTaste.Like,
                [5] = GiftTaste.Dislike,
                [9] = GiftTaste.Neutral
            };

            foreach (var (tasteIndex, Value) in personalMetadataKeys)
            {
                if (tasteIndex < giftTastes.Length && giftTastes[tasteIndex].Length > 0)
                {
                    var items = giftTastes[tasteIndex].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool hasTasteForItemOrCategory = items.Contains(itemData.ID) || (itemData.Category.Valid && items.Contains(itemData.Category.ID));
                    bool noCategoryOrNoTasteForCategory = !itemData.Category.Valid || !items.Contains(itemData.Category.ID);
                    if (hasTasteForItemOrCategory && (noCategoryOrNoTasteForCategory || !hasUniversalId))
                    {
                        return Value;
                    }
                }
            }
            return taste;
        }
    }
}
