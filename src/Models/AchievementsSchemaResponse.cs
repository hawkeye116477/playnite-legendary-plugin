using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class AchievementSet
    {
        public string? AchievementSetId { get; set; }
        public bool IsBase { get; set; }
        public int TotalAchievements { get; set; } = 0;
        public int TotalXP { get; set; } = 0;
    }

    public class AchievementsSchemaResponse
    {
        public AchievementData Data { get; set; } = new();

        public class AchievementData
        {
            public Achievement Achievement { get; set; } = new();
        }

        public class Achievement
        {
            public ProductAchievementsRecord ProductAchievementsRecordBySandbox { get; set; } = new();
        }

        public class ProductAchievementsRecord
        {
            public string? ProductId { get; set; }
            public List<AchievementSet> AchievementSets { get; set; } = [];

            public Rarity PlatinumRarity { get; set; } = new();
            public List<AchievementWrapper> Achievements { get; set; } = [];
        }

        public class AchievementWrapper
        {
            public AchievementDetail Achievement { get; set; } = new();
        }

        public class AchievementDetail
        {
            public string Name { get; set; } = "";
            public bool Hidden { get; set; }
            public bool IsBase { get; set; }
            public string? AchievementSetId { get; set; }
            public string UnlockedDisplayName { get; set; } = "";
            public string LockedDisplayName { get; set; }  = "";
            public string? UnlockedDescription { get; set; }
            public string? LockedDescription { get; set; }
            public string? UnlockedIconId { get; set; }
            public string? LockedIconId { get; set; }
            public int XP { get; set; }
            public string? FlavorText { get; set; }
            public string? UnlockedIconLink { get; set; }
            public string? LockedIconLink { get; set; }
            public Tier? Tier { get; set; }
            public Rarity? Rarity { get; set; }
        }

        public class Tier
        {
            public string Name { get; set; } = "";
            public string? HexColor { get; set; }
            public int Min { get; set; } = 0;
            public int Max { get; set; } = 0;
        }

        public class Rarity
        {
            public double Percent { get; set; }
        }
    }

    public class PlayerAchievementsResponse
    {
        public PlayerProfileData Data { get; set; } = new();

        public class PlayerProfileData
        {
            public PlayerProfile PlayerProfile { get; set; } = new();
        }

        public class PlayerProfile
        {
            public PlayerProfileInfo PlayerProfileInfo { get; set; } = new();
        }

        public class PlayerProfileInfo
        {
            public ProductAchievements ProductAchievements { get; set; } = new();
        }

        public class ProductAchievements
        {
            public ProductAchievementsData Data { get; set; } = new();
        }

        public class ProductAchievementsData
        {
            public int TotalXP { get; set; } = 0;
            public int TotalUnlocked { get; set; } = 0;
            public List<AchievementSet> AchievementSets { get; set; } = [];
            public List<PlayerAward> PlayerAwards { get; set; } = [];
            public List<PlayerAchievementWrapper> PlayerAchievements { get; set; } = [];
        }

        public class PlayerAchievementSet
        {
            public string? AchievementSetId { get; set; }
            public bool IsBase { get; set; }
            public int TotalUnlocked { get; set; } = 0;
            public int TotalXP { get; set; } = 0;
        }

        public class PlayerAward
        {
            public string? AwardType { get; set; }
            public string? UnlockedDateTime { get; set; }
            public string? AchievementSetId { get; set; }
        }

        public class PlayerAchievementWrapper
        {
            public PlayerAchievementDetail PlayerAchievement { get; set; } = new();
        }

        public class PlayerAchievementDetail
        {
            public string AchievementName { get; set; } = "";
            public double Progress { get; set; }
            public bool Unlocked { get; set; }
            public string UnlockDate { get; set; } = "";
            public int XP { get; set; } = 0;
            public string? AchievementSetId { get; set; }
            public bool IsBase { get; set; }
        }
    }
}