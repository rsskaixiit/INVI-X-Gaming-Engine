using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Threading;
using Invix.Assets;
using Invix.Models;
using Invix.Services;

namespace Invix.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly Brush ActiveBrush = Frozen("#00FF41");
    private static readonly Brush InactiveBrush = Frozen("#5A5A5A");

    private readonly SystemMetricsService _metrics = new();
    private readonly GpuInfoService _gpu = new();
    private readonly TuningService _tuning = new();
    private readonly BackupService _backup = new();
    private readonly DispatcherTimer _timer;
    private readonly StringBuilder _log = new();

    private GpuInfo _gpuInfo = GpuInfo.Unknown;
    private int _gpuTickCounter;
    private bool _isActive;
    private bool _isBusy;
    private bool _isLogExpanded;
    private string _cpuValue = "warming up";
    private string _ramValue = "warming up";
    private string _gpuValue = "scanning adapter...";
    private string _stepSummary = "IDLE";
    private string _logText = string.Empty;
    private int _lastApplied;
    private int _lastTotal;
    private string _lastPowerPlan = "unknown";
    private string? _lastGpuSourceLogged;

    public MainViewModel()
    {
        IsAdministrator = AdminHelper.IsAdministrator();
        IsUacEnabled = AdminHelper.IsUacEnabled();
        AdminText = IsAdministrator ? "ADMIN: ELEVATED" : "ADMIN: STANDARD USER";

        foreach (var definition in _tuning.Descriptors)
        {
            TweakRows.Add(new TweakRowViewModel(definition));
        }

        ToggleCommand = new RelayCommand(ToggleGamingMode, () => !IsBusy);
        ApplyCommand = new RelayCommand(RequestApply, () => !IsBusy);
        RevertCommand = new RelayCommand(RequestRevert, () => !IsBusy);
        RestoreDefaultsCommand = new RelayCommand(RequestRestoreDefaults, () => !IsBusy);
        ElevateCommand = new RelayCommand(Elevate, () => !IsAdministrator);
        ToggleLogCommand = new RelayCommand(() => IsLogExpanded = !IsLogExpanded);
        OpenYouTubeCommand = new RelayCommand(() => OpenLink(YouTubeUrl));
        OpenDiscordCommand = new RelayCommand(() => OpenLink(DiscordUrl));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => RefreshTelemetry();

        WriteBootLog();
        _gpuInfo = _gpu.Detect();
        UpdateGpuDisplay();
    }

    public event EventHandler? ExitRequested;


    public string Logo => AsciiArt.Logo;

    public string Subtitle => Spaced(AsciiArt.Subtitle);

    public string Footer => AsciiArt.Footer;


    public const string YouTubeUrl = "https://www.youtube.com/@invi-xd";

    public const string DiscordUrl = "https://discord.gg/T5HzhQTnxe";

    public string DevCredit => "DEV</> INVI-XD !!";


    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string StatusText => IsActive ? "ACTIVE" : "INACTIVE";

    public Brush StatusBrush => IsActive ? ActiveBrush : InactiveBrush;

    public string StepSummary
    {
        get => _stepSummary;
        private set => SetProperty(ref _stepSummary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsAdministrator { get; }

    public bool IsUacEnabled { get; }

    public string AdminText { get; }

    public string ElevateLabel => IsAdministrator ? "ELEVATED" : "RUN AS ADMIN";

    public string ElevateTooltip => IsAdministrator
        ? (IsUacEnabled
            ? "INVI X is already running with administrator rights - every tuning step is available."
            : "UAC is disabled on this PC, so INVI X always runs elevated - there is nothing to elevate.")
        : "Relaunch INVI X elevated (UAC prompt) so the HKLM tuning steps can be applied.";


    public string CpuValue
    {
        get => _cpuValue;
        private set => SetProperty(ref _cpuValue, value);
    }

    public string RamValue
    {
        get => _ramValue;
        private set => SetProperty(ref _ramValue, value);
    }

    public string GpuValue
    {
        get => _gpuValue;
        private set => SetProperty(ref _gpuValue, value);
    }


    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        private set
        {
            if (SetProperty(ref _isLogExpanded, value))
            {
                OnPropertyChanged(nameof(LogToggleLabel));
            }
        }
    }

    public string LogToggleLabel => IsLogExpanded ? "\u25BE HIDE LOG" : "\u25B8 SHOW LOG";

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public ObservableCollection<TweakRowViewModel> TweakRows { get; } = new();


    public RelayCommand ToggleCommand { get; }

    public RelayCommand ApplyCommand { get; }

    public RelayCommand RevertCommand { get; }

    public RelayCommand RestoreDefaultsCommand { get; }

    public RelayCommand ElevateCommand { get; }

    public RelayCommand ToggleLogCommand { get; }

    public RelayCommand OpenYouTubeCommand { get; }

    public RelayCommand OpenDiscordCommand { get; }

    public RelayCommand ExitCommand { get; }


    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();


    private void ToggleGamingMode()
    {
        if (IsActive)
        {
            RequestRevert();
        }
        else
        {
            RequestApply();
        }
    }

    private void OpenLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppendLog($"[INFO] Opening {url}");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] Could not open {url}: {ex.Message}");
        }
    }

    private void RequestApply()
    {
        ExecuteTuning(
            "APPLY GAMING PROFILE",
            _tuning.ApplyGamingMode,
            results =>
            {
                _lastApplied = results.Count(r => r.Status == TweakStatus.Applied);
                _lastTotal = results.Count;
                _lastPowerPlan = PowerPlanService.Describe(_tuning.Descriptors
                    .First(t => t.Id == "power_plan").GamingPowerPlan ?? PowerPlanService.HighPerformanceGuid);

                IsActive = _lastApplied > 0;
                StepSummary = $"{_lastApplied}/{_lastTotal} STEPS";
                AppendLog(BuildApplySummary());
                AppendLog($"Backup written: {_tuning.LastBackupPath ?? "(none)"}");
            });
    }

    private void RequestRevert()
    {
        ExecuteTuning(
            "REVERT TO BACKUP",
            _tuning.RevertFromBackup,
            results =>
            {
                IsActive = false;
                StepSummary = "REVERTED";
                AppendLog(results.All(r => r.Status == TweakStatus.Reverted)
                    ? "Previous Windows configuration restored from backup."
                    : "Revert completed with warnings - see the steps above for details.");
            });
    }

    private void RequestRestoreDefaults()
    {
        ExecuteTuning(
            "RESTORE WINDOWS DEFAULTS",
            _tuning.RestoreDefaults,
            results =>
            {
                IsActive = false;
                _lastApplied = 0;
                StepSummary = "DEFAULTS";
                AppendLog(results.All(r => r.Status == TweakStatus.Restored)
                    ? "Windows defaults written for every step in the catalogue."
                    : "Defaults restored with warnings - see the steps above for details.");
            });
    }

    private string BuildApplySummary()
    {
        var adminNote = _lastApplied < _lastTotal && !IsAdministrator
            ? " - restart as admin for the remaining steps"
            : string.Empty;

        return $"Gaming profile engaged: {_lastApplied}/{_lastTotal} steps applied "
             + $"(power plan: {_lastPowerPlan}){adminNote}.";
    }

    private async void ExecuteTuning(string heading, Func<IReadOnlyList<TweakResult>> work, Action<IReadOnlyList<TweakResult>> onCompleted)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        AppendLog($"--- {heading} ---");

        try
        {
            var results = await Task.Run(work);
            PublishResults(results);
            onCompleted(results);
            AppendLog("--- done ---");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] {heading} aborted: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PublishResults(IReadOnlyList<TweakResult> results)
    {
        foreach (var result in results)
        {
            var row = TweakRows.FirstOrDefault(r => r.Id == result.Id);
            row?.Set(result.Status, result.Detail);
            AppendLog($"[{Tag(result.Status)}] {result.Name}");
            AppendLog($"        {result.Detail}");
        }
    }

    private static string Tag(TweakStatus status) => status switch
    {
        TweakStatus.Applied => " OK ",
        TweakStatus.Reverted => "REV ",
        TweakStatus.Restored => "DEF ",
        TweakStatus.Failed => "FAIL",
        _ => " -- ",
    };

    private void Elevate()
    {
        if (IsAdministrator)
        {
            AppendLog("[INFO] Already running with administrator rights - no elevation needed.");
            return;
        }

        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                AppendLog("[FAIL] Could not resolve the INVI X executable path.");
                AppendLog("        Launch manually: right-click INVIX.exe > Run as administrator.");
                return;
            }

            var startInfo = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            };

            Process.Start(startInfo);
            AppendLog($"[ OK ] Elevated instance launched ({exePath}). Closing this panel.");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            AppendLog("[FAIL] Elevation cancelled - the UAC prompt was declined.");
            AppendLog("        The panel stays open in standard-user mode.");
        }
        catch (Exception ex)
        {
            AppendLog($"[FAIL] Elevation failed: {ex.Message}");
            AppendLog("        Alternative: right-click INVIX.exe > Run as administrator, or run .\\run.ps1 -Elevate");
        }
    }


    private void RefreshTelemetry()
    {
        var cpu = _metrics.SampleCpuPercent();
        CpuValue = double.IsNaN(cpu) ? "warming up" : $"{cpu:0.0} %";

        var memory = _metrics.SampleMemory();
        RamValue = $"{memory.LoadPercent} %   "
                 + $"{SystemMetricsService.FormatBytes(memory.UsedBytes)} / {SystemMetricsService.FormatBytes(memory.TotalBytes)}";

        if (++_gpuTickCounter % 5 == 0)
        {
            _gpuInfo = _gpu.Detect();
            UpdateGpuDisplay();
        }
    }

    private void UpdateGpuDisplay()
    {
        var parts = new List<string>();
        if (_gpuInfo.VramBytes is { } vram)
        {
            parts.Add($"{SystemMetricsService.FormatBytes(vram)} VRAM");
        }

        if (_gpuInfo.UtilizationPercent is { } util)
        {
            parts.Add($"{util:0} % util");
        }

        if (_gpuInfo.TemperatureC is { } temp)
        {
            parts.Add($"{temp:0} C");
        }

        GpuValue = parts.Count > 0
            ? $"{_gpuInfo.Name}   |   {string.Join("  |  ", parts)}"
            : $"{_gpuInfo.Name}   |   live metrics unavailable on this adapter";

        if (!string.Equals(_lastGpuSourceLogged, _gpuInfo.Source, StringComparison.Ordinal))
        {
            _lastGpuSourceLogged = _gpuInfo.Source;
            AppendLog($"GPU source: {_gpuInfo.Source}");
        }
    }


    private void WriteBootLog()
    {
        AppendLog($"{AsciiArt.Wordmark} - Ultra Low Latency Gaming Engine v1.0");
        AppendLog("Telemetry: Win32 GetSystemTimes / GlobalMemoryStatusEx (real values).");
        AppendLog($"Backups:   {_backup.BackupDirectory}");
        AppendLog(IsAdministrator
            ? "Privilege: elevated - all tuning steps are available."
            : "Privilege: standard user - HKLM steps will be skipped. Use RUN AS ADMIN for the full profile.");
        AppendLog("F6 toggles the gaming profile. ESC exits the panel.");
        AppendLog("Note: INVI X reports measured state only and makes no latency or FPS guarantees.");
    }

    private void AppendLog(string line)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        _log.Append('[').Append(stamp).Append("] ").AppendLine(line);
        LogText = _log.ToString();
    }

    private void RaiseCommandStates()
    {
        ToggleCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
        RestoreDefaultsCommand.RaiseCanExecuteChanged();
        ElevateCommand.RaiseCanExecuteChanged();
        ToggleLogCommand.RaiseCanExecuteChanged();
        ExitCommand.RaiseCanExecuteChanged();
    }

    private static string Spaced(string text, string gap = " ")
    {
        var builder = new StringBuilder(text.Length * 2);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(gap);
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}