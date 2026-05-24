using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace 积微.Models
{
    /// <summary>目标状态枚举</summary>
    public enum GoalStatus
    {
        /// <summary>进行中</summary>
        Active,
        /// <summary>已完成</summary>
        Completed,
        /// <summary>已失败</summary>
        Failed,
        /// <summary>已搁置</summary>
        Pending
    }

    /// <summary>目标状态变更记录</summary>
    public class StatusChange
    {
        /// <summary>状态</summary>
        public GoalStatus Status { get; set; }
        /// <summary>变更时间</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>是否为最终完成</summary>
        public bool IsFinalCompletion { get; set; }
    }

    /// <summary>目标类型枚举</summary>
    public enum GoalType
    {
        /// <summary>长期目标</summary>
        LongTerm,
        /// <summary>短期目标</summary>
        ShortTerm,
        /// <summary>重复目标</summary>
        Recurring
    }

    /// <summary>时间线条目类型枚举</summary>
    public enum TimelineEntryType
    {
        /// <summary>操作记录</summary>
        Operation,
        /// <summary>想法记录</summary>
        Thought,
        /// <summary>问题记录</summary>
        Question
    }

    /// <summary>时间线条目</summary>
    public class TimelineEntry : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private string _content;
        private TimelineEntryType _type;
        private List<string> _imagePathList;

        /// <summary>时间戳</summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        /// <summary>内容</summary>
        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged();
            }
        }

        /// <summary>类型</summary>
        public TimelineEntryType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        /// <summary>图片路径列表</summary>
        public List<string> ImagePathList
        {
            get => _imagePathList;
            set
            {
                _imagePathList = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasImages));
            }
        }

        /// <summary>是否有图片</summary>
        [JsonIgnore]
        public bool HasImages => ImagePathList != null && ImagePathList.Count > 0;

        /// <summary>构造时间线条目</summary>
        public TimelineEntry()
        {
            Timestamp = DateTime.Now;
            Content = string.Empty;
            Type = TimelineEntryType.Operation;
            ImagePathList = new List<string>();
        }

        /// <summary>构造时间线条目</summary>
        public TimelineEntry(string content, TimelineEntryType type = TimelineEntryType.Operation, DateTime? timestamp = null)
            : this()
        {
            Content = content;
            Type = type;
            Timestamp = timestamp ?? DateTime.Now;
        }

        /// <summary>构造时间线条目（含图片列表）</summary>
        public TimelineEntry(string content, TimelineEntryType type, DateTime? timestamp, List<string> imagePathList)
            : this(content, type, timestamp)
        {
            ImagePathList = imagePathList ?? new List<string>();
        }

        /// <summary>添加图片</summary>
        public void AddImage(string imagePath)
        {
            if (ImagePathList == null)
            {
                ImagePathList = new List<string>();
            }
            ImagePathList.Add(imagePath);
            OnPropertyChanged(nameof(ImagePathList));
            OnPropertyChanged(nameof(HasImages));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>目标类，支持层级结构和时间线记录</summary>
    public class Goal
    {
        /// <summary>时间线条目添加事件（静态）</summary>
        public static event EventHandler? TimelineEntryAdded;
        /// <summary>子目标变更事件</summary>
        public event EventHandler? ChildrenChanged;
        /// <summary>时长变更事件（自身或子目标时长变化时触发，沿父目标链向上传播）</summary>
        public event EventHandler? DurationChanged;

        /// <summary>目标ID</summary>
        public string Id { get; set; }
        /// <summary>目标标题</summary>
        public string Title { get; set; }
        /// <summary>目标描述</summary>
        public string Description { get; set; }
        /// <summary>过程分析</summary>
        public string ProcessAnalysis { get; set; }
        /// <summary>结果反馈</summary>
        public string ResultFeedback { get; set; }
        /// <summary>目标状态</summary>
        public GoalStatus Status { get; set; }
        /// <summary>目标类型</summary>
        public GoalType Type { get; set; }
        /// <summary>子目标列表</summary>
        public List<Goal> Children { get; set; }
        /// <summary>父目标</summary>
        public Goal Parent { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>完成时间</summary>
        public DateTime? CompletedAt { get; set; }
        /// <summary>失败时间</summary>
        public DateTime? FailedAt { get; set; }
        /// <summary>搁置时间</summary>
        public DateTime? PendingAt { get; set; }
        /// <summary>状态变更历史</summary>
        public List<StatusChange> StatusHistory { get; set; } = new();
        /// <summary>最后更新时间</summary>
        public DateTime UpdatedAt { get; set; }
        /// <summary>时间线记录集合</summary>
        public ObservableCollection<TimelineEntry> Timeline { get; set; }
        /// <summary>累计用时（秒）</summary>
        public int TotalElapsedSeconds { get; set; }
        /// <summary>总时长（包含所有子目标，计算属性）</summary>
        [JsonIgnore]
        public int TotalDurationIncludingChildren =>
            TotalElapsedSeconds + Children.Sum(c => c.TotalDurationIncludingChildren);
        /// <summary>重复目标完成次数</summary>
        public int RecurringCompletionCount { get; set; }

        /// <summary>构造目标实例</summary>
        public Goal()
        {
            Id = Guid.NewGuid().ToString();
            Children = new List<Goal>();
            Timeline = new ObservableCollection<TimelineEntry>();
            Status = GoalStatus.Active;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            TotalElapsedSeconds = 0;
            RecurringCompletionCount = 0;
            StatusHistory = new List<StatusChange>
            {
                new StatusChange { Status = GoalStatus.Active, Timestamp = DateTime.Now }
            };
            AddTimelineEntry("目标已创建");
        }

        /// <summary>构造目标实例</summary>
        public Goal(string title, string description = null)
            : this()
        {
            Title = title;
            Description = description;
        }

        /// <summary>添加子目标</summary>
        public void AddChild(Goal child)
        {
            child.Parent = this;
            Children.Add(child);
            UpdateUpdatedAt();
            ChildrenChanged?.Invoke(this, EventArgs.Empty);
            
            // 给父目标添加时间线记录
            AddTimelineEntry($"添加了子目标\"{child.Title}\"");
            // 通知父目标时长变更
            NotifyDurationChanged();
        }

        /// <summary>移除子目标</summary>
        public void RemoveChild(Goal child)
        {
            child.Parent = null;
            Children.Remove(child);
            UpdateUpdatedAt();
            ChildrenChanged?.Invoke(this, EventArgs.Empty);
            
            // 给父目标添加时间线记录
            AddTimelineEntry($"删除了子目标\"{child.Title}\"");
            // 通知父目标时长变更
            NotifyDurationChanged();
        }

        private void UpdateUpdatedAt()
        {
            UpdatedAt = DateTime.Now;
        }

        /// <summary>添加时间线条目</summary>
        public void AddTimelineEntry(string content, TimelineEntryType type = TimelineEntryType.Operation, DateTime? timestamp = null)
        {
            Timeline.Add(new TimelineEntry(content, type, timestamp));
            UpdateUpdatedAt();
            TimelineEntryAdded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>添加时间线条目（含图片）</summary>
        public void AddTimelineEntry(string content, TimelineEntryType type, DateTime? timestamp, List<string> imagePathList)
        {
            Timeline.Add(new TimelineEntry(content, type, timestamp, imagePathList));
            UpdateUpdatedAt();
            TimelineEntryAdded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>累计时长并触发变更通知（沿父目标链向上传播）</summary>
        public void AddElapsedSeconds(int seconds)
        {
            TotalElapsedSeconds += seconds;
            NotifyDurationChanged();
        }

        /// <summary>通知自身及所有父目标时长已变更</summary>
        public void NotifyDurationChanged()
        {
            DurationChanged?.Invoke(this, EventArgs.Empty);
            Parent?.NotifyDurationChanged();
        }

        /// <summary>添加想法记录</summary>
        public void AddThought(string content, DateTime? timestamp = null)
        {
            AddTimelineEntry(content, TimelineEntryType.Thought, timestamp);
        }

        /// <summary>添加想法记录（含图片）</summary>
        public void AddThought(string content, List<string> imagePathList, DateTime? timestamp = null)
        {
            AddTimelineEntry(content, TimelineEntryType.Thought, timestamp, imagePathList);
        }

        /// <summary>完成目标</summary>
        public void Complete()
        {
            Status = GoalStatus.Completed;
            CompletedAt = DateTime.Now;
            StatusHistory.Add(new StatusChange { Status = GoalStatus.Completed, Timestamp = CompletedAt.Value, IsFinalCompletion = true });
            AddTimelineEntry("目标已完成");

            if (Parent != null)
            {
                Parent.AddTimelineEntry($"子目标\"{Title}\"已完成");
            }
        }

        /// <summary>目标失败</summary>
        public void Fail()
        {
            Status = GoalStatus.Failed;
            FailedAt = DateTime.Now;
            StatusHistory.Add(new StatusChange { Status = GoalStatus.Failed, Timestamp = FailedAt.Value });
            AddTimelineEntry("目标已失败");

            if (Parent != null)
            {
                Parent.AddTimelineEntry($"子目标\"{Title}\"已失败");
            }
        }

        /// <summary>重新激活目标</summary>
        public void Reactivate()
        {
            Status = GoalStatus.Active;
            StatusHistory.Add(new StatusChange { Status = GoalStatus.Active, Timestamp = DateTime.Now });
            AddTimelineEntry("目标已重新开始");

            if (Parent != null)
            {
                Parent.AddTimelineEntry($"子目标\"{Title}\"已重新开始");
            }
        }

        /// <summary>搁置目标</summary>
        public void Pending()
        {
            Status = GoalStatus.Pending;
            PendingAt = DateTime.Now;
            StatusHistory.Add(new StatusChange { Status = GoalStatus.Pending, Timestamp = PendingAt.Value });
            AddTimelineEntry("目标已搁置");

            if (Parent != null)
            {
                Parent.AddTimelineEntry($"子目标\"{Title}\"已搁置");
            }
        }

        /// <summary>递增重复目标完成次数</summary>
        public void IncrementRecurringCompletion()
        {
            RecurringCompletionCount++;
            var now = DateTime.Now;
            StatusHistory.Add(new StatusChange { Status = GoalStatus.Completed, Timestamp = now });
            AddTimelineEntry($"重复目标完成第 {RecurringCompletionCount} 次");
            UpdateUpdatedAt();
        }
    }
}
