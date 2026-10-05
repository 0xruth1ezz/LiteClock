using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LiteClock;

public static class Store
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string Serialize(Settings settings) => JsonSerializer.Serialize(settings, JsonOptions);
    public static string DirectoryPath = AppContext.BaseDirectory;
    public static string ConfigPath { get { return Path.Combine(DirectoryPath, "settings.json"); } }
    public static string RecoveryMessage;
    public static Settings Load()
    {
        try { return Read(ConfigPath); }
        catch (FileNotFoundException) { return new Settings(); }
        catch (Exception ex)
        {
            Log(ex);
            try
            {
                Settings backup = Read(ConfigPath + ".bak");
                RecoveryMessage = "配置文件损坏，已读取上一次备份。原文件保留在程序目录。";
                return backup;
            }
            catch { RecoveryMessage = "配置无法读取，已使用默认显示。原文件保留在程序目录。"; return new Settings(); }
        }
    }
    public static Settings Read(string path)
    {
        var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        if (result == null) throw new InvalidDataException("配置为空。");
        result.Validate(); return result;
    }
    public static void Save(Settings settings)
    {
        settings.Validate();
        string temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, Serialize(settings), new UTF8Encoding(false));
        if (File.Exists(ConfigPath)) File.Replace(temporary, ConfigPath, ConfigPath + ".bak");
        else File.Move(temporary, ConfigPath);
    }
    public static void Log(Exception error)
    {
        try
        {
            string path = Path.Combine(DirectoryPath, "LiteClock.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256000)
                File.WriteAllText(path, "Log rotated\r\n");
            File.AppendAllText(path, DateTime.Now.ToString("o") + " " + error + Environment.NewLine);
        }
        catch { }
    }
}
