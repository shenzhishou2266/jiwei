using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// ============================================================
// Sessions.json 恢复工具
// 从 goals.json 的时间线条目中提取计时相关记录，
// 重建 sessions.json 文件。
//
// 用法：
//   dotnet run -- [存储目录路径]
//   如果不指定路径，默认使用 %APPDATA%\积微
// ============================================================

string storagePath;

if (args.Length > 0)
{
    storagePath = args[0];
}
else
{
    storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "积微"
    );
}

Console.WriteLine($"存储目录: {storagePath}");
Console.WriteLine();

string goalsPath = Path.Combine(storagePath, "goals.json");
string sessionsPath = Path.Combine(storagePath, "sessions.json");

if (!File.Exists(goalsPath))
{
    Console.WriteLine($"[错误] 找不到 goals.json: {goalsPath}");
    return 1;
}

// 备份现有 sessions.json（如果存在）
if (File.Exists(sessionsPath))
{
    string backupPath = sessionsPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
    File.Copy(sessionsPath, backupPath);
    Console.WriteLine($"[备份] 已备份现有 sessions.json -> {Path.GetFileName(backupPath)}");
    Console.WriteLine();
}

// 读取 goals.json
Console.WriteLine("[读取] 正在加载 goals.json...");
string goalsJson = File.ReadAllText(goalsPath);
var readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var goalsWrapper = JsonSerializer.Deserialize<ReferenceList<GoalData>>(goalsJson, readOptions);
if (goalsWrapper?.Values == null)
{
    Console.WriteLine("[错误] goals.json 解析失败");
    return 1;
}
var goals = goalsWrapper.Values;
Console.WriteLine($"[读取] 共加载 {goals.Count} 个根目标");
Console.WriteLine();

// 收集所有目标（递归）
var allGoals = new List<GoalData>();
void CollectGoals(GoalData goal)
{
    allGoals.Add(goal);
    if (goal.Children?.Values != null)
    {
        foreach (var child in goal.Children.Values)
            CollectGoals(child);
    }
}
foreach (var g in goals)
    CollectGoals(g);

Console.WriteLine($"[统计] 包含子目标共 {allGoals.Count} 个目标");

// 解析时间线条目，重建 SessionRecord
var recoveredSessions = new List<SessionRecordData>();
int timelineEntryCount = 0;
int matchedCount = 0;
int skippedCount = 0;

foreach (var goal in allGoals)
{
    if (goal.Timeline?.Values == null) continue;

    foreach (var entry in goal.Timeline.Values)
    {
        timelineEntryCount++;
        if (string.IsNullOrEmpty(entry.Content)) continue;

        var session = TryParseTimerEntry(entry, goal);
        if (session != null)
        {
            recoveredSessions.Add(session);
            matchedCount++;
        }
        else
        {
            skippedCount++;
        }
    }
}

Console.WriteLine($"[扫描] 共扫描 {timelineEntryCount} 条时间线记录");
Console.WriteLine($"[匹配] 识别到 {matchedCount} 条计时相关记录");
Console.WriteLine($"[跳过] {skippedCount} 条非计时记录");
Console.WriteLine();

// 按时间排序
recoveredSessions.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

// 写入 sessions.json
Console.WriteLine("[写入] 正在生成 sessions.json...");
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
};
string sessionsJson = JsonSerializer.Serialize(recoveredSessions, options);
File.WriteAllText(sessionsPath, sessionsJson);

Console.WriteLine($"[完成] 已恢复 {recoveredSessions.Count} 条会话记录 -> {sessionsPath}");
Console.WriteLine();

// 输出摘要
if (recoveredSessions.Count > 0)
{
    Console.WriteLine("=== 恢复摘要 ===");
    Console.WriteLine($"总记录数: {recoveredSessions.Count}");
    Console.WriteLine($"时间范围: {recoveredSessions[0].StartTime:yyyy-MM-dd HH:mm} ~ {recoveredSessions[^1].StartTime:yyyy-MM-dd HH:mm}");

    var pomodoroCount = recoveredSessions.Count(s => s.TimerType == 0); // PomodoroFocus
    var breakCount = recoveredSessions.Count(s => s.TimerType == 1);     // PomodoroBreak
    var stopwatchCount = recoveredSessions.Count(s => s.TimerType == 2); // Stopwatch
    var countdownCount = recoveredSessions.Count(s => s.TimerType == 3); // Countdown
    int totalSeconds = recoveredSessions.Sum(s => s.DurationSeconds);

    Console.WriteLine($"  番茄专注: {pomodoroCount} 次");
    Console.WriteLine($"  秒表记录: {stopwatchCount} 次");
    Console.WriteLine($"  倒计时记录: {countdownCount} 次（含提前停止的番茄钟）");
    Console.WriteLine($"  总时长: {totalSeconds / 3600}时{(totalSeconds % 3600) / 60}分{totalSeconds % 60}秒");
}

return 0;


// ---- 解析逻辑 ----

SessionRecordData? TryParseTimerEntry(TimelineEntryData entry, GoalData goal)
{
    string content = entry.Content.Trim();

    // 模式1: 完成了一个工作番茄钟，时长：XX分钟
    // 注意：休息番茄钟不生成 SessionRecord，与原始代码行为一致
    var match1 = Regex.Match(content, @"^完成了一个工作番茄钟，时长：(\d+)分钟$");
    if (match1.Success)
    {
        int durationMinutes = int.Parse(match1.Groups[1].Value);
        int durationSeconds = durationMinutes * 60;

        return new SessionRecordData
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = entry.Timestamp.AddSeconds(-durationSeconds),
            EndTime = entry.Timestamp,
            DurationSeconds = durationSeconds,
            Type = 0,     // Focus
            TimerType = 0, // PomodoroFocus
            GoalId = goal.Id,
            GoalTitle = goal.Title
        };
    }

    // 模式2: 停止了一个工作番茄钟，实际完成时长：X分Y秒
    var match2 = Regex.Match(content, @"^停止了一个工作番茄钟，实际完成时长：(\d+)分(\d+)秒$");
    if (match2.Success)
    {
        int minutes = int.Parse(match2.Groups[1].Value);
        int seconds = int.Parse(match2.Groups[2].Value);
        int durationSeconds = minutes * 60 + seconds;

        if (durationSeconds == 0) return null;

        return new SessionRecordData
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = entry.Timestamp.AddSeconds(-durationSeconds),
            EndTime = entry.Timestamp,
            DurationSeconds = durationSeconds,
            Type = 0,     // Focus
            TimerType = 3, // Countdown
            GoalId = goal.Id,
            GoalTitle = goal.Title
        };
    }

    // 跳过停止休息番茄钟（不生成SessionRecord）
    if (Regex.IsMatch(content, @"^停止了一个休息番茄钟"))
        return null;

    // 模式3: 完成了一次倒计时，时长：XXX
    var match3 = Regex.Match(content, @"^完成了一次倒计时，时长：(.+)$");
    if (match3.Success)
    {
        int durationSeconds = ParseDuration(match3.Groups[1].Value);
        if (durationSeconds == 0) return null;

        return new SessionRecordData
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = entry.Timestamp.AddSeconds(-durationSeconds),
            EndTime = entry.Timestamp,
            DurationSeconds = durationSeconds,
            Type = 0,     // Focus
            TimerType = 3, // Countdown
            GoalId = goal.Id,
            GoalTitle = goal.Title
        };
    }

    // 模式4: 计时器记录，时长：XXX
    var match4 = Regex.Match(content, @"^计时器记录，时长：(.+)$");
    if (match4.Success)
    {
        int durationSeconds = ParseDuration(match4.Groups[1].Value);
        if (durationSeconds == 0) return null;

        return new SessionRecordData
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = entry.Timestamp.AddSeconds(-durationSeconds),
            EndTime = entry.Timestamp,
            DurationSeconds = durationSeconds,
            Type = 0,     // Focus
            TimerType = 2, // Stopwatch
            GoalId = goal.Id,
            GoalTitle = goal.Title
        };
    }

    // 模式5: 倒计时记录，时长：XXX
    var match5 = Regex.Match(content, @"^倒计时记录，时长：(.+)$");
    if (match5.Success)
    {
        int durationSeconds = ParseDuration(match5.Groups[1].Value);
        if (durationSeconds == 0) return null;

        return new SessionRecordData
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = entry.Timestamp.AddSeconds(-durationSeconds),
            EndTime = entry.Timestamp,
            DurationSeconds = durationSeconds,
            Type = 0,     // Focus
            TimerType = 3, // Countdown
            GoalId = goal.Id,
            GoalTitle = goal.Title
        };
    }

    return null;
}

/// <summary>
/// 解析 FormatDuration 格式的时长文本
/// 格式: "{D}天{H}小时{M}分钟{S}秒"
/// </summary>
int ParseDuration(string text)
{
    int days = 0, hours = 0, minutes = 0, seconds = 0;

    var dayMatch = Regex.Match(text, @"(\d+)天");
    if (dayMatch.Success) days = int.Parse(dayMatch.Groups[1].Value);

    var hourMatch = Regex.Match(text, @"(\d+)小时");
    if (hourMatch.Success) hours = int.Parse(hourMatch.Groups[1].Value);

    var minMatch = Regex.Match(text, @"(\d+)分钟");
    if (minMatch.Success) minutes = int.Parse(minMatch.Groups[1].Value);

    var secMatch = Regex.Match(text, @"(\d+)秒");
    if (secMatch.Success) seconds = int.Parse(secMatch.Groups[1].Value);

    return days * 86400 + hours * 3600 + minutes * 60 + seconds;
}


// ---- JSON 数据模型（最小化定义，避免依赖主项目） ----

/// <summary>处理 ReferenceHandler.Preserve 序列化的 $id/$values 包装</summary>
public class ReferenceList<T>
{
    [JsonPropertyName("$id")]
    public string? Id { get; set; }
    [JsonPropertyName("$values")]
    public List<T>? Values { get; set; }
}

public class GoalData
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public ReferenceList<GoalData>? Children { get; set; }
    public ReferenceList<TimelineEntryData>? Timeline { get; set; }
}

public class TimelineEntryData
{
    public DateTime Timestamp { get; set; }
    public string Content { get; set; } = "";
}

public class SessionRecordData
{
    public string Id { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public int Type { get; set; }     // SessionType: Focus=0, Break=1
    public int TimerType { get; set; } // TimerSessionType: PomodoroFocus=0, PomodoroBreak=1, Stopwatch=2, Countdown=3
    public string? GoalId { get; set; }
    public string? GoalTitle { get; set; }
}