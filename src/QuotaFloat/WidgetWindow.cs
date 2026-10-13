using QuotaFloat.Core;
using System.Windows.Input;
using System.Windows.Interop;

namespace QuotaFloat;
public sealed class WidgetWindow : Window
{
    private readonly MonitorService service;
    private readonly App app;
    private TextBlock status=null!,fiveValue=null!,weekValue=null!,fiveTime=null!,weekTime=null!,resetValue=null!,resetExpiry=null!;
    private TextBlock fiveWarning=null!,weekWarning=null!,resetWarning=null!;
    private Border fiveFill=null!,weekFill=null!;
    public WidgetWindow(MonitorService service,App app)
    {
        this.service=service;this.app=app;Title="额度浮窗";Width=280;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=service.Settings.Topmost;
        Build();service.Changed+=Update;
        SourceInitialized+=(_,_)=>RestorePosition();
        LocationChanged+=(_,_)=>{if(IsLoaded){service.Settings.Left=Left;service.Settings.Top=Top;}};
        MouseLeftButtonDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Left){try{DragMove();service.Settings.Save();}catch(InvalidOperationException){}}};
        MouseDoubleClick+=(_,_)=>app.ShowDetails();
        Closing+=(_,e)=>{if(!app.Exiting){e.Cancel=true;ToTray();}};
        Closed+=(_,_)=>service.Changed-=Update;
    }
    public void Build()
    {
        Height=service.Settings.Mini?48:187;Width=service.Settings.Mini?340:280;ToolTip=null;
        var stack=new StackPanel();var outer=Ui.Card(stack,12);Content=outer;
        var menu=new ContextMenu();foreach(var (title,action) in new(string,Action)[]{("详细信息",()=>app.ShowDetails()),("展开／收起",ToggleMini),("设置",()=>app.ShowSettings()),("收进托盘",ToTray),("退出",()=>app.ExitAsync())}){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}ContextMenu=menu;
        if(service.Settings.Mini)
        {
            var row=new Grid();for(var i=0;i<3;i++)row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var five=MiniCell(out fiveValue,out fiveWarning);row.Children.Add(five);
            var week=MiniCell(out weekValue,out weekWarning);Grid.SetColumn(week,1);row.Children.Add(week);
            var resets=MiniCell(out resetValue,out resetWarning);Grid.SetColumn(resets,2);row.Children.Add(resets);
            var expand=Ui.Btn("↗",ToggleMini,"展开卡片");expand.Padding=new Thickness(4,0,4,0);Grid.SetColumn(expand,3);row.Children.Add(expand);stack.Children.Add(row);
        }
        else
        {
            var header=new DockPanel{Margin=new Thickness(0,0,0,9)};
            var hide=Ui.Btn("−",ToTray,"收进系统托盘");hide.Padding=new Thickness(6,0,6,0);DockPanel.SetDock(hide,Dock.Right);header.Children.Add(hide);
            var mini=Ui.Btn("⋯",()=>ContextMenu.IsOpen=true,"更多选项");mini.Padding=new Thickness(6,0,6,0);mini.Margin=new Thickness(0,0,4,0);DockPanel.SetDock(mini,Dock.Right);header.Children.Add(mini);
            header.Children.Add(Ui.Text("●  Codex",13,"Ink",true));stack.Children.Add(header);
            stack.Children.Add(QuotaRow("五小时",out fiveValue,out fiveTime,out fiveFill,out fiveWarning,"Accent"));
            stack.Children.Add(QuotaRow("一周",out weekValue,out weekTime,out weekFill,out weekWarning,"Blue"));
            var resets=new StackPanel{Margin=new Thickness(0,1,0,3)};
            var resetRow=new DockPanel();var resetGroup=new StackPanel{Orientation=Orientation.Horizontal};resetValue=Ui.Text("—",12,"Accent",true);resetWarning=WarningMark();resetGroup.Children.Add(resetValue);resetGroup.Children.Add(resetWarning);DockPanel.SetDock(resetGroup,Dock.Right);resetRow.Children.Add(resetGroup);resetRow.Children.Add(Ui.Text("可用重置次数",11));resets.Children.Add(resetRow);
            resetExpiry=Ui.Text("最近到期：—",9,"Muted");resetExpiry.Margin=new Thickness(0,3,0,0);resetExpiry.TextTrimming=TextTrimming.CharacterEllipsis;resets.Children.Add(resetExpiry);stack.Children.Add(resets);
            var footer=new DockPanel{Margin=new Thickness(0,5,0,0)};var details=Ui.Btn("详细信息  ↗",()=>app.ShowDetails());details.FontSize=10;details.Padding=new Thickness(5,2,5,2);DockPanel.SetDock(details,Dock.Right);footer.Children.Add(details);
            var refresh=Ui.Btn("↻",()=>{_ = service.RefreshAsync();},"手动刷新");refresh.Padding=new Thickness(3,0,6,0);DockPanel.SetDock(refresh,Dock.Left);footer.Children.Add(refresh);status=Ui.Text("连接中…",9,"Muted");status.TextTrimming=TextTrimming.CharacterEllipsis;footer.Children.Add(status);stack.Children.Add(footer);
        }
        Update();
    }
    private static TextBlock WarningMark()=>new TextBlock
    {
        Text="⚠",FontSize=12,FontFamily=new FontFamily("Segoe UI Symbol"),
        Margin=new Thickness(4,0,0,0),VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed
    };
    private static StackPanel MiniCell(out TextBlock value,out TextBlock warning)
    {
        var cell=new StackPanel{Orientation=Orientation.Horizontal};value=Ui.Text("—",11,"Ink",true);warning=WarningMark();cell.Children.Add(value);cell.Children.Add(warning);return cell;
    }
    private static StackPanel QuotaRow(string label,out TextBlock value,out TextBlock time,out Border fill,out TextBlock warning,string color)
    {
        var s=new StackPanel{Margin=new Thickness(0,0,0,7)};var row=new DockPanel{Margin=new Thickness(0,0,0,4)};
        time=Ui.Text("等待连接",9,"Muted");DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);
        var name=Ui.Text(label,11);name.Width=45;row.Children.Add(name);value=Ui.Text("—",12,color,true);row.Children.Add(value);warning=WarningMark();row.Children.Add(warning);s.Children.Add(row);
        fill=new Border{Height=3,CornerRadius=new CornerRadius(2),HorizontalAlignment=HorizontalAlignment.Left,Width=0};fill.SetResourceReference(Border.BackgroundProperty,color);var track=new Border{Height=3,CornerRadius=new CornerRadius(2),Child=fill};track.SetResourceReference(Border.BackgroundProperty,"Track");s.Children.Add(track);return s;
    }
    public void Update()=>UpdateSnapshot(service.Snapshot);
    internal void PreviewSnapshot(QuotaSnapshot snapshot)=>UpdateSnapshot(snapshot);
    private void UpdateSnapshot(QuotaSnapshot? q)
    {
        var now=DateTimeOffset.UtcNow;var fiveLow=QuotaAlerts.IsLow(q?.FiveHour,now);var weekLow=QuotaAlerts.IsLow(q?.Week,now);var expiring=QuotaAlerts.IsResetExpiring(q?.Resets,now);
        var cache=service.Verified?"":"\n依据缓存数据；刷新后确认当前状态。";
        SetWarning(fiveWarning,fiveLow,$"五小时剩余 {Percent(q?.FiveHour)}，低于或等于 {QuotaAlerts.LowRemainingPercent}%"+cache);
        SetWarning(weekWarning,weekLow,$"每周剩余 {Percent(q?.Week)}，低于或等于 {QuotaAlerts.LowRemainingPercent}%"+cache);
        SetWarning(resetWarning,expiring,$"重置将在 {QuotaAlerts.ResetExpiryDays} 天内到期\n"+Ui.NearestResetExpiry(q?.Resets,"yyyy-MM-dd HH:mm")+(q?.Resets is {DetailsComplete:false}?"\n服务只返回部分明细。":"")+cache);
        resetValue.SetResourceReference(TextBlock.ForegroundProperty,expiring?"Warning":"Accent");
        if(service.Settings.Mini)
        {fiveValue.Text="5h  "+Percent(q?.FiveHour);weekValue.Text="7d  "+Percent(q?.Week);resetValue.Text="重置 "+Ui.ResetCount(q?.Resets);fiveValue.SetResourceReference(TextBlock.ForegroundProperty,fiveLow?"Warning":"Ink");weekValue.SetResourceReference(TextBlock.ForegroundProperty,weekLow?"Warning":"Ink");ToolTip=service.Status+"\n五小时："+FullTime(q?.FiveHour)+"\n一周："+FullTime(q?.Week)+"\n可用重置次数："+Ui.ResetCount(q?.Resets)+"\n"+Ui.NearestResetExpiry(q?.Resets,"yyyy-MM-dd HH:mm");return;}
        UpdateRow(q?.FiveHour,fiveValue,fiveTime,fiveFill,fiveLow?"Warning":"Accent");UpdateRow(q?.Week,weekValue,weekTime,weekFill,weekLow?"Warning":"Blue");
        resetValue.Text=Ui.ResetCount(q?.Resets);resetExpiry.Text=Ui.NearestResetExpiry(q?.Resets);resetExpiry.ToolTip=Ui.NearestResetExpiry(q?.Resets,"yyyy-MM-dd HH:mm")+(q?.Resets is {DetailsComplete:false,NearestExpiryAt:not null}?"\n服务只返回部分明细，实际最早到期时间和次数可能不同。":"");
        resetExpiry.SetResourceReference(TextBlock.ForegroundProperty,expiring?"Warning":"Muted");
        status.Text=service.Status;status.ToolTip=service.Status+"\n"+(q==null?"":$"数据时间：{q.CapturedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n")+"ChatGPT Work 与 Codex 共享额度";
    }
    private static void SetWarning(TextBlock mark,bool visible,string tooltip)
    {mark.Visibility=visible?Visibility.Visible:Visibility.Collapsed;mark.SetResourceReference(TextBlock.ForegroundProperty,"Warning");mark.ToolTip=tooltip;System.Windows.Automation.AutomationProperties.SetName(mark,tooltip);}
    private static string Percent(QuotaWindow? w)=>w==null?"—":$"{w.Remaining:0.#}%";
    private static string FullTime(QuotaWindow? w)=>w==null?"暂无数据":DateTimeOffset.FromUnixTimeSeconds(w.Reset).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    private static void UpdateRow(QuotaWindow? window,TextBlock value,TextBlock time,Border fill,string color)
    {value.Text=Percent(window);time.Text=window==null?"暂无数据":Ui.Countdown(window.Reset);time.ToolTip="刷新时间："+FullTime(window);fill.Width=window==null?0:254*window.Remaining/100;value.ToolTip="剩余额度";value.SetResourceReference(TextBlock.ForegroundProperty,color);fill.SetResourceReference(Border.BackgroundProperty,color);}
    public void ToggleMini(){service.Settings.Mini=!service.Settings.Mini;Build();ClampPosition();service.Settings.Save();}
    public void ToTray(){service.Visible=app.DetailsVisible;Hide();service.Settings.Save();}
    public void Restore(){Show();service.Visible=true;ClampPosition();Activate();if(!service.Busy)_ = service.RefreshAsync();}
    private void RestorePosition()
    {Left=service.Settings.Left ?? SystemParameters.WorkArea.Right-Width-24;Top=service.Settings.Top ?? SystemParameters.WorkArea.Top+32;ClampPosition();}
    public void ClampPosition()
    {
        if(!double.IsFinite(Left)||!double.IsFinite(Top)){Left=20;Top=20;}
        var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;
        var screen=System.Windows.Forms.Screen.FromHandle(hwnd);var dpi=VisualTreeHelper.GetDpi(this);var r=screen.WorkingArea;
        Left=Math.Clamp(Left,r.Left/dpi.DpiScaleX,Math.Max(r.Left/dpi.DpiScaleX,r.Right/dpi.DpiScaleX-Width));Top=Math.Clamp(Top,r.Top/dpi.DpiScaleY,Math.Max(r.Top/dpi.DpiScaleY,r.Bottom/dpi.DpiScaleY-Height));
    }
}
