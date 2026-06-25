using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using 积微.Models;

namespace 积微.Services
{
    /// <summary>目标数据存储服务</summary>
    public class DataStorageService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        private static string GetDataFilePath()
        {
            string dataStoragePath = SettingsManager.Current.DataStoragePath;

            // 如果用户没有设定目录或者目录为空，使用默认路径
            if (string.IsNullOrEmpty(dataStoragePath))
            {
                dataStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "积微"
                );
            }

            // 确保目录存在
            if (!Directory.Exists(dataStoragePath))
            {
                Directory.CreateDirectory(dataStoragePath);
            }

            return Path.Combine(dataStoragePath, "goals.json");
        }

        /// <summary>从文件加载目标列表</summary>
        public static async Task<List<Goal>> LoadGoalsAsync()
        {
            try
            {
                string dataFilePath = GetDataFilePath();
                if (File.Exists(dataFilePath))
                {
                    var json = await File.ReadAllTextAsync(dataFilePath);
                    return JsonSerializer.Deserialize<List<Goal>>(json, SerializerOptions) ?? new List<Goal>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading goals: {ex.Message}");
            }

            return new List<Goal>();
        }

        /// <summary>保存目标列表到文件</summary>
        public static async Task SaveGoalsAsync(List<Goal> goals)
        {
            try
            {
                string dataFilePath = GetDataFilePath();
                var json = JsonSerializer.Serialize(goals, SerializerOptions);
                await File.WriteAllTextAsync(dataFilePath, json);

                // 保存后清理未使用的图片文件
                await CleanupUnusedImagesAsync(goals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving goals: {ex.Message}");
            }
        }

        /// <summary>清理未使用的图片文件</summary>
        public static async Task CleanupUnusedImagesAsync(List<Goal> goals)
        {
            try
            {
                string dataStoragePath = SettingsManager.Current.DataStoragePath;
                if (string.IsNullOrEmpty(dataStoragePath))
                {
                    dataStoragePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "积微"
                    );
                }

                string imagesFolder = Path.Combine(dataStoragePath, "images");
                if (!Directory.Exists(imagesFolder))
                {
                    return;
                }

                // 收集所有被引用的图片路径
                HashSet<string> usedImagePaths = new HashSet<string>();
                CollectUsedImages(goals, usedImagePaths);

                // 获取所有图片文件
                var imageFiles = Directory.GetFiles(imagesFolder, "*.png");

                // 删除未使用的图片文件
                foreach (var imageFile in imageFiles)
                {
                    string relativePath = Path.Combine("images", Path.GetFileName(imageFile));
                    if (!usedImagePaths.Contains(relativePath))
                    {
                        try
                        {
                            File.Delete(imageFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting unused image: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up images: {ex.Message}");
            }
        }

        /// <summary>收集所有被引用的图片路径</summary>
        private static void CollectUsedImages(List<Goal> goals, HashSet<string> usedImagePaths)
        {
            foreach (var goal in goals)
            {
                foreach (var entry in goal.Timeline)
                {
                    if (entry.HasImages && entry.ImagePathList != null)
                    {
                        foreach (var imagePath in entry.ImagePathList)
                        {
                            usedImagePaths.Add(imagePath);
                        }
                    }
                }

                if (goal.Children != null && goal.Children.Count > 0)
                {
                    CollectUsedImages(goal.Children, usedImagePaths);
                }
            }
        }
    }
}