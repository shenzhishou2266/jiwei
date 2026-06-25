using 积微.Models;

namespace 积微.ViewModels
{
    /// <summary>统计数据视图模型，封装今日统计的比较和格式化逻辑。</summary>
    public class StatsViewModel
    {
        /// <summary>格式化小时显示。</summary>
        public static string FormatHours(double hours)
        {
            if (hours == 0)
                return "0";
            return hours.ToString("F1");
        }

        /// <summary>比较两个整数并返回（文本，变化类型）元组。</summary>
        public static (string Text, string ChangeType) CompareInt(int today, int yesterday, string unit)
        {
            int diff = today - yesterday;
            if (diff > 0)
                return ($"比昨天多 {diff} {unit}", "Positive");
            if (diff < 0)
                return ($"比昨天少 {Math.Abs(diff)} {unit}", "Negative");
            return ("与昨天持平", "Zero");
        }

        /// <summary>比较两个小数并返回（文本，变化类型）元组。</summary>
        public static (string Text, string ChangeType) CompareDouble(double today, double yesterday, string unit)
        {
            double diff = today - yesterday;
            if (diff > 0)
                return ($"较昨天 +{diff:F1}{unit}", "Positive");
            if (diff < 0)
                return ($"较昨天 -{Math.Abs(diff):F1}{unit}", "Negative");
            return ("与昨天持平", "Zero");
        }
    }
}