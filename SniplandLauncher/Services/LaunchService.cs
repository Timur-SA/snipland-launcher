using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
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

            var launchOption = new MLaunchOption
            {
                MaximumRamMb = ramMb,
                Session = mSession,
                VersionType = "Snipland " + mode.Version,
                GameLauncherName = "SniplandLauncher"
            };

            string? versionId = mode.MinecraftVersion;

            if (mode.Loader != null)
            {
                LogReceived?.Invoke($"Loader {mode.Loader.Type} {mode.Loader.Version} requested.");
            }

            if (versionId == null) return;

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
    }
}
