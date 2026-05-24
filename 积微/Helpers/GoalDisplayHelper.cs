using System;
using System.Collections.Generic;
using 积微.Models;

namespace 积微.Helpers
{
    /// <summary>目标显示辅助类，提供目标名称去重和标题计数功能。</summary>
    public static class GoalDisplayHelper
    {
        /// <summary>获取目标的显示名称，如有重名则附加创建时间区分。</summary>
        public static string GetGoalDisplayName(Goal goal, Dictionary<string, int> titleCount)
        {
            if (titleCount != null && titleCount.ContainsKey(goal.Title) && titleCount[goal.Title] > 1)
            {
                return $"{goal.Title} ({goal.CreatedAt:yyyy-MM-dd HH:mm})";
            }
            return goal.Title;
        }

        /// <summary>统计所有目标的标题出现次数（含子目标）。</summary>
        public static Dictionary<string, int> GetTitleCount(IEnumerable<Goal> goals)
        {
            var titleCount = new Dictionary<string, int>();
            AddGoalsToTitleCount(titleCount, goals);
            return titleCount;
        }

        private static void AddGoalsToTitleCount(Dictionary<string, int> titleCount, IEnumerable<Goal> goals)
        {
            foreach (var goal in goals)
            {
                if (titleCount.ContainsKey(goal.Title))
                    titleCount[goal.Title]++;
                else
                    titleCount[goal.Title] = 1;

                // 递归添加子目标
                if (goal.Children.Count > 0)
                    AddGoalsToTitleCount(titleCount, goal.Children);
            }
        }
    }
}