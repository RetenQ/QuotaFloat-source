namespace QuotaFloat;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(App app,MonitorService service)
    {
        Title="额度浮窗 · 设置";Width=420;Height=410;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;SetResourceReference(BackgroundProperty,"Surface");
        var body=new StackPanel{Margin=new Thickness(26)};body.Children.Add(Ui.Text("按你的习惯使用",21,"Ink",true));
        Check("悬浮窗始终置顶",service.Settings.Topmost,v=>{service.Settings.Topmost=v;app.Widget.Topmost=v;service.Settings.Save();});
        Check("默认使用迷你条",service.Settings.Mini,v=>{service.Settings.Mini=v;app.Widget.Build();service.Settings.Save();});
        Check("登录 Windows 或重新打开 Codex 时启动",Settings.AutoStart,v=>Settings.AutoStart=v);
        var note=Ui.Text("外观跟随系统深浅色。\n\n可见时每 2 分钟刷新，托盘中每 10 分钟刷新；历史每 30 分钟更新。\n\n普通聊天没有可验证的数据时显示“暂不可获取”。",12,"Muted");note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,22,0,18);body.Children.Add(note);
        var login=Ui.Btn("打开 Codex 登录入口",()=>Ui.OpenUrl("codex://"));login.HorizontalAlignment=HorizontalAlignment.Left;body.Children.Add(login);Content=body;
        void Check(string label,bool initial,Action<bool> change){var c=new CheckBox{Content=label,IsChecked=initial,FontSize=13,Margin=new Thickness(0,20,0,0)};c.SetResourceReference(ForegroundProperty,"Ink");c.Click+=(_,_)=>{try{change(c.IsChecked==true);}catch(Exception){c.IsChecked=!c.IsChecked;MessageBox.Show("设置未能保存，请检查 Windows 权限。","额度浮窗");}};body.Children.Add(c);}
    }
}
