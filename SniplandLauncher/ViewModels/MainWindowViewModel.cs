using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using SniplandLauncher.Models;
using SniplandLauncher.Services;

namespace SniplandLauncher.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly LaunchService _launchService;

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
        }

        private string _username = "";
        public string Username
        {
            get => _username;
            set => this.RaiseAndSetIfChanged(ref _username, value);
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        private UserSession? _session;
        public ObservableCollection<GameMode> Modes { get; } = new();

        private GameMode? _selectedMode;
        public GameMode? SelectedMode
        {
            get => _selectedMode;
            set => this.RaiseAndSetIfChanged(ref _selectedMode, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => this.RaiseAndSetIfChanged(ref _progress, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> LaunchCommand { get; }

        public MainWindowViewModel()
        {
            _authService = new AuthService();
            _updateService = new UpdateService();
            _launchService = new LaunchService("minecraft");

            _updateService.ProgressChanged += (p, s) =>
            {
                Progress = p * 100;
                StatusText = s;
            };

            _launchService.LogReceived += (s) => StatusText = s;

            LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync);
            LaunchCommand = ReactiveCommand.CreateFromTask(LaunchAsync);

            // Mock modes for now if manifest not loaded
            _ = LoadManifestAsync();
        }

        private async Task LoadManifestAsync()
        {
            var manifest = await _updateService.GetLauncherManifestAsync("https://raw.githubusercontent.com/user/repo/main/launcher.json");
            if (manifest?.Modes != null)
            {
                foreach (var mode in manifest.Modes)
                    Modes.Add(mode);
            }
            else
            {
                // Fallback / Mock
                Modes.Add(new GameMode { Id = "test", Name = "Test Mode", Version = "1.0.0", MinecraftVersion = "1.19.2" });
            }
        }

        private async Task LoginAsync()
        {
            IsBusy = true;
            StatusText = "Logging in...";
            _session = await _authService.LoginAsync(Username, Password);
            if (_session != null)
            {
                IsLoggedIn = true;
                StatusText = $"Logged in as {_session.Username}";
            }
            else
            {
                StatusText = "Login failed";
            }
            IsBusy = false;
        }

        private async Task LaunchAsync()
        {
            if (SelectedMode == null || _session == null) return;

            IsBusy = true;
            StatusText = "Updating files...";

            var modeManifest = await _updateService.GetModeManifestAsync(SelectedMode.RemoteManifestUrl ?? "");
            if (modeManifest != null)
            {
                await _updateService.SyncModeFilesAsync(Path.Combine("minecraft", "instances", SelectedMode.Id ?? "default"), modeManifest);
            }

            StatusText = "Launching game...";
            await _launchService.LaunchAsync(SelectedMode, _session, 2048);
            IsBusy = false;
        }
    }
}
