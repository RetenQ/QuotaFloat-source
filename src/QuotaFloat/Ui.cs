using Microsoft.Win32;
using QuotaFloat.Core;

namespace QuotaFloat;
public static class Ui
{
    public static bool Dark {get;private set;}
    public static void ApplyTheme(bool? dark=null)
    {
        if(dark==null)
        { using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");dark=key?.GetValue("AppsUseLightTheme") is int v && v==0; }
        Dark=dark.Value;
        var resources=Application.Current.Resources;
        var colors=Dark ? new[]{"#181D24","#222A34","#F2F6FA","#9BAABB","#354150","#56D6B7","#79AFFF","#12171D","#FFC166"} : new[]{"#FBFCFE","#F0F4F8","#182B3C","#67798B","#DEE6EE","#087F70","#386CCD","#EAF0F6","#A65A00"};
        var names=new[]{"Surface","Panel","Ink","Muted","Line","Accent","Blue","Track","Warning"};
        for(int i=0;i<names.Length;i++)resources[names[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        if(!resources.Contains(typeof(Button)))
        {
            var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.FontFamilyProperty,new FontFamily("Segoe UI, Microsoft YaHei UI")));style.Setters.Add(new Setter(Control.FontSizeProperty,12d));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,5,10,5)));style.Setters.Add(new Setter(Control.CursorProperty,System.Windows.Input.Cursors.Hand));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension("Panel")));style.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension("Ink")));
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;style.Setters.Add(new Setter(Control.TemplateProperty,template));var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,.76));style.Triggers.Add(hover);var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.4));style.Triggers.Add(disabled);resources[typeof(Button)]=style;
        }
    }
    public static Brush B(string name)=>(Brush)Application.Current.Resources[name];
    public static TextBlock Text(string text,double size=12,string color="Ink",bool bold=false)
    {var t=new TextBlock{Text=text,FontSize=size,FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
    public static Button Btn(string text,Action action,string? tooltip=null)
    {var b=new Button{Content=text,ToolTip=tooltip};b.Click+=(_,_)=>action();return b;}
    public static Border Card(UIElement child,int padding=18)
    {var b=new Border{CornerRadius=new CornerRadius(12),Padding=new Thickness(padding),Child=child,BorderThickness=new Thickness(1)};b.SetResourceReference(Border.BackgroundProperty,"Surface");b.SetResourceReference(Border.BorderBrushProperty,"Line");return b;}
    public static string Number(long value)=>value>=1_000_000?$"{value/1_000_000d:0.##}M":value>=1000?$"{value/1000d:0.#}K":value.ToString("N0");
    public static string Countdown(long reset)
    {var d=DateTimeOffset.FromUnixTimeSeconds(reset)-DateTimeOffset.Now;if(d<=TimeSpan.Zero)return "等待刷新";return d.TotalDays>=1?$"{(int)d.TotalDays}天 {d.Hours}小时后":d.TotalHours>=1?$"{(int)d.TotalHours}小时 {d.Minutes}分后":$"{Math.Max(1,(int)d.TotalMinutes)}分钟后";}
    public static string ResetCount(ResetCredits? resets)=>resets==null?"—":$"{resets.AvailableCount} 次";
    public static string NearestResetExpiry(ResetCredits? resets, string dateFormat="MM-dd HH:mm")
    {
        if(resets==null)return "最近到期：—";
        if(resets.AvailableCount==0)return "最近到期：无";
        if(resets.NearestExpiryAt is not long time || resets.NearestExpiryCount is not int count)return "最近到期：暂无到期明细";
        var prefix=resets.DetailsComplete?"最近到期":"已知最近到期";
        return $"{prefix}：{count} 次 · {DateTimeOffset.FromUnixTimeSeconds(time).ToLocalTime().ToString(dateFormat)}";
    }
    public static void OpenUrl(string url)
    {try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch(Exception){MessageBox.Show("无法打开浏览器，请手动访问 ChatGPT。","额度浮窗");}}
    public static void UsagePage()=>OpenUrl("https://chatgpt.com/settings/usage");
}
