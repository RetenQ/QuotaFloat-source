using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace QuotaFloat.Core;

public sealed class UsageStore
{
    private readonly string connectionString;
    private readonly object gate=new();
    public UsageStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString=new SqliteConnectionStringBuilder { DataSource=path, Mode=SqliteOpenMode.ReadWriteCreate }.ToString();
        using var db=Open();
        Exec(db,"PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS quotas(account TEXT NOT NULL, time INTEGER NOT NULL, json TEXT NOT NULL, PRIMARY KEY(account,time)); CREATE TABLE IF NOT EXISTS daily(account TEXT NOT NULL, day TEXT NOT NULL, tokens INTEGER NOT NULL, PRIMARY KEY(account,day));");
        // Upgrade to official-only mode. Remove the old imported logs while preserving official data.
        using var check=db.CreateCommand();
        check.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('events','checkpoints')";
        if(Convert.ToInt64(check.ExecuteScalar())>0)
        {
            Exec(db,"PRAGMA secure_delete=ON; BEGIN IMMEDIATE; DROP TABLE IF EXISTS events; DROP TABLE IF EXISTS checkpoints; COMMIT; VACUUM; PRAGMA wal_checkpoint(TRUNCATE);");
        }
    }
    private SqliteConnection Open() { var db=new SqliteConnection(connectionString); db.Open(); return db; }
    private static void Exec(SqliteConnection db,string sql, params (string,object?)[] values)
    { using var cmd=db.CreateCommand(); cmd.CommandText=sql; foreach(var (k,v) in values) cmd.Parameters.AddWithValue(k,v ?? DBNull.Value); cmd.ExecuteNonQuery(); }
    public void SaveSnapshot(QuotaSnapshot snapshot)
    {
        lock(gate) { using var db=Open(); Exec(db,"INSERT OR REPLACE INTO quotas VALUES($a,$t,$j)",("$a",snapshot.Account),("$t",snapshot.CapturedAt.ToUnixTimeSeconds()),("$j",JsonSerializer.Serialize(snapshot))); }
    }
    public QuotaSnapshot? LastSnapshot(string account)
    {
        lock(gate) { using var db=Open(); using var cmd=db.CreateCommand(); cmd.CommandText="SELECT json FROM quotas WHERE account=$a ORDER BY time DESC LIMIT 1"; cmd.Parameters.AddWithValue("$a",account); return cmd.ExecuteScalar() is string j ? JsonSerializer.Deserialize<QuotaSnapshot>(j) : null; }
    }
    public void SaveDays(string account, IEnumerable<DayUsage> days)
    {
        lock(gate) { using var db=Open(); using var tx=db.BeginTransaction(); foreach(var d in days) Exec(db,"INSERT OR REPLACE INTO daily VALUES($a,$d,$t)",("$a",account),("$d",d.Day),("$t",d.Tokens)); tx.Commit(); }
    }
    public DashboardData ReadDashboard(string? account,int days)
    {
        var since=DateTime.Today.AddDays(1-days).ToString("yyyy-MM-dd");
        lock(gate)
        {
            using var db=Open(); var official=new List<DayUsage>(); var quota=new List<QuotaPoint>();
            using(var cmd=db.CreateCommand()) { cmd.CommandText="SELECT day,tokens FROM daily WHERE account=$a AND day >= $d ORDER BY day";cmd.Parameters.AddWithValue("$a",account ?? "");cmd.Parameters.AddWithValue("$d",since);using var r=cmd.ExecuteReader();while(r.Read())official.Add(new(r.GetString(0),r.GetInt64(1))); }
            using(var cmd=db.CreateCommand()) { cmd.CommandText="SELECT json FROM quotas WHERE account=$a AND time >= $t ORDER BY time";cmd.Parameters.AddWithValue("$a",account ?? "");cmd.Parameters.AddWithValue("$t",new DateTimeOffset(DateTime.Today.AddDays(1-days)).ToUnixTimeSeconds());using var r=cmd.ExecuteReader();while(r.Read()) { var s=JsonSerializer.Deserialize<QuotaSnapshot>(r.GetString(0))!;quota.Add(new(s.CapturedAt.ToUnixTimeSeconds(),s.FiveHour?.Remaining,s.Week?.Remaining,s.FiveHour?.Reset,s.Week?.Reset)); } }
            return new(official,quota);
        }
    }
    public void Prune()
    {
        lock(gate) { using var db=Open();var day=DateTime.Today.AddDays(-89).ToString("yyyy-MM-dd");Exec(db,"DELETE FROM daily WHERE day < $d; DELETE FROM quotas WHERE time < $t",("$d",day),("$t",DateTimeOffset.UtcNow.AddDays(-90).ToUnixTimeSeconds())); }
    }
}
