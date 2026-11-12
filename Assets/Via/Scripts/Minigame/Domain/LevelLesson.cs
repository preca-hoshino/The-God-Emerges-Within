namespace Via.Minigame.Domain
{
    // Player-facing lesson hints for the configured levels.
    public static class LevelLesson
    {
        public static bool StartsWithArcDrill(string levelId) => levelId == "level_04";

        public static string Hint(string levelId)
        {
            switch ((levelId ?? string.Empty).ToLowerInvariant())
            {
                case "level_01":
                    return "从绿色端点引出折角可形成折线·敌意；继续引导路径闭合可形成多边形·亲近。确认后进入对应的咖啡店剧情。";
                case "level_03":
                    return "用糖果查看样本。Y 形提交为厌恶，8 字形为喜欢，三角形为疑虑。";
                case "level_04":
                    return "先把折角重塑成圆弧。正式解题可提交好奇、亲切或戒备。糖果可查看对应样本。";
                case "level_05":
                    return "配合牵引和重塑。三角形是担忧，S 形是好奇，波浪形是兴奋。糖果可查看样本。";
                case "level_06":
                    return "多边形回应关切，三角形回应担心，X 形回应恐惧。确认后继续病房剧情。";
                case "level_07":
                    return "医生会推动连接形成8字形，达成后本轮抵抗失败。切断并形成两个开放、无分叉、无交会的部分，再提交抗拒。";
                case "level_08":
                    return "8字形选择成为神谱公司顾问，双直线选择继续自己的研究，Y形选择对抗神谱计划。确认后进入对应结局。";
                default:
                    return null;
            }
        }
    }
}
