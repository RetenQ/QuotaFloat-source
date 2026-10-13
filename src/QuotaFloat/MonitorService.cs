using QuotaFloat.Core;
using System.Windows.Threading;
using System.Text.Json;

namespace QuotaFloat;
public sealed class MonitorService : IAsyncDisposable
{
    public Settings Settings {get;}
    public UsageStore Store {get;}
    public QuotaSnapshot? Snapshot {get;private set;}
    public string? Account {get;private set;}
    public string Status {get;private set;}="正在连接 Codex…";
    public string HistoryStatus {get;private set;}="尚未同步官方历史";
    public bool Verified {get;private set;}
    public bool Busy {get;private set;}
    public double PeakRefreshWorkingSetMB {get;private set;}
    public List<int> DataProcessIds {get;}=[];
    public bool Visible {get;set;}=true;
    public bool Suspended {get;set;}
    public event Action? Changed;
    private readonly CancellationTokenSource stop=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMinutes(1)};
    private DateTimeOffset lastAttempt=DateTimeOffset.MinValue,lastHistory=DateTimeOffset.MinValue;
    private Task? refreshTask;
    private bool expiredAttempted;
    private readonly Func<ICodexSession> sessionFactory;
    public MonitorService(Settings settings,Func<ICodexSession>? sessionFactory=null)
    {
        this.sessionFactory=sessionFactory ?? (()=>new CodexClient());
        Settings=settings;Store=new UsageStore(Path.Combine(Settings.DataDirectory,"usage.db"));
        if(settings.LastAccount!=null){Snapshot=Store.LastSnapshot(settings.LastAccount);if(Snapshot!=null)Status="上次账号缓存 · 正在确认登录";}
        timer.Tick+=async(_,_)=>{Changed?.Invoke();if(Suspended)return;var expired=Snapshot?.FiveHour is { } f && f.Reset<=DateTimeOffset.UtcNow.ToUnixTimeSeconds() || Snapshot?.Week is { } w && w.Reset<=DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(DateTimeOffset.UtcNow-lastAttempt>TimeSpan.FromMinutes(Visible?2:10) || expired && !expiredAttempted){expiredAttempted=expired;await RefreshAsync();}};
    }
    public void Start(){timer.Start();_ = RefreshAsync();}
    public Task RefreshAsync(bool history=false)
    { if(Busy)return refreshTask ?? Task.CompletedTask;refreshTask=RefreshCoreAsync(history);return refreshTask; }
    private async Task RefreshCoreAsync(bool forceHistory)
    {
        Busy=true;lastAttempt=DateTimeOffset.UtcNow;Status="正在更新…";Changed?.Invoke();
        try
        {
            await using var client=sessionFactory();await client.StartAsync(stop.Token);
            if(client.ProcessId is int pid)DataProcessIds.Add(pid);
            SampleMemory(client);
            var account=await client.AccountAsync(stop.Token);
            if(Account!=account.Key)
            {Account=account.Key;Snapshot=Store.LastSnapshot(Account);lastHistory=DateTimeOffset.MinValue;Settings.LastAccount=Account;Settings.Save();HistoryStatus="尚未同步官方历史";Changed?.Invoke();}
            Verified=true;
            var result=await client.CallAsync("account/rateLimits/read",ct:stop.Token);
            SampleMemory(client);
            Snapshot=QuotaSnapshot.Parse(Account,account.Plan,result);Store.SaveSnapshot(Snapshot);
            Status="已更新 "+Snapshot.CapturedAt.ToLocalTime().ToString("HH:mm");
            if(Snapshot.FiveHour?.Reset>DateTimeOffset.UtcNow.ToUnixTimeSeconds() && Snapshot.Week?.Reset>DateTimeOffset.UtcNow.ToUnixTimeSeconds())expiredAttempted=false;
            Changed?.Invoke();
            if(forceHistory || DateTimeOffset.UtcNow-lastHistory>TimeSpan.FromMinutes(30))
            {
                try
                {
                    var usage=await client.CallAsync("account/usage/read",ct:stop.Token);var buckets=usage.Get("dailyUsageBuckets");var days=new List<DayUsage>();
                    SampleMemory(client);
                    if(buckets.ValueKind==JsonValueKind.Array)foreach(var day in buckets.EnumerateArray())
                        if(DateOnly.TryParse(day.Get("startDate").Str(),out var date) && day.Get("tokens").Num() is >=0 and var tokens) days.Add(new(date.ToString("yyyy-MM-dd"),tokens));
                    Store.SaveDays(Account,days);lastHistory=DateTimeOffset.UtcNow;HistoryStatus=days.Count==0?"官方接口暂无每日用量记录，不代表零消耗":"官方历史已同步 · 日期以服务返回为准";
                }
                catch(Exception e) when(e is not OperationCanceledException){HistoryStatus="官方历史暂不可用 · 保留已有记录";}
            }
            Store.Prune();
        }
        catch(OperationCanceledException){Status="已停止";}
        catch(CodexLoginRequiredException)
        {Verified=false;Status=Snapshot==null?"需在 Codex 登录 ChatGPT 账号":"Codex 当前为 API Key 登录 · 显示上次缓存";}
        catch(Exception e) when(e is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException or JsonException)
        {Verified=false;Status=Snapshot==null?"连接失败 · 请检查 Codex 登录或网络":"连接失败 · 显示上次缓存";}
        finally{Busy=false;Changed?.Invoke();}
    }
    private void SampleMemory(ICodexSession client)
    {
        using var own=System.Diagnostics.Process.GetCurrentProcess();long memory=own.WorkingSet64;
        try{if(client.ProcessId is int pid){using var child=System.Diagnostics.Process.GetProcessById(pid);memory+=child.WorkingSet64;}}catch(ArgumentException){}
        PeakRefreshWorkingSetMB=Math.Max(PeakRefreshWorkingSetMB,memory/1048576d);
    }
    public async ValueTask DisposeAsync(){timer.Stop();stop.Cancel();if(refreshTask!=null)await refreshTask;stop.Dispose();}
}
