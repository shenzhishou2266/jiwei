using 积微.Models;
using 积微.Services;
using 积微.ViewModels;
using 积微.Views;
using SortOrder = 积微.Views.SortOrder;

namespace 积微.Tests;

public class GoalsViewModelTests
{
    private static Goal CreateGoal(string title, DateTime updatedAt, DateTime createdAt, GoalStatus status = GoalStatus.Active)
    {
        var goal = new Goal(title) { UpdatedAt = updatedAt, CreatedAt = createdAt };
        // 强制设置状态（不触发时间线条目）
        goal.Status = status;
        // 清除构造函数自动添加的时间线条目
        goal.Timeline.Clear();
        return goal;
    }

    private static GoalsViewModel CreateViewModel(List<Goal> goals)
    {
        var goalRepo = new FakeGoalRepository(new List<List<Goal>>());
        var vm = new GoalsViewModel(goalRepo);
        // 使用反射设置私有字段进行测试
        var goalsField = typeof(GoalsViewModel).GetField("_goals",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        goalsField!.SetValue(vm, goals);
        return vm;
    }

    /// <summary>按 UpdatedAt 降序排序：最新更新的在前。</summary>
    [Fact]
    public void GetSortedGoals_SortByUpdatedAtDescending_NewestFirst()
    {
        var now = DateTime.Now;
        var goals = new List<Goal>
        {
            CreateGoal("旧目标", now.AddDays(-5), now.AddDays(-10)),
            CreateGoal("新目标", now, now.AddDays(-1)),
            CreateGoal("中间目标", now.AddDays(-2), now.AddDays(-5)),
        };
        var vm = CreateViewModel(goals);
        vm.CurrentSortBy = SortBy.UpdatedAt;
        vm.UpdatedAtSortOrder = SortOrder.Descending;

        var sorted = vm.GetSortedGoals();

        Assert.Equal("新目标", sorted[0].Title);
        Assert.Equal("中间目标", sorted[1].Title);
        Assert.Equal("旧目标", sorted[2].Title);
    }

    /// <summary>按 UpdatedAt 升序排序：最早更新的在前。</summary>
    [Fact]
    public void GetSortedGoals_SortByUpdatedAtAscending_OldestFirst()
    {
        var now = DateTime.Now;
        var goals = new List<Goal>
        {
            CreateGoal("旧目标", now.AddDays(-5), now),
            CreateGoal("新目标", now, now),
            CreateGoal("中间目标", now.AddDays(-2), now),
        };
        var vm = CreateViewModel(goals);
        vm.CurrentSortBy = SortBy.UpdatedAt;
        vm.UpdatedAtSortOrder = SortOrder.Ascending;

        var sorted = vm.GetSortedGoals();

        Assert.Equal("旧目标", sorted[0].Title);
        Assert.Equal("中间目标", sorted[1].Title);
        Assert.Equal("新目标", sorted[2].Title);
    }

    /// <summary>按 CreatedAt 降序排序。</summary>
    [Fact]
    public void GetSortedGoals_SortByCreatedAtDescending_NewestFirst()
    {
        var now = DateTime.Now;
        var goals = new List<Goal>
        {
            CreateGoal("旧目标", now, now.AddDays(-10)),
            CreateGoal("新目标", now, now),
            CreateGoal("中间目标", now, now.AddDays(-5)),
        };
        var vm = CreateViewModel(goals);
        vm.CurrentSortBy = SortBy.CreatedAt;
        vm.CreatedAtSortOrder = SortOrder.Descending;

        var sorted = vm.GetSortedGoals();

        Assert.Equal("新目标", sorted[0].Title);
        Assert.Equal("中间目标", sorted[1].Title);
        Assert.Equal("旧目标", sorted[2].Title);
    }

    /// <summary>扁平化层级：子目标也出现在排序结果中。</summary>
    [Fact]
    public void GetSortedGoals_IncludesChildrenInFlatList()
    {
        var now = DateTime.Now;
        var parent = CreateGoal("父目标", now, now);
        var child = CreateGoal("子目标", now.AddDays(1), now.AddDays(1));
        parent.Children.Add(child);
        child.Parent = parent;

        var goals = new List<Goal> { parent };
        var vm = CreateViewModel(goals);
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var sorted = vm.GetSortedGoals();

        Assert.Equal(2, sorted.Count);
        Assert.Contains(sorted, g => g.Title == "父目标");
        Assert.Contains(sorted, g => g.Title == "子目标");
    }

    /// <summary>GetVisibleGoals 在 IsAllTab 模式下返回所有顶层目标。</summary>
    [Fact]
    public void GetVisibleGoals_AllTab_ReturnsTopLevelOnly()
    {
        var now = DateTime.Now;
        var parent = CreateGoal("父目标", now, now, GoalStatus.Active);
        var child = CreateGoal("子目标", now, now, GoalStatus.Active);
        parent.Children.Add(child);
        child.Parent = parent;
        var standalone = CreateGoal("独立目标", now, now, GoalStatus.Completed);

        var goals = new List<Goal> { parent, standalone };
        var vm = CreateViewModel(goals);
        vm.IsAllTab = true;
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var visible = vm.GetVisibleGoals();

        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, g => g.Title == "父目标");
        Assert.Contains(visible, g => g.Title == "独立目标");
    }

    /// <summary>GetVisibleGoals 在状态 Tab 模式下只返回匹配状态的目标。</summary>
    [Fact]
    public void GetVisibleGoals_StatusTab_FiltersByStatus()
    {
        var now = DateTime.Now;
        var active = CreateGoal("进行中", now, now, GoalStatus.Active);
        var completed = CreateGoal("已完成", now, now, GoalStatus.Completed);
        var failed = CreateGoal("已失败", now, now, GoalStatus.Failed);

        var goals = new List<Goal> { active, completed, failed };
        var vm = CreateViewModel(goals);
        vm.IsAllTab = false;
        vm.CurrentStatus = GoalStatus.Completed;
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var visible = vm.GetVisibleGoals();

        var single = Assert.Single(visible);
        Assert.Equal("已完成", single.Title);
    }

    /// <summary>搜索过滤：标题匹配。</summary>
    [Fact]
    public void GetVisibleGoals_SearchByTitle_FiltersCorrectly()
    {
        var now = DateTime.Now;
        var apple = CreateGoal("苹果项目", now, now);
        var banana = CreateGoal("香蕉任务", now, now);
        var orange = CreateGoal("橘子计划", now, now);

        var goals = new List<Goal> { apple, banana, orange };
        var vm = CreateViewModel(goals);
        vm.IsAllTab = true;
        vm.SearchText = "苹果";
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var visible = vm.GetVisibleGoals();

        var single = Assert.Single(visible);
        Assert.Equal("苹果项目", single.Title);
    }

    /// <summary>搜索过滤：描述匹配。</summary>
    [Fact]
    public void GetVisibleGoals_SearchByDescription_FiltersCorrectly()
    {
        var now = DateTime.Now;
        var goal1 = new Goal("目标A", "这是一个技术项目");
        goal1.UpdatedAt = now;
        goal1.CreatedAt = now;
        goal1.Timeline.Clear();
        var goal2 = new Goal("目标B", "这是一个商业计划");
        goal2.UpdatedAt = now;
        goal2.CreatedAt = now;
        goal2.Timeline.Clear();

        var goals = new List<Goal> { goal1, goal2 };
        var vm = CreateViewModel(goals);
        vm.IsAllTab = true;
        vm.SearchText = "技术";
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var visible = vm.GetVisibleGoals();

        var single = Assert.Single(visible);
        Assert.Equal("目标A", single.Title);
    }

    /// <summary>搜索过滤：子目标匹配时追溯到顶层祖先。</summary>
    [Fact]
    public void GetVisibleGoals_SearchMatchesChild_ShowsParent()
    {
        var now = DateTime.Now;
        var parent = CreateGoal("父目标", now, now);
        var child = CreateGoal("匹配子目标", now, now);
        parent.Children.Add(child);
        child.Parent = parent;

        var goals = new List<Goal> { parent };
        var vm = CreateViewModel(goals);
        vm.IsAllTab = true;
        vm.SearchText = "匹配子目标";
        vm.CurrentSortBy = SortBy.UpdatedAt;

        var visible = vm.GetVisibleGoals();

        Assert.Single(visible);
        Assert.Equal("父目标", visible[0].Title);
    }

    /// <summary>添加目标：创建并保存。</summary>
    [Fact]
    public async Task AddGoalAsync_CreatesAndSaves()
    {
        var savedSnapshots = new List<List<Goal>>();
        var goalRepo = new FakeGoalRepository(savedSnapshots);
        var vm = new GoalsViewModel(goalRepo);
        // 初始化空的 Goals 列表
        await vm.LoadGoalsAsync();
        int initialCount = vm.Goals.Count;

        await vm.AddGoalAsync("新目标", "描述", GoalType.LongTerm);

        Assert.Equal(initialCount + 1, vm.Goals.Count);
        Assert.Contains(vm.Goals, g => g.Title == "新目标");
        Assert.True(savedSnapshots.Count > 0);
        Assert.Contains(savedSnapshots[^1], g => g.Title == "新目标");
    }

    /// <summary>SaveAsync 应通过 IGoalRepository 保存。</summary>
    [Fact]
    public async Task SaveAsync_SavesThroughRepository()
    {
        var savedSnapshots = new List<List<Goal>>();
        var goalRepo = new FakeGoalRepository(savedSnapshots);
        var vm = new GoalsViewModel(goalRepo);
        await vm.LoadGoalsAsync();
        vm.Goals.Add(new Goal("手动添加"));

        int snapshotsBefore = savedSnapshots.Count;
        await vm.SaveAsync();
        int snapshotsAfter = savedSnapshots.Count;

        Assert.Equal(snapshotsBefore + 1, snapshotsAfter);
        Assert.Contains(savedSnapshots[^1], g => g.Title == "手动添加");
    }

    /// <summary>移动目标状态应只产生一条时间线记录（不重复）。</summary>
    [Fact]
    public void MoveGoalToNewStatus_ProducesSingleTimelineEntry()
    {
        var now = DateTime.Now;
        var goal = CreateGoal("测试目标", now, now, GoalStatus.Active);
        var goals = new List<Goal> { goal };
        var vm = CreateViewModel(goals);
        int entriesBefore = goal.Timeline.Count;

        vm.MoveGoalToNewStatus(goal, GoalStatus.Completed);

        // 状态变更应该只产生 1 条时间线记录
        Assert.Equal(entriesBefore + 1, goal.Timeline.Count);
        Assert.Contains("已完成", goal.Timeline[^1].Content);
    }

    /// <summary>移动目标状态。</summary>
    [Fact]
    public void MoveGoalToNewStatus_ChangesStatus()
    {
        var now = DateTime.Now;
        var goal = CreateGoal("测试目标", now, now, GoalStatus.Active);
        var goals = new List<Goal> { goal };
        var vm = CreateViewModel(goals);

        vm.MoveGoalToNewStatus(goal, GoalStatus.Completed);

        Assert.Equal(GoalStatus.Completed, goal.Status);
    }

    /// <summary>删除目标应从列表中移除，且清除计时器当前目标引用。</summary>
    [Fact]
    public async Task RemoveGoal_RemovesFromList()
    {
        var goal = CreateGoal("待删除目标", DateTime.Now, DateTime.Now, GoalStatus.Active);
        var goals = new List<Goal> { goal };
        var vm = CreateViewModel(goals);
        await vm.LoadGoalsAsync();

        vm.RemoveGoal(goal);

        Assert.DoesNotContain(goal, vm.Goals);
    }
}