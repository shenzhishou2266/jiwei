using Microsoft.Extensions.DependencyInjection;
using 积微.Models;
using 积微.ViewModels;

namespace 积微.Services
{
    /// <summary>应用程序组合根，负责 DI 容器的配置和创建。</summary>
    public static class AppServices
    {
        private static IServiceProvider? _provider;

        /// <summary>全局服务提供者。</summary>
        public static IServiceProvider Provider => _provider ?? throw new InvalidOperationException("AppServices.Configure() 尚未调用。");

        /// <summary>配置并构建 DI 容器。应在应用启动时调用一次。</summary>
        public static void Configure()
        {
            var services = new ServiceCollection();

            // 存储适配器
            services.AddSingleton<IGoalRepository, GoalRepositoryAdapter>();
            services.AddSingleton<ISessionRepository, SessionRepositoryAdapter>();

            // SessionRecorder 需要 Func<List<Goal>> 作为工厂
            services.AddSingleton<SessionRecorder>(sp =>
                new SessionRecorder(
                    sp.GetRequiredService<IGoalRepository>(),
                    sp.GetRequiredService<ISessionRepository>(),
                    () => GoalsViewModel.GoalsProvider?.Invoke() ?? new List<Goal>()));

            // 核心服务（单例）
            services.AddSingleton<TimerService>();
            services.AddSingleton<FocusSessionService>();
            services.AddSingleton<GoalTimerManager>();

            // ViewModel（瞬态，每次创建新实例）
            services.AddSingleton<GoalsViewModel>(sp =>
                new GoalsViewModel(sp.GetRequiredService<IGoalRepository>()));

            _provider = services.BuildServiceProvider();
        }
    }
}