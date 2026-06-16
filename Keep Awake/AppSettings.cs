using System.IO;
using System.Text.Json;

namespace Keep_Awake;

public enum ActiveMode { AlwaysActive, Scheduled }

public class AppSettings
{
    public bool       StartWithWindows  { get; set; } = false;
    public bool       MinimizeOnClose   { get; set; } = true;
    public ActiveMode Mode              { get; set; } = ActiveMode.AlwaysActive;
    public List<ScheduleEntry> Schedule { get; set; } = new();

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KeepAwake",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }
}
