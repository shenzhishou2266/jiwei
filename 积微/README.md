# 积微

专注与时间管理 WPF 应用 (.NET 8.0)

## 项目结构

```
积微/
├── App.xaml / App.xaml.cs              # 应用入口，窗口生命周期、主题切换、系统托盘
├── AssemblyInfo.cs                     # 程序集信息
│
├── Models/                             # 数据模型层（纯数据结构，无业务逻辑）
│   ├── Goal.cs                         # 目标、时间线条目、状态变更记录
│   ├── SessionRecord.cs                # 会话记录、每日统计(DailyStats)
│   ├── Settings.cs                     # 应用设置(AppSettings)、设置管理器(SettingsManager)
│   ├── TimerSessionType.cs             # 计时器会话类型枚举
│   └── Audio/
│       ├── NotificationSound.cs        # 提示音数据模型
│       └── WhiteNoise.cs               # 白噪音数据模型
│
├── Services/                           # 业务服务层（单例服务、管理器）
│   ├── TimerService.cs                 # 计时器服务（秒表/倒计时/番茄钟）
│   ├── FocusSessionService.cs          # 番茄钟会话服务（工作/休息状态机）
│   ├── StatisticsService.cs            # 统计服务（日报/周报/月报/年报）
│   ├── DataStorageService.cs           # 数据存储服务（JSON 序列化/反序列化）
│   ├── GoalTimerManager.cs             # 目标计时器管理器（多目标并行计时）
│   └── Audio/
│       ├── NotificationSoundManager.cs # 提示音管理器（加载/播放/音量控制）
│       └── WhiteNoiseManager.cs        # 白噪音管理器（加载/播放/循环控制）
│
├── ViewModels/                         # 视图模型层（MVVM 数据绑定）
│   ├── NotificationSoundViewModel.cs   # 提示音视图模型
│   └── WhiteNoiseViewModel.cs          # 白噪音视图模型
│
├── Views/                              # 视图层（窗口、页面）
│   ├── MainWindow.xaml/.cs             # 主窗口（页面导航、计时器模式切换）
│   ├── GoalsPage.xaml/.cs              # 目标管理页面（增删改查、状态切换、排序）
│   ├── StatsPage.xaml/.cs              # 统计页面（热力图、周柱状图、今日/月度/累计）
│   ├── SettingsPage.xaml/.cs           # 设置页面（番茄钟、提示音、白噪音、主题）
│   ├── WidgetWindow.xaml/.cs           # 悬浮窗（迷你计时器、快捷输入）
│   ├── TimelineWindow.xaml/.cs         # 时间线窗口（目标的时间记录与编辑）
│   ├── ReportWindow.xaml/.cs           # 统计报表窗口（日报/周报/月报/年报/自定义）
│   ├── GoalDetailWindow.xaml/.cs       # 目标详情窗口（编辑标题/描述/分析/反馈）
│   ├── AddGoalWindow.xaml/.cs          # 添加/编辑目标窗口
│   ├── QuickTimelineInputWindow.xaml/.cs # 快速时间线输入窗口（文本/图片）
│   ├── ImageViewerWindow.xaml/.cs      # 图片查看器窗口（缩放/拖拽）
│   ├── MessageBoxWindow.xaml/.cs       # 通用消息提示窗口
│   └── DeleteConfirmationWindow.xaml/.cs # 删除确认对话框窗口
│
├── Controls/                            # 自定义控件
│   ├── TimerControl.xaml/.cs            # 通用计时器控件（秒表/倒计时）
│   ├── FocusSessionControl.xaml/.cs     # 番茄钟计时器控件（工作/休息、目标选择）
│   ├── GoalItem.xaml/.cs                # 目标列表项控件（子目标、操作按钮）
│   ├── NumberScroll.xaml/.cs            # 数字滚动控件
│   └── CalendarDatePicker.xaml/.cs      # 日历日期选择器控件
│
├── Converters/                          # 值转换器
│   └── StringToUriConverter.cs          # 字符串转 URI 转换器
│
└── Helpers/                             # 工具辅助类
    └── GoalDisplayHelper.cs             # 目标显示辅助（名称去重、标题计数）
```

## 命名空间说明

| 目录 | 命名空间 | 说明 |
|------|----------|------|
| `Models/` + `Models/Audio/` | `积微.Models` / `积微.Models.Audio` | 纯数据模型，不含业务逻辑 |
| `Services/` | `积微.Services` | 业务服务与状态管理 |
| `Services/Audio/` | `积微.Services.Audio` | 音频播放与管理 |
| `ViewModels/` | `积微.ViewModels` | MVVM 视图模型 |
| `Views/` | `积微.Views` | WPF 窗口和页面 |
| `Controls/` | `积微.Controls` | 自定义 WPF 控件 |
| `Converters/` | `积微.Converters` | XAML 值转换器 |
| `Helpers/` | `积微.Helpers` | 通用工具类 |

## 技术栈

- .NET 8.0 / WPF
- System.Text.Json (数据持久化)
- Windows 系统托盘 / 悬浮窗
- 自定义控件与主题切换