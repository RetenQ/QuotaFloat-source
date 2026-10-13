using QuotaFloat.Core;

namespace QuotaFloat;
public sealed class DetailsWindow : Window
{
    private readonly MonitorService service;
    private readonly App app;
    private readonly StackPanel body=new();
    private ScrollViewer scroll=null!;
    private readonly Dictionary<string,Button> tabButtons=new();
    private readonly Dictionary<int,Button> dayButtons=new();
    private int days=7;
    private string tab="Codex";
    public DetailsWindow(MonitorService service,App app)
    {
        this.service=service;this.app=app;Title="额度浮窗 · 使用情况";Width=960;Height=820;MinWidth=760;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty,"Panel");FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");
        var root=new DockPanel{Margin=new Thickness(28,22,28,22)};
        var top=new DockPanel{Margin=new Thickness(0,0,0,22)};var settings=Ui.Btn("设置",()=>app.ShowSettings());DockPanel.SetDock(settings,Dock.Right);top.Children.Add(settings);
        var titles=new StackPanel();titles.Children.Add(Ui.Text("使用情况",25,"Ink",true));titles.Children.Add(Ui.Text("看清额度，也看见使用节奏。",12,"Muted"));top.Children.Add(titles);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var toolbar=new DockPanel{Margin=new Thickness(0,0,0,16)};var period=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        foreach(var n in new[]{7,30}){var b=Ui.Btn($"近 {n} 天",()=>{days=n;Render();});b.Margin=new Thickness(5,0,0,0);period.Children.Add(b);dayButtons[n]=b;}var refresh=Ui.Btn("↻ 刷新",()=>{_ = service.RefreshAsync(true);});refresh.Margin=new Thickness(8,0,0,0);period.Children.Add(refresh);DockPanel.SetDock(period,Dock.Right);toolbar.Children.Add(period);
        var tabs=new StackPanel{Orientation=Orientation.Horizontal};foreach(var name in new[]{"Codex","ChatGPT Work","普通聊天"}){var b=Ui.Btn(name,()=>{tab=name;Render();scroll.ScrollToTop();});b.Margin=new Thickness(0,0,7,0);tabs.Children.Add(b);tabButtons[name]=b;}toolbar.Children.Add(tabs);DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
        scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);Content=root;
        service.Changed+=Render;Closed+=(_,_)=>{service.Changed-=Render;body.Children.Clear();};Render();
    }
    public void SetTabForVerification(string name){tab=name;Render();}
    public void ScrollToBottom()=>scroll.ScrollToEnd();
    public void Render()
    {
        body.Children.Clear();
        foreach(var (name,button) in tabButtons){button.SetResourceReference(Control.ForegroundProperty,name==tab?"Accent":"Muted");button.SetResourceReference(Control.BackgroundProperty,name==tab?"Surface":"Panel");}
        foreach(var (n,button) in dayButtons){button.SetResourceReference(Control.ForegroundProperty,n==days?"Accent":"Muted");button.SetResourceReference(Control.BackgroundProperty,n==days?"Surface":"Panel");}
        if(tab=="普通聊天")
        {
            var empty=new StackPanel{Margin=new Thickness(20,32,20,40)};empty.Children.Add(Ui.Text("ChatGPT 普通聊天",22,"Ink",true));
            var text=Ui.Text("额度与历史用量暂不可获取",16,"Muted");text.Margin=new Thickness(0,16,0,10);empty.Children.Add(text);
            var note=Ui.Text("当前已验证的数据接口用于 Codex / Work。普通聊天的剩余额度、刷新时间和历史 Token 尚无已验证来源，暂不展示数值。",13,"Muted");note.TextWrapping=TextWrapping.Wrap;note.MaxWidth=620;note.HorizontalAlignment=HorizontalAlignment.Left;empty.Children.Add(note);
            var open=Ui.Btn("打开 ChatGPT  ↗",()=>Ui.OpenUrl("https://chatgpt.com/"));open.HorizontalAlignment=HorizontalAlignment.Left;open.Margin=new Thickness(0,24,0,0);empty.Children.Add(open);body.Children.Add(Ui.Card(empty));return;
        }
        var intro=new DockPanel{Margin=new Thickness(0,0,0,14)};var badge=Ui.Text($"近 {days} 天",12,"Muted");DockPanel.SetDock(badge,Dock.Right);intro.Children.Add(badge);intro.Children.Add(Ui.Text(tab=="ChatGPT Work"?"ChatGPT Work · 与 Codex 共享额度，不重复累计":"Codex · 包含共享的 ChatGPT Work 额度",12,"Muted"));body.Children.Add(intro);
        var quotaGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new Thickness(0,0,0,14)};
        quotaGrid.Children.Add(QuotaCard("五小时额度",service.Snapshot?.FiveHour,"Accent",new Thickness(0,0,7,0)));quotaGrid.Children.Add(QuotaCard("每周额度",service.Snapshot?.Week,"Blue",new Thickness(7,0,0,0)));body.Children.Add(quotaGrid);
        var resets=service.Snapshot?.Resets;var resetCard=new StackPanel();resetCard.Children.Add(Ui.Text("可用重置次数",12,"Muted"));
        var resetCount=Ui.Text(Ui.ResetCount(resets),26,"Accent",true);resetCount.Margin=new Thickness(0,4,0,2);resetCard.Children.Add(resetCount);
        resetCard.Children.Add(Note(Ui.NearestResetExpiry(resets,"yyyy-MM-dd HH:mm")));
        if(resets is {DetailsComplete:false,NearestExpiryAt:not null})resetCard.Children.Add(Note("服务只返回部分重置明细，实际最早到期时间和次数可能不同。"));
        var resetBorder=Ui.Card(resetCard);resetBorder.Margin=new Thickness(0,0,0,14);body.Children.Add(resetBorder);
        var connection=Ui.Text(service.Status+(service.Snapshot==null?"":$" · 数据时间 {service.Snapshot.CapturedAt.ToLocalTime():MM-dd HH:mm:ss}")+(service.Verified?"":" · 当前登录尚未确认"),11,"Muted");connection.Margin=new Thickness(0,0,0,14);body.Children.Add(connection);
        var data=service.Store.ReadDashboard(service.Account,days);
        var quotaChart=new StackPanel();quotaChart.Children.Add(Ui.Text("官方剩余额度变化",16,"Ink",true));var legend=Ui.Text("● 五小时（绿色）   ● 每周（蓝色）",11,"Muted");legend.Margin=new Thickness(0,8,0,0);quotaChart.Children.Add(legend);quotaChart.Children.Add(new UsageChart{Quotas=data.Quotas,DayCount=days,Height=155});quotaChart.Children.Add(Note("由本工具定时保存官方额度返回值，从开始运行时积累。刷新窗口或采样缺口会断开曲线。"));AddCard(quotaChart);
        var trend=data.OfficialDays;
        var chartStack=new StackPanel();chartStack.Children.Add(Ui.Text("官方每日用量",16,"Ink",true));
        if(trend.Count==0)
        {
            var empty=Ui.Text("暂无官方历史记录",19,"Muted");empty.Margin=new Thickness(0,16,0,6);chartStack.Children.Add(empty);
            chartStack.Children.Add(Note(service.HistoryStatus));
            chartStack.Children.Add(Note("实时额度可以正常读取。这里仅展示官方返回的每日 Token，不从本机日志补算。"));
        }
        else
        {
            var sum=Ui.Text(Ui.Number(trend.Sum(x=>x.Tokens)),30,"Accent",true);sum.Margin=new Thickness(0,9,0,0);sum.ToolTip=$"{trend.Sum(x=>x.Tokens):N0} Token";chartStack.Children.Add(sum);
            chartStack.Children.Add(Note(service.HistoryStatus));chartStack.Children.Add(new UsageChart{Days=trend,DayCount=days});chartStack.Children.Add(Note("缺失日期显示为 ·，不代表零消耗。"));
        }
        AddCard(chartStack);
        var foot=new DockPanel{Margin=new Thickness(0,4,0,0)};var official=Ui.Btn("官方用量页面 ↗",Ui.UsagePage);DockPanel.SetDock(official,Dock.Right);foot.Children.Add(official);foot.Children.Add(Note("仅官方数据 · 在本机保留 90 天"));body.Children.Add(foot);
    }
    private Border QuotaCard(string title,QuotaWindow? w,string color,Thickness margin)
    {var s=new StackPanel();s.Children.Add(Ui.Text(title,12,"Muted"));var value=Ui.Text(w==null?"—":$"{w.Remaining:0.#}%",30,color,true);value.Margin=new Thickness(0,5,0,4);s.Children.Add(value);s.Children.Add(Ui.Text(w==null?"暂无数据":$"剩余 · {Ui.Countdown(w.Reset)}刷新",11,"Muted"));if(w!=null)s.ToolTip=$"刷新时间：{DateTimeOffset.FromUnixTimeSeconds(w.Reset).ToLocalTime():yyyy-MM-dd HH:mm:ss}";var card=Ui.Card(s);card.Margin=margin;return card;}
    private static TextBlock Note(string text){var t=Ui.Text(text,11,"Muted");t.TextWrapping=TextWrapping.Wrap;t.Margin=new Thickness(0,8,0,0);return t;}
    private void AddCard(UIElement child){var card=Ui.Card(child);card.Margin=new Thickness(0,0,0,14);body.Children.Add(card);}
}
