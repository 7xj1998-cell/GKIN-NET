using System.Collections.Generic;

namespace GKIN
{
    public static partial class CadEngine
    {
        public static List<string> LastTitles { get; set; } = new List<string>();

        static string TitleOf(SheetPlan plan)
        {
            if (plan == null) return "";
            string kind = plan.Type == "BD" ? "BÌNH ĐỒ"
                : plan.Type == "TD" ? "TRẮC DỌC TUYẾN"
                : plan.Type == "TN" ? "TRẮC NGANG"
                : plan.Type;
            if (plan.Index > 0) return kind + " · tờ " + plan.Index;
            return kind;
        }
    }
}
