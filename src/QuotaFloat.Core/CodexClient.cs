using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;

namespace QuotaFloat.Core;

public interface ICodexSession : IAsyncDisposable
{
    string CodexHome {get;}
    int? ProcessId {get;}
    Task StartAsync(CancellationToken ct=default);
    Task<(string Key,string Plan)> AccountAsync(CancellationToken ct=default);
    Task<JsonElement> CallAsync(string method,object? parameters=null,CancellationToken ct=default);
}
public sealed class CodexLoginRequiredException : InvalidOperationException
{
    public CodexLoginRequiredException() : base("请先在 Codex 中使用 ChatGPT 账号登录。") { }
}
public sealed class CodexClient : ICodexSession
{
    private Process? process;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly SemaphoreSlim writeLock = new(1,1);
    private int nextId;
    private Task? reader;
    public string CodexHome { get; private set; } = Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    public static string? FindExecutable()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(root))
        {
            var found = Directory.EnumerateFiles(root, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(found != null) return found;
        }
        foreach(var part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        { var path = Path.Combine(part.Trim('"'), "codex.exe"); if(File.Exists(path)) return path; }
        return null;
    }
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (process is { HasExited:false }) return;
        var path = FindExecutable() ?? throw new InvalidOperationException("未找到 Codex，请先安装并登录 Codex 桌面应用。");
        var info = new ProcessStartInfo(path) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
        info.ArgumentList.Add("app-server"); info.ArgumentList.Add("--stdio");
        info.Environment["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        process = Process.Start(info) ?? throw new IOException("Codex 数据服务未能启动。");
        process.ErrorDataReceived += (_,_) => {}; process.BeginErrorReadLine();
        reader = ReadAsync(process);
        var initialized = await CallAsync("initialize", new { clientInfo = new { name="quota_float", version="1.0.0" }, capabilities = new { experimentalApi=true } }, ct);
        CodexHome = initialized.Get("codexHome").Str() ?? CodexHome;
        await SendAsync(new { method="initialized" }, ct);
    }
    private async Task ReadAsync(Process p)
    {
        try
        {
            while(await p.StandardOutput.ReadLineAsync() is { } line)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line); var root=doc.RootElement;
                    if(root.Get("id").Num() is long id && pending.TryRemove((int)id, out var completion))
                    {
                        if(root.Get("error").ValueKind==JsonValueKind.Object)
                            completion.TrySetException(new InvalidOperationException("额度服务暂不可用，请检查网络与 Codex 登录状态。"));
                        else completion.TrySetResult(root.Get("result").Clone());
                    }
                }
                catch(JsonException) { }
            }
        }
        catch(IOException) { }
        finally { foreach(var item in pending.Values) item.TrySetException(new IOException("Codex 数据连接已断开。")); pending.Clear(); }
    }
    private async Task SendAsync(object message, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try { await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), ct); await process.StandardInput.FlushAsync(ct); }
        finally { writeLock.Release(); }
    }
    public async Task<JsonElement> CallAsync(string method, object? parameters=null, CancellationToken ct=default)
    {
        var id=Interlocked.Increment(ref nextId); var tcs=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously); pending[id]=tcs;
        try { await SendAsync(new { id, method, @params=parameters },ct); return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(35),ct); }
        finally { pending.TryRemove(id,out _); }
    }
    public async Task<(string Key,string Plan)> AccountAsync(CancellationToken ct=default)
    {
        var r=await CallAsync("account/read",new { refreshToken=false },ct); var a=r.Get("account");
        if(a.Get("type").Str() != "chatgpt") throw new CodexLoginRequiredException();
        var email=a.Get("email").Str() ?? throw new InvalidOperationException("无法确认当前账号，暂不加载账号历史。");
        // Only read the non-secret account identifier; never copy or log tokens.
        var accountId="";
        var auth=Path.Combine(CodexHome,"auth.json");
        if(File.Exists(auth))
        {
            using var stream=File.OpenRead(auth); using var doc=JsonDocument.Parse(stream);
            accountId=doc.RootElement.Get("tokens").Get("account_id").Str() ?? "";
        }
        return (Json.Hash(email.ToLowerInvariant()+"|"+accountId),a.Get("planType").Str() ?? "ChatGPT");
    }
    public int? ProcessId => process is { HasExited:false } ? process.Id : null;
    public async ValueTask DisposeAsync()
    {
        var p=process; process=null; if(p==null)return;
        try
        {
            if(!p.HasExited) { p.StandardInput.Close(); try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); } catch(TimeoutException) { p.Kill(true); await p.WaitForExitAsync(); } }
            if(reader!=null) await reader;
        }
        catch(InvalidOperationException) { }
        finally { p.Dispose(); }
    }
}
