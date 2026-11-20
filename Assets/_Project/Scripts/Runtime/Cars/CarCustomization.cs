using System;
using System.Collections.Generic;
using UnityEngine;
using UltimateCarParking.Save;

namespace UltimateCarParking.Cars
{
    public enum CustomSlot { Paint, Finish, Rims, Tint, Headlights, Plate }

    /// <summary>One car's chosen look. -1 / 0 = factory. Stored in the save (version 6).</summary>
    [Serializable]
    public sealed class CarLook
    {
        public string carId = "";
        public int paint = -1;       // index in CustomizationCatalog.Paints, -1 factory
        public int finish;           // 0 factory, 1 gloss, 2 metallic, 3 matte
        public int rims = -1;        // index in Rims, -1 factory
        public int tint;             // 0 none, 1 light, 2 dark, 3 limo
        public int headlights;       // 0 factory (warm white), 1 cool white, 2 xenon blue-white, 3 yellow
        public string plate = "";    // "" = default plate text
        /// <summary>Options bought for this car ("Paint:3", "Finish:2", ...). Factory options are always free.</summary>
        public List<string> owned = new List<string>();

        public bool IsFactory => paint < 0 && finish == 0 && rims < 0 && tint == 0 && headlights == 0 && string.IsNullOrEmpty(plate);
        public CarLook Clone() { var c = (CarLook)MemberwiseClone(); c.owned = new List<string>(owned); return c; }
    }

    /// <summary>The options on offer and their prices (coins). Kept small next to car prices (950 – 55,000).</summary>
    public static class CustomizationCatalog
    {
        public static readonly (string name, Color color)[] Paints =
        {
            ("ARCTIC WHITE", new Color(0.93f, 0.94f, 0.95f)), ("GRAPHITE", new Color(0.22f, 0.23f, 0.25f)), ("MIDNIGHT BLACK", new Color(0.05f, 0.05f, 0.06f)),
            ("SILVER", new Color(0.66f, 0.68f, 0.70f)), ("RACING RED", new Color(0.72f, 0.06f, 0.07f)), ("SUNSET ORANGE", new Color(0.93f, 0.40f, 0.08f)),
            ("SOLAR YELLOW", new Color(0.95f, 0.78f, 0.12f)), ("LIME", new Color(0.55f, 0.82f, 0.12f)), ("RACING GREEN", new Color(0.05f, 0.28f, 0.16f)),
            ("OCEAN BLUE", new Color(0.08f, 0.32f, 0.70f)), ("NAVY", new Color(0.06f, 0.10f, 0.26f)), ("VIOLET", new Color(0.36f, 0.16f, 0.55f)),
        };
        public static readonly string[] Finishes = { "FACTORY", "GLOSS", "METALLIC", "MATTE" };
        public static readonly (float metallic, float smoothness)[] FinishValues = { (0f, 0f), (0.1f, 0.85f), (0.85f, 0.75f), (0.05f, 0.12f) };
        public static readonly (string name, Color tint, float metallic)[] Rims =
        {
            ("CHROME", new Color(1.15f, 1.15f, 1.18f), 0.95f), ("GLOSS BLACK", new Color(0.18f, 0.18f, 0.19f), 0.4f),
            ("GUNMETAL", new Color(0.45f, 0.47f, 0.50f), 0.8f), ("GOLD", new Color(1.0f, 0.78f, 0.32f), 0.9f), ("BRONZE", new Color(0.72f, 0.48f, 0.26f), 0.85f),
        };
        public static readonly (string name, float darkness)[] Tints = { ("NONE", 0f), ("LIGHT", 0.35f), ("DARK", 0.6f), ("LIMO", 0.82f) };
        public static readonly (string name, Color color)[] Headlights =
        {
            ("WARM WHITE", new Color(1f, 0.92f, 0.78f)), ("COOL WHITE", new Color(0.92f, 0.96f, 1f)), ("XENON", new Color(0.78f, 0.88f, 1f)), ("SELECTIVE YELLOW", new Color(1f, 0.85f, 0.45f)),
        };

        public const int PaintPrice = 400, GlossPrice = 250, MetallicPrice = 600, MattePrice = 700, RimPrice = 500, TintPrice = 300, HeadlightPrice = 250, PlatePrice = 150;

        public static int Price(CustomSlot slot, int value) => slot switch
        {
            CustomSlot.Paint => value < 0 ? 0 : PaintPrice,
            CustomSlot.Finish => value == 1 ? GlossPrice : value == 2 ? MetallicPrice : value == 3 ? MattePrice : 0,
            CustomSlot.Rims => value < 0 ? 0 : RimPrice,
            CustomSlot.Tint => value <= 0 ? 0 : TintPrice,
            CustomSlot.Headlights => value <= 0 ? 0 : HeadlightPrice,
            CustomSlot.Plate => PlatePrice,
            _ => 0,
        };

        public static string Key(CustomSlot slot, int value) => $"{slot}:{value}";

        /// <summary>Plate text rule: 2–8 characters, A–Z, 0–9, space and dash, upper case.</summary>
        public static string CleanPlate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.ToUpperInvariant())
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == ' ' || ch == '-') { sb.Append(ch); if (sb.Length == 8) break; }
            string t = sb.ToString().Trim();
            return t.Length >= 2 ? t : "";
        }
    }

    /// <summary>
    /// Buying and applying looks. Options are bought once per car with coins (never the car or the balance itself is
    /// touched otherwise); switching between options already bought, and back to factory, is free.
    /// </summary>
    public static class CustomizationService
    {
        public static CarLook Look(SaveData save, string carId)
        {
            save.carLooks ??= new List<CarLook>();
            foreach (CarLook l in save.carLooks) if (l != null && l.carId == carId) return l;
            return new CarLook { carId = carId };
        }

        private static CarLook GetOrAdd(SaveData save, string carId)
        {
            save.carLooks ??= new List<CarLook>();
            foreach (CarLook l in save.carLooks) if (l != null && l.carId == carId) return l;
            var n = new CarLook { carId = carId };
            save.carLooks.Add(n);
            return n;
        }

        public static bool Owns(SaveData save, string carId, CustomSlot slot, int value) =>
            CustomizationCatalog.Price(slot, value) == 0 || (slot != CustomSlot.Plate && Look(save, carId).owned.Contains(CustomizationCatalog.Key(slot, value)));

        /// <summary>Cost to apply this option now (0 when owned or free).</summary>
        public static int CostToApply(SaveData save, string carId, CustomSlot slot, int value) =>
            Owns(save, carId, slot, value) ? 0 : CustomizationCatalog.Price(slot, value);

        /// <summary>Applies an option, paying for it if needed. False (nothing changes) when the car is not owned or coins are short.</summary>
        public static bool TryApply(SaveData save, CarData car, CustomSlot slot, int value, string plateText = null)
        {
            if (save == null || car == null || !GarageService.IsOwned(save, car)) return false;
            if (slot == CustomSlot.Plate)
            {
                string clean = CustomizationCatalog.CleanPlate(plateText);
                if (clean.Length == 0) return false;
                if (Look(save, car.CarId).plate == clean) return true;
                if (save.coins < CustomizationCatalog.PlatePrice) return false;
                save.coins -= CustomizationCatalog.PlatePrice;
                GetOrAdd(save, car.CarId).plate = clean;
                return true;
            }
            int cost = CostToApply(save, car.CarId, slot, value);
            if (save.coins < cost) return false;
            CarLook l = GetOrAdd(save, car.CarId);
            if (cost > 0) { save.coins -= cost; l.owned.Add(CustomizationCatalog.Key(slot, value)); }
            switch (slot)
            {
                case CustomSlot.Paint: l.paint = Mathf.Clamp(value, -1, CustomizationCatalog.Paints.Length - 1); break;
                case CustomSlot.Finish: l.finish = Mathf.Clamp(value, 0, 3); break;
                case CustomSlot.Rims: l.rims = Mathf.Clamp(value, -1, CustomizationCatalog.Rims.Length - 1); break;
                case CustomSlot.Tint: l.tint = Mathf.Clamp(value, 0, 3); break;
                case CustomSlot.Headlights: l.headlights = Mathf.Clamp(value, 0, 3); break;
            }
            return true;
        }

        /// <summary>Back to the factory look (free). Bought options stay owned, so re-applying them is free.</summary>
        public static void ResetToFactory(SaveData save, string carId)
        {
            CarLook l = GetOrAdd(save, carId);
            l.paint = -1; l.finish = 0; l.rims = -1; l.tint = 0; l.headlights = 0; l.plate = "";
        }
    }
}
