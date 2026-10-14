using System.Text.Json;
using QuotaFloat.Startup;

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
        get => StartupRegistration.Enabled;
        set
        {
            if(value) StartupRegistration.Enable(Path.Combine(AppContext.BaseDirectory,"QuotaFloat.Launcher.exe"),true);
            else StartupRegistration.Disable();
        }
    }
}
