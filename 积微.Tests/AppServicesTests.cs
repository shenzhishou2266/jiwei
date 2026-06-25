using Microsoft.Extensions.DependencyInjection;
using 积微.Services;
using 积微.ViewModels;

namespace 积微.Tests;

public class AppServicesTests
{
    /// <summary>容器构建后，Parser 应能解析所有注册的服务。</summary>
    [Fact]
    public void Configure_ResolvesAllRegisteredServices()
    {
        // Act
        AppServices.Configure();
        var provider = AppServices.Provider;

        // Assert: 所有核心服务可解析
        Assert.NotNull(provider.GetService<IGoalRepository>());
        Assert.NotNull(provider.GetService<ISessionRepository>());
        Assert.NotNull(provider.GetService<SessionRecorder>());
        Assert.NotNull(provider.GetService<TimerService>());
        Assert.NotNull(provider.GetService<FocusSessionService>());
        Assert.NotNull(provider.GetService<GoalTimerManager>());
        Assert.NotNull(provider.GetService<GoalsViewModel>());
    }

    /// <summary>单例服务应保持同一实例。</summary>
    [Fact]
    public void Configure_SingletonServices_ReturnsSameInstance()
    {
        AppServices.Configure();
        var provider = AppServices.Provider;

        var timer1 = provider.GetRequiredService<TimerService>();
        var timer2 = provider.GetRequiredService<TimerService>();
        Assert.Same(timer1, timer2);

        var focus1 = provider.GetRequiredService<FocusSessionService>();
        var focus2 = provider.GetRequiredService<FocusSessionService>();
        Assert.Same(focus1, focus2);

        var mgr1 = provider.GetRequiredService<GoalTimerManager>();
        var mgr2 = provider.GetRequiredService<GoalTimerManager>();
        Assert.Same(mgr1, mgr2);

        var repo1 = provider.GetRequiredService<IGoalRepository>();
        var repo2 = provider.GetRequiredService<IGoalRepository>();
        Assert.Same(repo1, repo2);
    }

    /// <summary>GoalsViewModel 应注册为 Singleton（单一真源，避免 GoalsProvider 冲突）。</summary>
    [Fact]
    public void Configure_GoalsViewModelIsSingleton()
    {
        AppServices.Configure();
        var provider = AppServices.Provider;

        var vm1 = provider.GetRequiredService<GoalsViewModel>();
        var vm2 = provider.GetRequiredService<GoalsViewModel>();

        Assert.Same(vm1, vm2);
    }

    /// <summary>TimerService 内部 SessionRecorder 应由 DI 注入。</summary>
    [Fact]
    public void Configure_TimerService_HasSessionRecorderInjected()
    {
        AppServices.Configure();
        var provider = AppServices.Provider;

        var timer = provider.GetRequiredService<TimerService>();
        var sessionRecorder = provider.GetRequiredService<SessionRecorder>();

        // 验证 TimerService 内部的 SessionRecorder 与 DI 容器中的是同一个
        var timerField = typeof(TimerService).GetField("_sessionRecorder",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var injected = timerField!.GetValue(timer);

        Assert.Same(sessionRecorder, injected);
    }
}