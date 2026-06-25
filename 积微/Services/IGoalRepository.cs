using 积微.Models;

namespace 积微.Services
{
    /// <summary>目标数据存储接口，抽象持久化操作以便测试。</summary>
    public interface IGoalRepository
    {
        /// <summary>加载目标列表。</summary>
        Task<List<Goal>> LoadGoalsAsync();

        /// <summary>保存目标列表。</summary>
        Task SaveGoalsAsync(List<Goal> goals);
    }

    /// <summary>会话记录存储接口，抽象持久化操作以便测试。</summary>
    public interface ISessionRepository
    {
        /// <summary>添加一条会话记录。</summary>
        Task AddSessionAsync(SessionRecord session);
    }
}