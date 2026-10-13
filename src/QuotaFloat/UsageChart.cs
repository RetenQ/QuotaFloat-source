using System.Globalization;
using QuotaFloat.Core;

namespace QuotaFloat;
public sealed class UsageChart : FrameworkElement
{
    public List<DayUsage>? Days {get;init;}
    public List<QuotaPoint>? Quotas {get;init;}
    public int DayCount {get;init;}=7;
    public UsageChart(){Height=190;MinWidth=220;SnapsToDevicePixels=true;MouseMove+=(_,e)=>{if(Days==null)return;var day=DateTime.Today.AddDays(1-DayCount+Math.Clamp((int)((e.GetPosition(this).X-48)/Math.Max(1,(ActualWidth-64)/DayCount)),0,DayCount-1));var data=Days.FirstOrDefault(d=>d.Day==day.ToString("yyyy-MM-dd"));ToolTip=$"{day:MM-dd}："+(data==null?"无记录（不代表零消耗）":$"{data.Tokens:N0} Token");};}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double left=48,top=15,w=Math.Max(1,ActualWidth-64),h=ActualHeight-43;
        var max=Quotas!=null?100:Math.Max(1,Days?.MaxBy(x=>x.Tokens)?.Tokens ?? 1);
        for(int i=0;i<=4;i++){var y=top+h*i/4;dc.DrawLine(new Pen(Ui.B("Line"),.6),new(left,y),new(left+w,y));Label(dc,Quotas!=null?$"{100-i*25}%":Ui.Number((long)(max*(4-i)/4)),0,y-7,10);}
        var start=DateTime.Today.AddDays(1-DayCount);
        Label(dc,start.ToString("MM/dd"),left,top+h+12,10);Label(dc,DateTime.Today.ToString("MM/dd"),left+w-36,top+h+12,10);
        if(Quotas!=null)
        {
            if(Quotas.Count==0){Empty(dc,"开始记录后，这里会出现额度变化",left,w,top+h/2);return;}
            var begin=new DateTimeOffset(start).ToUnixTimeSeconds();var end=DateTimeOffset.Now.ToUnixTimeSeconds();var span=Math.Max(1,end-begin);
            for(int series=0;series<2;series++)
            {
                Point? prev=null;QuotaPoint? last=null;var pen=new Pen(Ui.B(series==0?"Accent":"Blue"),2);
                foreach(var p in Quotas)
                {
                    var value=series==0?p.Five:p.Week;if(value==null){prev=null;last=null;continue;}
                    var next=new Point(left+Math.Clamp((p.Time-begin)/(double)span,0,1)*w,top+(100-value.Value)/100*h);
                    var sameWindow=last!=null && (series==0?last.FiveReset==p.FiveReset:last.WeekReset==p.WeekReset);
                    if(prev!=null && last!=null && p.Time-last.Time<=20*60 && sameWindow)dc.DrawLine(pen,prev.Value,next);
                    dc.DrawEllipse(pen.Brush,null,next,2.3,2.3);prev=next;last=p;
                }
            }
        }
        else
        {
            if(Days==null || Days.Count==0){Empty(dc,"此来源暂无历史数据",left,w,top+h/2);return;}
            var step=w/DayCount;var bw=Math.Min(32,step*.58);
            for(int i=0;i<DayCount;i++)
            {
                var day=start.AddDays(i).ToString("yyyy-MM-dd");var d=Days.FirstOrDefault(x=>x.Day==day);var x=left+i*step+(step-bw)/2;
                if(d==null){Label(dc,"·",x+bw/2-2,top+h-9,12);continue;}
                var bh=Math.Max(2,d.Tokens/max*h);dc.DrawRoundedRectangle(Ui.B("Accent"),null,new Rect(x,top+h-bh,bw,bh),3,3);
            }
        }
    }
    private void Label(DrawingContext dc,string text,double x,double y,double size)
    {dc.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,System.Windows.FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),size,Ui.B("Muted"),VisualTreeHelper.GetDpi(this).PixelsPerDip),new(x,y));}
    private void Empty(DrawingContext dc,string text,double left,double w,double y)=>Label(dc,text,left+Math.Max(0,(w-text.Length*12)/2),y,12);
}
