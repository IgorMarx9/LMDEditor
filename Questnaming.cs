using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LMDTool
{
    public enum QuestCategory
    {
        Village,
        GuildRegular,
        GuildArena,
        GuildTraining,
        GuildSpecial,
        ProwlerVillage,
        ProwlerGuild,
    }

    public class QuestInfo
    {
        public string FileName = "";       // e.g. "q0000428"
        public bool IsProwler;
        public int GuildSubtype;            // 0=village, 1..4=guild subtype
        public int Stars;                   // 1..10
        public int MissionNumber;           // 1..99
        public QuestCategory Category;

        public string CategoryFolder()
        {
            switch (Category)
            {
                case QuestCategory.Village: return "Village";
                case QuestCategory.GuildRegular: return Path.Combine("Guild", "Regular");
                case QuestCategory.GuildArena: return Path.Combine("Guild", "Arena");
                case QuestCategory.GuildTraining: return Path.Combine("Guild", "Training");
                case QuestCategory.GuildSpecial: return Path.Combine("Guild", "Special");
                case QuestCategory.ProwlerVillage: return Path.Combine("Prowler", "Village");
                case QuestCategory.ProwlerGuild: return Path.Combine("Prowler", "Guild");
                default: return "Unknown";
            }
        }

        public string OutputSubfolder()
        {
            // CategoryFolder/Stars, e.g. "Guild/Regular/4"
            return Path.Combine(CategoryFolder(), Stars.ToString());
        }
    }

    /*
        Decodes MHXX quest file names of the form "qDDDDDDD" (7 digits
        after the 'q'), where the digits are:

            D1      reserved (observed: always 0)
            D2      0 = regular quest, 1 = Prowler (Cat) quest
            D3      0 = Village, 1 = Guild Regular, 2 = Arena,
                    3 = Training, 4 = Special Permission (Deviants)
            D4-D5   difficulty / star rank, 01-10
            D6-D7   mission number within that difficulty

        Confirmed against every example given by the user:
            q0000428 -> village,       4*, #28
            q0001044 -> village,       10*, #44
            q0010101 -> guild-regular, 1*, #1
            q0100201 -> prowler-village, 2*, #1
            q0110103 -> prowler-guild,   1*, #3

        This naming scheme applies to standalone quest files and to the
        numeric suffix of questData_*_jpn entries inside bundled archives.
    */
    public static class QuestNaming
    {
        static readonly Regex QuestNamePattern = new Regex(
            @"^q(\d)(\d)(\d)(\d{2})(\d{2})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public static bool TryParse(string fileNameWithoutExtension, out QuestInfo info)
        {
            info = new QuestInfo();

            Match match = QuestNamePattern.Match(fileNameWithoutExtension);

            if (!match.Success)
                return false;

            int d1 = int.Parse(match.Groups[1].Value);
            int d2 = int.Parse(match.Groups[2].Value);
            int d3 = int.Parse(match.Groups[3].Value);
            int stars = int.Parse(match.Groups[4].Value);
            int missionNumber = int.Parse(match.Groups[5].Value);

            if (stars < 1 || stars > 18)
                return false;

            bool isProwler = d2 != 0;
            int guildSubtype = d3;

            QuestCategory category;

            if (guildSubtype == 0)
            {
                category = isProwler ? QuestCategory.ProwlerVillage : QuestCategory.Village;
            }
            else if (isProwler)
            {
                // All Prowler-in-guild quests are grouped together,
                // regardless of guildSubtype, since the user only
                // requested a single "Prowler/Guild" bucket.
                category = QuestCategory.ProwlerGuild;
            }
            else
            {
                switch (guildSubtype)
                {
                    case 1: category = QuestCategory.GuildRegular; break;
                    case 2: category = QuestCategory.GuildArena; break;
                    case 3: category = QuestCategory.GuildTraining; break;
                    case 4: category = QuestCategory.GuildSpecial; break;
                    default: return false; // unknown subtype, don't guess
                }
            }

            info.FileName = fileNameWithoutExtension;
            info.IsProwler = isProwler;
            info.GuildSubtype = guildSubtype;
            info.Stars = stars;
            info.MissionNumber = missionNumber;
            info.Category = category;

            return true;
        }

        /*
            Filter used by the UI's category/star checkboxes. Returns true
            if the given quest should be included given the current
            selection.
        */
        public static bool MatchesFilter(
            QuestInfo info,
            QuestFilter filter
        )
        {
            if (info.Stars < filter.MinStars || info.Stars > filter.MaxStars)
                return false;

            switch (info.Category)
            {
                case QuestCategory.Village: return filter.IncludeVillage;
                case QuestCategory.GuildRegular: return filter.IncludeGuildRegular;
                case QuestCategory.GuildArena: return filter.IncludeGuildArena;
                case QuestCategory.GuildTraining: return filter.IncludeGuildTraining;
                case QuestCategory.GuildSpecial: return filter.IncludeGuildSpecial;
                case QuestCategory.ProwlerVillage: return filter.IncludeProwlerVillage;
                case QuestCategory.ProwlerGuild: return filter.IncludeProwlerGuild;
                default: return false;
            }
        }
    }

    /*
        Selection state for the quest export/import filter UI.
    */
    public class QuestColumn
    {
        public bool Regular = true, Prowler = true, Special = true;
        public int Min = 1, Max;
        public QuestColumn(int max) { Max = max; }
        public bool Any => Regular || Prowler;
    }
    public class QuestFilter
    {
        public QuestColumn Village = new QuestColumn(10);
        public QuestColumn Hub = new QuestColumn(10);
        public QuestColumn Pub = new QuestColumn(4);
        public QuestColumn Special = new QuestColumn(18) { Prowler = false };
        public bool Valid => Village.Min <= Village.Max && Hub.Min <= Hub.Max && Pub.Min <= Pub.Max && Special.Min <= Special.Max;
        public bool Any => Village.Any || Hub.Any || Pub.Any || Special.Regular;
        public bool IncludeVillage = true;
        public bool IncludeGuildRegular = true;
        public bool IncludeGuildArena = true;
        public bool IncludeGuildTraining = true;
        public bool IncludeGuildSpecial = true;
        public bool IncludeProwlerVillage = true;
        public bool IncludeProwlerGuild = true;

        public int MinStars = 1;
        public int MaxStars = 10;
    }
}