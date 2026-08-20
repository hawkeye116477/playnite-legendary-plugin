using System;
using System.Collections.Generic;

namespace LegendaryLibraryNS.Models;

public class LegendaryAchievements
{
    public int Total_achievements { get; set; }
    public int Total_product_xp { get; set; }
    public List<AchievementSets>? Achievement_sets { get; set; }
    public PlatinumRarity? Platinum_rarity { get; set; }
    public List<AchievementData> Completed { get; set; } = [];
    public List<AchievementData> In_progress { get; set; } = [];
    public List<AchievementData> Uninitiated { get; set; } = [];
    public List<AchievementData> Hidden { get; set; } = [];
    public int User_unlocked { get; set; }
    public int User_xp { get; set; }
    public object[]? User_awards { get; set; }
    
    public class AchievementSets
    {
        public string? AchievementSetId { get; set; }
        public bool IsBase { get; set; }
        public int TotalAchievements { get; set; }
        public int TotalXp { get; set; }
    }

    public class PlatinumRarity
    {
        public double Percent { get; set; }
    }

    public class AchievementData
    {
        public string Name { get; set; } = "";
        public bool Is_base { get; set; }
        public bool Hidden { get; set; }
        public int Xp { get; set; }
        public bool Unlocked { get; set; }
        public double Progress { get; set; }
        public DateTimeOffset? Unlock_date { get; set; }
        public string Display_name { get; set; }  = "";
        public string Description { get; set; } = "";
        public string? Icon_id { get; set; }
        public string? Icon_link { get; set; }
        public Tier Tier { get; set; } = new();
        public Rarity Rarity { get; set; } = new();
    }

    public class Tier
    {
        public string HexColor { get; set; } = "";
        public int Max { get; set; }
        public int Min { get; set; }
        public string Name { get; set; } = "";
    }

    public class Rarity
    {
        public double Percent { get; set; }
    }
}