using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.FabricMC;
using CmlLib.Core.Version;
using SniplandLauncher.Models;

namespace SniplandLauncher.Services
{
    public class LaunchService
    {
        private readonly MinecraftPath _path;
        private readonly CMLauncher _launcher;

        public event Action<string>? LogReceived;
        public event Action<int>? ProgressChanged;

        public LaunchService(string rootPath)
        {
            _path = new MinecraftPath(rootPath);
            _launcher = new CMLauncher(_path);

            _launcher.FileChanged += (e) =>
            {
                LogReceived?.Invoke($"[{e.FileKind}] {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})");
                if (e.TotalFileCount > 0)
                    ProgressChanged?.Invoke((int)((double)e.ProgressedFileCount / e.TotalFileCount * 100));
            };
        }

        public async Task LaunchAsync(GameMode mode, UserSession session, int ramMb)
        {
            var mSession = new MSession
            {
                Username = session.Username,
                UUID = session.UUID,
                AccessToken = session.AccessToken,
                ClientToken = session.ClientToken
            };

            string? versionId = mode.MinecraftVersion;
            if (string.IsNullOrEmpty(versionId))
            {
                LogReceived?.Invoke("Error: Minecraft version is not specified.");
                return;
            }

            // 1. Resolve Java first
            string javaPath = await GetJavaPathAsync(versionId);

            // 2. Ensure Minecraft version is downloaded
            LogReceived?.Invoke($"Checking Minecraft {versionId}...");
            await _launcher.CheckAndDownloadAsync(await _launcher.GetVersionAsync(versionId));

            // 3. Ensure Loader
            if (mode.Loader != null && !string.IsNullOrEmpty(mode.Loader.Type))
            {
                LogReceived?.Invoke($"Ensuring loader {mode.Loader.Type} {mode.Loader.Version}...");
                versionId = await EnsureLoaderAsync(mode, javaPath);
            }

            var launchOption = new MLaunchOption
            {
                MaximumRamMb = ramMb,
                Session = mSession,
                VersionType = "Snipland " + mode.Version,
                GameLauncherName = "SniplandLauncher",
                JavaPath = javaPath
            };

            // JVM arguments for optimization
            launchOption.JVMArguments = new[]
            {
                "-XX:+UseG1GC",
                "-XX:+UnlockExperimentalVMOptions",
                "-XX:G1NewSizePercent=20",
                "-XX:G1ReservePercent=20",
                "-XX:MaxGCPauseMillis=50",
                "-XX:G1HeapRegionSize=32M"
            };

            LogReceived?.Invoke($"Starting Minecraft {versionId}...");
            var process = await _launcher.CreateProcessAsync(versionId, launchOption);

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += (s, e) => { if (e.Data != null) LogReceived?.Invoke(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) LogReceived?.Invoke(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private async Task<string> EnsureLoaderAsync(GameMode mode, string javaPath)
        {
            string mcVersion = mode.MinecraftVersion!;
            string? loaderVersion = mode.Loader?.Version;

            if (mode.Loader?.Type?.Equals("forge", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (string.IsNullOrEmpty(loaderVersion)) return mcVersion;

                var forge = new MForge(_path, javaPath);
                forge.FileChanged += (e) =>
                {
                    if (e.TotalFileCount > 0)
                        ProgressChanged?.Invoke((int)((double)e.ProgressedFileCount / e.TotalFileCount * 100));
                    LogReceived?.Invoke($"[Forge] {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})");
                };
                forge.InstallerOutput += (s, e) => { if (e != null) LogReceived?.Invoke(e); };

                // Check if already installed
                var installedVersions = await _launcher.GetAllVersionsAsync();
                var forgeVersion = installedVersions.FirstOrDefault(v => v.Name.Contains("forge", StringComparison.OrdinalIgnoreCase)
                    && v.Name.Contains(mcVersion) && v.Name.Contains(loaderVersion));

                if (forgeVersion != null)
                    return forgeVersion.Name;

                return await Task.Run(() => forge.InstallForge(mcVersion, loaderVersion));
            }
            else if (mode.Loader?.Type?.Equals("fabric", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (string.IsNullOrEmpty(loaderVersion)) return mcVersion;

                // Check if already installed
                var installedVersions = await _launcher.GetAllVersionsAsync();
                var fabricVersion = installedVersions.FirstOrDefault(v => v.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase)
                    && v.Name.Contains(mcVersion) && v.Name.Contains(loaderVersion));

                if (fabricVersion != null)
                    return fabricVersion.Name;

                var fabric = new FabricVersionLoader();
                var fabricVersions = await fabric.GetVersionMetadatasAsync();
                var metadata = fabricVersions.FirstOrDefault(v => v.Name.Contains(loaderVersion) && v.Name.Contains(mcVersion));

                if (metadata != null)
                {
                    await metadata.SaveAsync(_path);
                    return metadata.Name;
                }
            }

            return mcVersion;
        }

        private async Task<string> GetJavaPathAsync(string mcVersion)
        {
            int javaVersionNum = 8;
            if (IsVersionAtLeast(mcVersion, "1.20.5")) javaVersionNum = 21;
            else if (IsVersionAtLeast(mcVersion, "1.17")) javaVersionNum = 17;

            string javaDir = Path.Combine(_path.BasePath, "runtime", $"java-{javaVersionNum}");
            string binName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
            string javaPath = Path.Combine(javaDir, "bin", binName);

            if (File.Exists(javaPath))
                return javaPath;

            await DownloadJavaAsync(javaVersionNum, javaDir);

            // On Linux/macOS, we need to set execute permissions
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var process = Process.Start("chmod", $"+x \"{javaPath}\"");
                    await process.WaitForExitAsync();
                }
                catch { }
            }

            return javaPath;
        }

        private async Task DownloadJavaAsync(int version, string targetDir)
        {
            LogReceived?.Invoke($"Downloading Java {version}...");

            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
            string arch = "x64";
            string url = $"https://api.adoptium.net/v3/binary/latest/{version}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + (os == "windows" ? ".zip" : ".tar.gz"));
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            LogReceived?.Invoke($"Extracting Java {version}...");
            if (os == "windows")
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, targetDir);
            }
            else
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                await ExtractTarGzAsync(tempFile, targetDir);
            }

            // Move contents up if they are in a subfolder
            var binDir = Directory.GetDirectories(targetDir, "bin", SearchOption.AllDirectories).FirstOrDefault();
            if (binDir != null)
            {
                var jreRoot = Path.GetDirectoryName(binDir);
                if (jreRoot != targetDir && jreRoot != null)
                {
                    foreach (var dir in Directory.GetDirectories(jreRoot))
                    {
                        var dest = Path.Combine(targetDir, Path.GetFileName(dir));
                        if (!Directory.Exists(dest)) Directory.Move(dir, dest);
                    }
                    foreach (var file in Directory.GetFiles(jreRoot))
                    {
                        var dest = Path.Combine(targetDir, Path.GetFileName(file));
                        if (!File.Exists(dest)) File.Move(file, dest);
                    }
                }
            }

            if (File.Exists(tempFile)) File.Delete(tempFile);
            LogReceived?.Invoke($"Java {version} installed.");
        }

        private async Task ExtractTarGzAsync(string tarGzPath, string targetDir)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xzf \"{tarGzPath}\" -C \"{targetDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process != null) await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"Error extracting tar.gz: {ex.Message}");
            }
        }

        private bool IsVersionAtLeast(string version, string minVersion)
        {
            try
            {
                var v1Text = version.Split('-')[0];
                if (v1Text.Count(c => c == '.') == 1) v1Text += ".0";
                var v1 = new Version(v1Text);

                var v2Text = minVersion;
                if (v2Text.Count(c => c == '.') == 1) v2Text += ".0";
                var v2 = new Version(v2Text);

                return v1 >= v2;
            }
            catch { return false; }
        }
    }
}
