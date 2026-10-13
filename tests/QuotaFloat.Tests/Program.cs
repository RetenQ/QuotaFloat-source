using QuotaFloat.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;

var passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);passed++;}
JsonElement J(string text)=>JsonDocument.Parse(text).RootElement.Clone();
var s=QuotaSnapshot.Parse("account","plus",J("""{"rateLimits":{"primary":{"usedPercent":99,"windowDurationMins":300,"resetsAt":2000000000}},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":20,"windowDurationMins":10080,"resetsAt":2000000000},"secondary":{"usedPercent":35,"windowDurationMins":300,"resetsAt":1900000000}},"reserve":{"primary":{"usedPercent":90,"windowDurationMins":300,"resetsAt":1900000000}}}}"""));
Check(s.FiveHour?.Remaining==65 && s.Week?.Remaining==80,"official bucket and duration mapping");
Check(QuotaSnapshot.Parse("a","p",J("{}")).FiveHour==null,"missing quota is unknown");
Check(QuotaWindow.Parse(J("""{"usedPercent":120,"windowDurationMins":300,"resetsAt":1}"""))?.Remaining==0,"remaining clamped");
Check(QuotaWindow.Parse(J("""{"usedPercent":0,"windowDurationMins":300}"""))==null,"missing reset is unknown");
var credits=QuotaSnapshot.Parse("a","p",J("""{"rateLimitResetCredits":{"availableCount":3,"credits":[{"status":"available","expiresAt":2000000000},{"status":"available","expiresAt":2000000000},{"status":"available","expiresAt":2100000000}]}}""")).Resets;
Check(credits is {AvailableCount:3,NearestExpiryCount:2,NearestExpiryAt:2000000000,DetailsComplete:true},"nearest expiring reset credits are counted");
Check(QuotaSnapshot.Parse("a","p",J("""{"rateLimitResetCredits":{"availableCount":3,"credits":null}}""")).Resets is {AvailableCount:3,NearestExpiryCount:null},"count only does not invent expiry details");
Check(QuotaSnapshot.Parse("a","p",J("""{"rateLimitResetCredits":{"availableCount":3,"credits":[{"status":"available","expiresAt":2000000000}]}}""")).Resets is {NearestExpiryCount:1,DetailsComplete:false},"capped reset details are marked incomplete");
Check(QuotaSnapshot.Parse("a","p",J("""{"rateLimitResetCredits":{"availableCount":0,"credits":[]}}""")).Resets is {AvailableCount:0,NearestExpiryCount:0,NearestExpiryAt:null},"zero resets are distinct from unavailable data");
Check(QuotaSnapshot.Parse("a","p",J("{} ")).Resets==null,"missing reset credits are unknown");
var alertNow=DateTimeOffset.FromUnixTimeSeconds(1800000000);
var expiryBoundary=alertNow.AddDays(3).ToUnixTimeSeconds();
Check(QuotaAlerts.IsResetExpiring(new(2,1,expiryBoundary,true),alertNow),"reset expiry warning includes exactly three days");
Check(!QuotaAlerts.IsResetExpiring(new(2,1,expiryBoundary+1,true),alertNow),"reset expiry warning stays off before three-day window");
Check(!QuotaAlerts.IsResetExpiring(new(2,1,alertNow.ToUnixTimeSeconds(),true),alertNow),"expired cached credits are not shown as about to expire");
Check(!QuotaAlerts.IsResetExpiring(null,alertNow) && !QuotaAlerts.IsResetExpiring(new(2,null,null,false),alertNow) && !QuotaAlerts.IsResetExpiring(new(0,0,null,true),alertNow),"unknown and empty reset credits do not trigger warnings");
Check(QuotaAlerts.IsLow(new(80,300,expiryBoundary),alertNow) && !QuotaAlerts.IsLow(new(79.9,300,expiryBoundary),alertNow),"low quota warning uses inclusive twenty-percent threshold");
Check(!QuotaAlerts.IsLow(null,alertNow) && !QuotaAlerts.IsLow(new(95,300,alertNow.ToUnixTimeSeconds()),alertNow),"unknown and expired quota windows do not trigger low warnings");
var dir=Path.Combine(Path.GetTempPath(),"QuotaFloatTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
var path=Path.Combine(dir,"test.db");var store=new UsageStore(path);var stamp=DateTimeOffset.UtcNow;
store.SaveSnapshot(s);store.SaveSnapshot(s with{Account="other",FiveHour=new(99,300,2000000000)});
Check(store.LastSnapshot("account")?.FiveHour?.Remaining==65 && store.LastSnapshot("other")?.FiveHour?.Remaining==1,"quota account isolation");
store.SaveSnapshot(s with{Resets=credits});Check(store.LastSnapshot("account")?.Resets?.NearestExpiryCount==2,"reset credits survive local cache round trip");
store.SaveDays("account",new[]{new DayUsage(DateTime.Today.ToString("yyyy-MM-dd"),1234)});store.SaveDays("other",new[]{new DayUsage(DateTime.Today.ToString("yyyy-MM-dd"),99)});store.SaveDays("account",new[]{new DayUsage(DateTime.Today.ToString("yyyy-MM-dd"),1235)});
Check(store.ReadDashboard("account",7).OfficialDays.Single().Tokens==1235 && store.ReadDashboard("other",7).OfficialDays.Single().Tokens==99,"official history account isolation and update");
Check(store.ReadDashboard("account",30).OfficialDays.Count==1,"missing days not fabricated");Check(store.ReadDashboard("third",30).OfficialDays.Count==0,"unknown account has no official history");
store.SaveSnapshot(s with{CapturedAt=stamp.AddDays(-100)});store.SaveDays("account",new[]{new DayUsage(DateTime.Today.AddDays(-100).ToString("yyyy-MM-dd"),1000)});store.Prune();Check(store.ReadDashboard("account",365).OfficialDays.Count==1 && store.ReadDashboard("account",365).Quotas.Count==1,"90-day retention");
using(var db=new SqliteConnection($"Data Source={path}"))
{
    db.Open();using var cmd=db.CreateCommand();cmd.CommandText="CREATE TABLE events(id TEXT, model TEXT); INSERT INTO events VALUES('old','deepseek'); CREATE TABLE checkpoints(path TEXT); INSERT INTO checkpoints VALUES('old-log');";cmd.ExecuteNonQuery();
}
var upgraded=new UsageStore(path);
using(var db=new SqliteConnection($"Data Source={path}"))
{
    db.Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE name IN ('events','checkpoints')";
    Check(Convert.ToInt64(cmd.ExecuteScalar())==0,"upgrade removes local model events and import checkpoints");
}
Check(upgraded.LastSnapshot("account")?.FiveHour?.Remaining==65 && upgraded.ReadDashboard("account",30).OfficialDays.Single().Tokens==1235,"upgrade preserves official quota and daily history");
var reopened=new UsageStore(path);Check(reopened.LastSnapshot("other")?.FiveHour?.Remaining==1,"official-only migration is idempotent");
QuotaFloat.Settings.DataDirectory=Path.Combine(dir,"monitor");
var fake=new FakeSession();await using(var monitor=new QuotaFloat.MonitorService(new QuotaFloat.Settings(),()=>fake))
{
    await monitor.RefreshAsync(true);var captured=monitor.Snapshot;
    Check(monitor.Verified && captured?.FiveHour?.Remaining==75 && captured.Resets?.AvailableCount==2 && captured.Resets.NearestExpiryCount==1,"monitor successful refresh with reset credits");
    Check(monitor.Store.ReadDashboard(monitor.Account,30).OfficialDays.Count==0 && monitor.HistoryStatus.Contains("不代表零消耗"),"empty official history remains empty and explained");
    fake.Offline=true;await monitor.RefreshAsync();Check(!monitor.Verified && monitor.Snapshot==captured && monitor.Status.Contains("缓存"),"offline preserves timestamped cache");
    fake.Offline=false;fake.LoggedOut=true;await monitor.RefreshAsync();Check(!monitor.Verified && monitor.Snapshot==captured,"expired login preserves marked stale data");
    fake.LoggedOut=false;fake.ApiKey=true;await monitor.RefreshAsync();Check(!monitor.Verified && monitor.Snapshot==captured && monitor.Status.Contains("API Key"),"API key login is clearly labeled");
    fake.ApiKey=false;fake.Account="second";fake.Offline=true;await monitor.RefreshAsync();Check(monitor.Account=="second" && monitor.Snapshot==null,"switch account never shows previous account quota");
    fake.Offline=false;fake.Reset=1;await monitor.RefreshAsync();Check(monitor.Snapshot?.FiveHour?.Remaining==75 && monitor.Snapshot.FiveHour.Reset==1,"expired reset never fabricates 100 percent");
    Check(fake.Disposals==6,"query session disposed on success and failure");
    Check(fake.HomeReads==0,"refresh never accesses local session log location");
}
Console.WriteLine($"ALL {passed} CHECKS PASSED");

sealed class FakeSession : ICodexSession
{
    public bool Offline,LoggedOut,ApiKey;public string Account="first";public long Reset=2000000000;public int Disposals,HomeReads;
    public string CodexHome {get{HomeReads++;throw new InvalidOperationException("Local logs must not be accessed");}}
    public int? ProcessId=>null;
    public Task StartAsync(CancellationToken ct=default)=>Task.CompletedTask;
    public Task<(string Key,string Plan)> AccountAsync(CancellationToken ct=default)=>ApiKey?Task.FromException<(string,string)>(new CodexLoginRequiredException()):LoggedOut?Task.FromException<(string,string)>(new InvalidOperationException("login expired")):Task.FromResult((Account,"plus"));
    public Task<JsonElement> CallAsync(string method,object? parameters=null,CancellationToken ct=default)
    {
        if(Offline)throw new TimeoutException();
        var json=method=="account/usage/read"?"""{"dailyUsageBuckets":[]}""":JsonSerializer.Serialize(new{rateLimits=new{primary=new{usedPercent=25,windowDurationMins=300,resetsAt=Reset},secondary=new{usedPercent=10,windowDurationMins=10080,resetsAt=2000000000}},rateLimitResetCredits=new{availableCount=2,credits=new[]{new{status="available",expiresAt=2000000000L},new{status="available",expiresAt=2100000000L}}}});
        return Task.FromResult(JsonDocument.Parse(json).RootElement.Clone());
    }
    public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
}

