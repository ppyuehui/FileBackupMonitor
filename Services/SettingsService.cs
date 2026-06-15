using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Logging;

namespace FileBackupMonitor.Services
{
    /// <summary>
    /// JSON 文件持久化 — settings.json 放在 %AppData%\文件备份监控助手\
    /// </summary>
    public static class SettingsService
    {
        //settings.json 放在 %AppData%\文件备份监控助手\
        private static readonly string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "文件备份监控助手");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");
        private static readonly string CategoriesPath = Path.Combine(ConfigDir, "categories.json");


        public static Models.AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var opts = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    return JsonSerializer.Deserialize<Models.AppSettings>(json, opts) ?? new Models.AppSettings();
                }
            }
            catch (Exception ex) { FileLogger.LogError("读取配置文件失败：", ex); }
            return new Models.AppSettings();
        }

        public static void Save(Models.AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(settings, opts);
                File.WriteAllText(ConfigPath, json);
            }
            catch(Exception ex) { FileLogger.LogError("保存配置文件失败：", ex); }
        }

        public static Dictionary<string, List<string>> LoadCategories()
        {
            try
            {
                if (File.Exists(CategoriesPath))
                {
                    var json = File.ReadAllText(CategoriesPath);
                    var opts = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, opts);
                }
            }
            catch (Exception ex) { FileLogger.LogError("读取分类文件失败：", ex); }
            return null;
        }

        public static void SaveCategories(Dictionary<string, List<string>> categories)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(categories, opts);
                File.WriteAllText(CategoriesPath, json);
            }
            catch (Exception ex) { FileLogger.LogError("保存分类文件失败：", ex); }
        }
    }
}
