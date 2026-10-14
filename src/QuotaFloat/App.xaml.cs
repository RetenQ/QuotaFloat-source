using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QuotaFloat;
public partial class App : Application
{
    public WidgetWindow Widget {get;private set;}=null!;
    public bool Exiting {get;private set;}
    private MonitorService service=null!;
    private DetailsWindow? details;
    private SettingsWindow? settingsWindow;
    private System.Windows.Forms.NotifyIcon? tray;
    private Mutex? mutex;
    private EventWaitHandle? showEvent;
    private RegisteredWaitHandle? showRegistration;
    private bool ownsMutex;
    public bool DetailsVisible=>details?.IsVisible==true;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Static, small native views need no GPU device or its resident driver allocations.
        System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        var verify=e.Args.Length==2 && e.Args[0]=="--verify";
        try
        {
            if(verify)Settings.DataDirectory=Path.Combine(Path.GetFullPath(e.Args[1]),"data");
            mutex=new Mutex(true,verify?"Local\\QuotaFloat.Verification":"Local\\QuotaFloat.Main",out ownsMutex);
            if(!ownsMutex){try{using var signal=EventWaitHandle.OpenExisting("Local\\QuotaFloat.Show");signal.Set();}catch(WaitHandleCannotBeOpenedException){}Shutdown();return;}
            if(!verify){showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\QuotaFloat.Show");showRegistration=ThreadPool.RegisterWaitForSingleObject(showEvent,(_,_)=>Dispatcher.BeginInvoke(()=>Widget.Restore()),null,Timeout.Infinite,false);}
            Ui.ApplyTheme();service=new MonitorService(Settings.Load());Widget=new WidgetWindow(service,this);MainWindow=Widget;Widget.Show();CreateTray();
            SystemEvents.UserPreferenceChanged+=ThemeChanged;SystemEvents.PowerModeChanged+=PowerChanged;SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            DispatcherUnhandledException+=(_,args)=>{args.Handled=true;MessageBox.Show("操作未完成，请重试。若问题持续，可退出后重新打开额度浮窗。","额度浮窗");};
            if(verify){await VerifyAsync(Path.GetFullPath(e.Args[1]));await ExitCoreAsync();}
            else
            {
                service.Start();
                if(Settings.AutoStart)
                {
                    try { QuotaFloat.Startup.StartupRegistration.StartMonitor(Path.Combine(AppContext.BaseDirectory,"QuotaFloat.Launcher.exe")); }
                    catch(Exception error) when(error is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
                    { MessageBox.Show("启动联动未能注册，请在设置中重新启用启动开关。","额度浮窗"); }
                }
            }
        }
        catch(Exception ex)
        {
            if(verify){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),ex.ToString());}
            else MessageBox.Show("额度浮窗启动失败："+ex.Message,"额度浮窗");
            await ExitCoreAsync();
        }
    }
    private void CreateTray()
    {
        using var iconStream=GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;
        tray=new System.Windows.Forms.NotifyIcon{Icon=new System.Drawing.Icon(iconStream),Text="额度浮窗 · Codex / Work",Visible=true};
        tray.DoubleClick+=(_,_)=>Widget.Restore();var menu=new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示悬浮窗",null,(_,_)=>Widget.Restore());menu.Items.Add("详细信息",null,(_,_)=>ShowDetails());menu.Items.Add("立即刷新",null,(_,_)=>{_ = service.RefreshAsync();});menu.Items.Add("设置",null,(_,_)=>ShowSettings());menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());menu.Items.Add("退出",null,(_,_)=>ExitAsync());tray.ContextMenuStrip=menu;
    }
    public void ShowDetails()
    {if(details==null){details=new DetailsWindow(service,this);details.Closed+=(_,_)=>{details=null;service.Visible=Widget.IsVisible;};details.Show();}else{details.WindowState=WindowState.Normal;details.Activate();}service.Visible=true;_ = service.RefreshAsync(true);}
    public void ShowSettings()
    {if(settingsWindow==null){settingsWindow=new SettingsWindow(this,service);settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();}else settingsWindow.Activate();}
    private void ThemeChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{Ui.ApplyTheme();Widget.Build();details?.Render();});
    private void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(()=>Widget.ClampPosition());
    private void PowerChanged(object sender,PowerModeChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{if(e.Mode==PowerModes.Suspend)service.Suspended=true;else if(e.Mode==PowerModes.Resume){service.Suspended=false;_ = service.RefreshAsync();}});
    public async void ExitAsync()=>await ExitCoreAsync();
    private async Task ExitCoreAsync()
    {
        if(Exiting)return;Exiting=true;
        SystemEvents.UserPreferenceChanged-=ThemeChanged;SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.DisplaySettingsChanged-=DisplayChanged;
        if(service!=null){service.Settings.Save();await service.DisposeAsync();}
        var icon=tray?.Icon;tray?.Dispose();icon?.Dispose();details?.Close();settingsWindow?.Close();Widget?.Close();showRegistration?.Unregister(null);showEvent?.Dispose();if(ownsMutex)mutex?.ReleaseMutex();mutex?.Dispose();Shutdown();
    }
    private async Task VerifyAsync(string directory)
    {
        Directory.CreateDirectory(directory);var measurements=new List<object>();
        await service.RefreshAsync(true);
        Ui.ApplyTheme(false);Widget.Build();await Task.Delay(400);Capture(Widget,Path.Combine(directory,"widget-light.png"));
        Ui.ApplyTheme(true);Widget.Build();await Task.Delay(400);Capture(Widget,Path.Combine(directory,"widget-dark.png"));
        measurements.Add(await MeasureAsync("widget"));
        Widget.ToggleMini();await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-mini.png"));Widget.ToggleMini();
        // Simulated previews exercise warning layouts without changing saved account data.
        var previewNow=DateTimeOffset.UtcNow;
        var preview=new QuotaFloat.Core.QuotaSnapshot("preview","preview",previewNow,new(85,300,previewNow.AddHours(2).ToUnixTimeSeconds()),new(92,10080,previewNow.AddDays(6).ToUnixTimeSeconds()),new(3,2,previewNow.AddDays(2).ToUnixTimeSeconds(),true));
        Ui.ApplyTheme(false);Widget.Build();Widget.PreviewSnapshot(preview);await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-alerts-light.png"));
        Ui.ApplyTheme(true);Widget.Build();Widget.PreviewSnapshot(preview);await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-alerts-dark.png"));
        Widget.ToggleMini();Widget.PreviewSnapshot(preview);await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-mini-alerts-dark.png"));
        Ui.ApplyTheme(false);Widget.Build();Widget.PreviewSnapshot(preview);await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-mini-alerts-light.png"));
        Widget.PreviewSnapshot(preview with{FiveHour=new(0,300,previewNow.AddHours(2).ToUnixTimeSeconds()),Week=new(0,10080,previewNow.AddDays(6).ToUnixTimeSeconds()),Resets=new(12,null,null,false)});await Task.Delay(200);Capture(Widget,Path.Combine(directory,"widget-mini-full.png"));
        Widget.ToggleMini();Ui.ApplyTheme(true);Widget.Build();
        details=new DetailsWindow(service,this);details.Show();await Task.Delay(400);Capture(details,Path.Combine(directory,"details-dark.png"));
        Ui.ApplyTheme(false);details.Render();await Task.Delay(400);Capture(details,Path.Combine(directory,"details-light.png"));
        details.ScrollToBottom();await Task.Delay(250);Capture(details,Path.Combine(directory,"details-official-history.png"));
        measurements.Add(await MeasureAsync("details"));
        details.SetTabForVerification("普通聊天");await Task.Delay(200);Capture(details,Path.Combine(directory,"chatgpt-unavailable.png"));details.Close();details=null;
        Widget.ToTray();measurements.Add(await MeasureAsync("tray"));
        var data=service.Store.ReadDashboard(service.Account,30);
        var childExited=service.DataProcessIds.All(id=>{try{using var p=Process.GetProcessById(id);return p.HasExited;}catch(ArgumentException){return true;}});
        var report=new { verifiedAt=DateTimeOffset.Now, officialOnly=true, liveQuotaConnected=service.Verified, fiveHourRemaining=service.Snapshot?.FiveHour?.Remaining, weeklyRemaining=service.Snapshot?.Week?.Remaining, availableResets=service.Snapshot?.Resets?.AvailableCount, alertPreviewIsSimulated=true, historyStatus=service.HistoryStatus, officialDays=data.OfficialDays.Count, dataProcessesExited=childExited, sampledRefreshPeakMB=service.PeakRefreshWorkingSetMB, measurements, notes=new[]{"仅查询官方额度和官方历史接口，不读取本机会话日志，不向模型发起推理请求。","UI 截图覆盖浅色、深色、迷你条、详情及普通聊天无数据状态。","文件名包含 alerts 的截图及 widget-mini-full.png 使用模拟数据检查提醒和迷你布局，未写入账号数据库。","多显示器、实际锁屏/休眠、登录过期与关闭主 Codex 窗口需人工实机复核。","额度接入进程按查询创建并关闭；空闲状态仅保留本工具。"} };
        File.WriteAllText(Path.Combine(directory,"verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static async Task<object> MeasureAsync(string state)
    {
        using var p=Process.GetCurrentProcess();GC.Collect();GC.WaitForPendingFinalizers();await Task.Delay(1500);p.Refresh();var start=p.TotalProcessorTime;var clock=Stopwatch.StartNew();await Task.Delay(12000);p.Refresh();
        return new{state,seconds=clock.Elapsed.TotalSeconds,logicalProcessors=Environment.ProcessorCount,cpuPercent=(p.TotalProcessorTime-start).TotalMilliseconds/clock.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100,workingSetMB=p.WorkingSet64/1048576d,privateMB=p.PrivateMemorySize64/1048576d};
    }
    private static void Capture(FrameworkElement element,string path)
    {
        element.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(element);var bmp=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(element.ActualHeight*dpi.DpiScaleY),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);bmp.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);
    }
}
