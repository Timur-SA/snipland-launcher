using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SniplandLauncher.Models;

namespace SniplandLauncher.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;

        public event Action<double, string>? ProgressChanged;

        public UpdateService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<LauncherManifest?> GetLauncherManifestAsync(string url)
        {
            try
            {
                var json = await _httpClient.GetStringAsync(url);
                return JsonConvert.DeserializeObject<LauncherManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<ModeManifest?> GetModeManifestAsync(string url)
        {
            try
            {
                var json = await _httpClient.GetStringAsync(url);
                return JsonConvert.DeserializeObject<ModeManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SyncModeFilesAsync(string basePath, ModeManifest manifest)
        {
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            // 1. Cleaning
            if (manifest.UpdateCleaning != null)
            {
                foreach (var dirName in manifest.UpdateCleaning)
                {
                    var dirPath = Path.Combine(basePath, dirName);
                    if (Directory.Exists(dirPath))
                    {
                        var filesInDir = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                        foreach (var file in filesInDir)
                        {
                            var relativePath = Path.GetRelativePath(basePath, file).Replace("\\", "/");
                            if (manifest.Files != null && !manifest.Files.Any(f => f.Path == relativePath))
                            {
                                File.Delete(file);
                            }
                        }
                    }
                }
            }

            // 2. Downloading / Updating
            if (manifest.Files == null) return;

            int totalFiles = manifest.Files.Count;
            int completedFiles = 0;

            foreach (var fileEntry in manifest.Files)
            {
                if (string.IsNullOrEmpty(fileEntry.Path) || string.IsNullOrEmpty(fileEntry.Url)) continue;

                var localPath = Path.Combine(basePath, fileEntry.Path);
                var directory = Path.GetDirectoryName(localPath);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                bool needDownload = true;
                if (File.Exists(localPath))
                {
                    var localHash = CalculateSHA1(localPath);
                    if (localHash.Equals(fileEntry.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        needDownload = false;
                    }
                }

                if (needDownload)
                {
                    ProgressChanged?.Invoke((double)completedFiles / totalFiles, $"Downloading {fileEntry.Path}...");
                    await DownloadFileAsync(fileEntry.Url, localPath);
                }

                completedFiles++;
                ProgressChanged?.Invoke((double)completedFiles / totalFiles, $"Checked {fileEntry.Path}");
            }
        }

        private async Task DownloadFileAsync(string url, string path)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        private string CalculateSHA1(string filePath)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
