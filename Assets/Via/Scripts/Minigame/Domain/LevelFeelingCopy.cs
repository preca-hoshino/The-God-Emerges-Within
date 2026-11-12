namespace Via.Minigame.Domain
{
    // Player-facing feeling sentence shown before and after the story minigames.
    public static class LevelFeelingCopy
    {
        private const string Blank = "_______";

        public static string Before(string levelId)
        {
            string template = Template(levelId);
            return template == null ? null : template.Replace("{0}", Blank);
        }

        public static string After(string levelId, string displayName)
        {
            string template = Template(levelId);
            return template == null ? null : template.Replace("{0}", "【" + Feeling(displayName) + "】");
        }

        private static string Feeling(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;
            int separator = displayName.LastIndexOf('·');
            return (separator >= 0 ? displayName.Substring(separator + 1) : displayName).Trim();
        }

        private static string Template(string levelId)
        {
            switch (levelId)
            {
                case "Level_02": return "对这块蛋糕感觉到很{0}";
                case "Level_03": return "对这颗传传糖感觉到很{0}";
                case "Level_04": return "面对陌生人进入房间，{0}地回应试试";
                case "Level_05": return "第一次见到300号房间门牌，这令人有些{0}";
                case "Level_06": return "学妹状况好像不太对……应该{0}";
                case "Level_08": return "想让人加入“神谱”计划吗？{0}地回应他";
                default: return null;
            }
        }
    }
}