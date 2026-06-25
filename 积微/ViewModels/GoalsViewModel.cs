using System.ComponentModel;
using System.Runtime.CompilerServices;
using 积微.Models;
using 积微.Services;
using 积微.Views;
using SortOrder = 积微.Views.SortOrder;

namespace 积微.ViewModels
{
    /// <summary>目标管理页面的 ViewModel，封装数据加载、排序、过滤和 CRUD 逻辑。</summary>
    public class GoalsViewModel : INotifyPropertyChanged
    {
        private readonly IGoalRepository _goalRepo;
        private List<Goal> _goals = new();
        private Goal? _globalGoal;
        private GoalStatus _currentStatus = GoalStatus.Active;
        private SortBy _currentSortBy = SortBy.UpdatedAt;
        private SortOrder _updatedAtSortOrder = SortOrder.Descending;
        private SortOrder _createdAtSortOrder = SortOrder.Descending;
        private bool _isAllTab;
        private string _searchText = string.Empty;

        public const string GlobalGoalId = "global-goal";

        /// <summary>是否已加载数据。</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>当前全局目标列表的提供者，供 SessionRecorder 使用。</summary>
        public static Func<List<Goal>>? GoalsProvider { get; private set; }

        public GoalsViewModel(IGoalRepository goalRepo)
        {
            _goalRepo = goalRepo;
        }

        public List<Goal> Goals => _goals;
        public Goal? GlobalGoal => _globalGoal;

        public GoalStatus CurrentStatus
        {
            get => _currentStatus;
            set { _currentStatus = value; OnPropertyChanged(); }
        }

        public bool IsAllTab
        {
            get => _isAllTab;
            set { _isAllTab = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public SortBy CurrentSortBy
        {
            get => _currentSortBy;
            set { _currentSortBy = value; OnPropertyChanged(); }
        }

        public SortOrder UpdatedAtSortOrder
        {
            get => _updatedAtSortOrder;
            set { _updatedAtSortOrder = value; OnPropertyChanged(); }
        }

        public SortOrder CreatedAtSortOrder
        {
            get => _createdAtSortOrder;
            set { _createdAtSortOrder = value; OnPropertyChanged(); }
        }

        /// <summary>获取扁平化排序后的所有目标列表。</summary>
        public List<Goal> GetSortedGoals()
        {
            var allGoals = new HashSet<Goal>();
            CollectAllGoals(_goals, allGoals);

            if (_currentSortBy == SortBy.UpdatedAt)
            {
                return _updatedAtSortOrder == SortOrder.Descending
                    ? allGoals.OrderByDescending(g => g.UpdatedAt).ToList()
                    : allGoals.OrderBy(g => g.UpdatedAt).ToList();
            }
            else
            {
                return _createdAtSortOrder == SortOrder.Descending
                    ? allGoals.OrderByDescending(g => g.CreatedAt).ToList()
                    : allGoals.OrderBy(g => g.CreatedAt).ToList();
            }
        }

        private static void CollectAllGoals(List<Goal> source, HashSet<Goal> result)
        {
            foreach (var goal in source)
            {
                if (result.Add(goal))
                    CollectAllGoals(goal.Children, result);
            }
        }

        /// <summary>获取当前视图可见的目标列表（排序 + 状态过滤 + 搜索过滤）。</summary>
        public List<Goal> GetVisibleGoals()
        {
            var sortedGoals = GetSortedGoals();

            if (_isAllTab)
            {
                return GetVisibleGoalsAllTab(sortedGoals);
            }
            else
            {
                return GetVisibleGoalsStatusTab(sortedGoals);
            }
        }

        private List<Goal> GetVisibleGoalsAllTab(List<Goal> sortedGoals)
        {
            // 收集搜索匹配的顶层祖先 ID
            HashSet<string>? visibleTopLevelIds = null;
            if (!string.IsNullOrEmpty(_searchText))
            {
                visibleTopLevelIds = new HashSet<string>();
                foreach (var goal in sortedGoals)
                {
                    if (goal.Id == GlobalGoalId) continue;
                    if (MatchesSearch(goal))
                    {
                        var ancestor = goal;
                        while (ancestor.Parent != null)
                            ancestor = ancestor.Parent;
                        visibleTopLevelIds.Add(ancestor.Id);
                    }
                }
            }

            var result = new List<Goal>();
            foreach (var goal in sortedGoals)
            {
                if (goal.Id == GlobalGoalId) continue;
                if (goal.Parent != null) continue;

                if (visibleTopLevelIds != null && !visibleTopLevelIds.Contains(goal.Id))
                    continue;

                result.Add(goal);
            }
            return result;
        }

        private List<Goal> GetVisibleGoalsStatusTab(List<Goal> sortedGoals)
        {
            var statusGoals = sortedGoals
                .Where(g => g.Status == _currentStatus && g.Id != GlobalGoalId)
                .ToList();

            HashSet<string>? visibleIds = null;
            if (!string.IsNullOrEmpty(_searchText))
            {
                visibleIds = new HashSet<string>();
                foreach (var goal in statusGoals)
                {
                    if (MatchesSearch(goal))
                    {
                        var ancestor = goal;
                        while (ancestor.Parent != null && ancestor.Parent.Status == _currentStatus)
                            ancestor = ancestor.Parent;
                        AddDescendantsInStatus(ancestor, _currentStatus, visibleIds);
                    }
                }
            }

            var matchingGoals = visibleIds != null
                ? statusGoals.Where(g => visibleIds.Contains(g.Id)).ToList()
                : statusGoals;

            var matchingIds = new HashSet<string>(matchingGoals.Select(g => g.Id));
            var result = new List<Goal>();

            foreach (var goal in matchingGoals)
            {
                if (goal.Parent == null)
                {
                    result.Add(goal);
                }
                else if (!matchingIds.Contains(goal.Parent.Id))
                {
                    result.Add(goal);
                }
            }
            return result;
        }

        private static void AddDescendantsInStatus(Goal goal, GoalStatus status, HashSet<string> visibleIds)
        {
            if (goal.Status != status) return;
            visibleIds.Add(goal.Id);
            foreach (var child in goal.Children)
                AddDescendantsInStatus(child, status, visibleIds);
        }

        private bool MatchesSearch(Goal goal)
        {
            var titleMatch = (goal.Title ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            var descMatch = (goal.Description ?? "").IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            return titleMatch || descMatch;
        }

        /// <summary>加载目标列表。</summary>
        public async Task LoadGoalsAsync()
        {
            _goals = await _goalRepo.LoadGoalsAsync();
            GoalsProvider = () => _goals;
            IsLoaded = true;
            OnPropertyChanged(nameof(Goals));

            _globalGoal = _goals.FirstOrDefault(g => g.Id == GlobalGoalId);
            if (_globalGoal == null)
            {
                _globalGoal = new Goal("全局目标", "记录与任何目标无关的想法和内容")
                {
                    Id = GlobalGoalId
                };
                _globalGoal.Timeline.Clear();
                _goals.Add(_globalGoal);
                await _goalRepo.SaveGoalsAsync(_goals);
            }
        }

        /// <summary>添加新目标。</summary>
        public async Task AddGoalAsync(string title, string description, GoalType type)
        {
            var newGoal = new Goal(title, description) { Type = type };
            _goals.Add(newGoal);
            await _goalRepo.SaveGoalsAsync(_goals);
        }

        /// <summary>保存当前目标列表。</summary>
        public async Task SaveAsync()
        {
            await _goalRepo.SaveGoalsAsync(_goals);
        }

        /// <summary>移动目标到新状态。</summary>
        public void MoveGoalToNewStatus(Goal goal, GoalStatus newStatus)
        {
            switch (newStatus)
            {
                case GoalStatus.Completed:
                    goal.Complete();
                    break;
                case GoalStatus.Failed:
                    goal.Fail();
                    break;
                case GoalStatus.Active:
                    goal.Reactivate();
                    break;
                case GoalStatus.Pending:
                    goal.Pending();
                    break;
            }
        }

        /// <summary>从列表中移除目标。</summary>
        public void RemoveGoal(Goal goal)
        {
            _goals.Remove(goal);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}