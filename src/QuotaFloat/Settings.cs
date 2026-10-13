using System.Text.Json;
using Microsoft.Win32;

namespace QuotaFloat;
public sealed class Settings
{
    public double? Left {get;set;}
    public double? Top {get;set;}
    public bool Mini {get;set;}
    public bool Topmost {get;set;}=true;
    public string? LastAccount {get;set;}
    public static string DataDirectory {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuotaFloat");
    private static string FilePath=>Path.Combine(DataDirectory,"settings.json");
    public static Settings Load()
    {
        try {return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {return new();}
    }
    public void Save()
    { Directory.CreateDirectory(DataDirectory);var temp=FilePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,FilePath,true); }
    public static bool AutoStart
    {
        get {using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return key?.GetValue("QuotaFloat")!=null;}
        set
        {
            using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(value)
            {
                var launcher=Path.Combine(AppContext.BaseDirectory,"QuotaFloat.Launcher.exe");
                key.SetValue("QuotaFloat","\""+(File.Exists(launcher)?launcher:Environment.ProcessPath)+"\"");
                if(File.Exists(launcher))
                {
                    using var launched=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launcher){UseShellExecute=true});
                }
            }
            else key.DeleteValue("QuotaFloat",false);
        }
    }
}
