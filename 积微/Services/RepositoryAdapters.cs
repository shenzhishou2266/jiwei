using 积微.Models;

namespace 积微.Services
{
    /// <summary>目标存储适配器，将 DataStorageService 的静态方法适配为 IGoalRepository 接口。</summary>
    public class GoalRepositoryAdapter : IGoalRepository
    {
        public async Task<List<Goal>> LoadGoalsAsync()
        {
            return await DataStorageService.LoadGoalsAsync();
        }

        public async Task SaveGoalsAsync(List<Goal> goals)
        {
            await DataStorageService.SaveGoalsAsync(goals);
        }
    }

    /// <summary>会话存储适配器，将 StatisticsService 的静态方法适配为 ISessionRepository 接口。</summary>
    public class SessionRepositoryAdapter : ISessionRepository
    {
        public async Task AddSessionAsync(SessionRecord session)
        {
            await StatisticsService.AddSessionAsync(session);
        }
    }
}