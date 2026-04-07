using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SyslogAgent.Collectors;
using SyslogAgent.Desktop.Data;
using SyslogAgent.Desktop.Models;
using SyslogAgent.Desktop.Services;

namespace SyslogAgent.Desktop;

public partial class DashboardWindow : Window
{
    private AgentService _service;
    private readonly ObservableCollection<LogEntryVm> _logEntries = new();
    private readonly DispatcherTimer _uptimeTimer;
    private bool _isStopping;
    private bool _isRunning = true;
    private bool _isFilterOn;
    private bool _forceClose;
    private const int MaxLogEntries = 5000;

    // Store connection details for restart
    private readonly string _host;
    private readonly int _port;
    private readonly string _protocol;

    public DashboardWindow(AgentService service)
    {
        InitializeComponent();
        _service = service;
        _isFilterOn = service.Options.Collectors.ApplyFilter;
        _host = service.Options.SyslogServer.Host;
        _port = service.Options.SyslogServer.Port;
        _protocol = service.Options.SyslogServer.Protocol;

        LogGrid.ItemsSource = _logEntries;

        SetupConnectionInfo();
        UpdateFilterUI();
        SetupFilterChannels();
        SetupStatusBar();

        // Subscribe to events
        _service.LogReceived += OnLogReceived;
        _service.MetricsUpdated += OnMetricsUpdated;

        // Uptime timer
        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) =>
        {
            TxtUptime.Text = _service.Uptime.ToString(@"hh\:mm\:ss");
        };
        _uptimeTimer.Start();
    }

    private void SetupConnectionInfo()
    {
        TxtConnection.Text = $"{_protocol}://{_host}:{_port}";
    }

    private void UpdateFilterUI()
    {
        if (_isFilterOn)
        {
            FilterDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            TxtFilterStatus.Text = "Filter ON";
            BtnToggleFilter.Content = "Turn OFF";
            BtnToggleFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
            BtnToggleFilter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            BtnToggleFilter.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA"));
        }
        else
        {
            FilterDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            TxtFilterStatus.Text = "Filter OFF";
            BtnToggleFilter.Content = "Turn ON";
            BtnToggleFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
            BtnToggleFilter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0369A1"));
            BtnToggleFilter.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE6FD"));
        }

        TxtCollectorCount.Text = $"{_service.Registry.All.Count} collectors active";
    }

    private void SetupFilterChannels()
    {
        var channels = new List<FilterChannelModel>();
        foreach (var (channelName, events) in EventDescriptions.ByChannel)
        {
            var eventIds = events.Select(kvp => new EventIdModel
            {
                Id = kvp.Key,
                Description = kvp.Value
            }).OrderBy(e => e.Id).ToList();

            channels.Add(new FilterChannelModel
            {
                ChannelName = channelName.Replace("Microsoft-Windows-", "").Replace("/Operational", ""),
                EventIds = eventIds
            });
        }

        ChannelList.ItemsSource = channels;
    }

    private void SetupStatusBar()
    {
        var opts = _service.Options;
        TxtActiveCollectors.Text = _service.Registry.All.Count.ToString();

        var modes = new List<string>();
        if (opts.EnableRealtime) modes.Add("Realtime");
        if (opts.EnableBatch) modes.Add("Batch");
        TxtMode.Text = string.Join(" + ", modes);
    }

    private async void BtnToggleFilter_Click(object sender, RoutedEventArgs e)
    {
        if (_isStopping) return;

        BtnToggleFilter.IsEnabled = false;
        BtnToggleFilter.Content = "Restarting...";

        try
        {
            // Stop current service
            _service.LogReceived -= OnLogReceived;
            _service.MetricsUpdated -= OnMetricsUpdated;
            await _service.StopAsync();
            _service.Dispose();

            // Toggle filter
            _isFilterOn = !_isFilterOn;

            // Create new service with toggled filter
            _service = new AgentService(_host, _port, _protocol, _isFilterOn);
            await _service.StartAsync();

            // Resubscribe
            _service.LogReceived += OnLogReceived;
            _service.MetricsUpdated += OnMetricsUpdated;

            UpdateFilterUI();
            SetupStatusBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to restart with new filter: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnToggleFilter.IsEnabled = true;
        }
    }

    private void OnLogReceived(LogEntryVm entry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _logEntries.Insert(0, entry);

            // Cap the list to prevent memory growth
            while (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveAt(_logEntries.Count - 1);

            // Auto-scroll to top
            if (ChkAutoScroll.IsChecked == true && _logEntries.Count > 0)
            {
                LogGrid.ScrollIntoView(_logEntries[0]);
            }
        }, DispatcherPriority.Background);
    }

    private void OnMetricsUpdated(long sent, long errors)
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtSent.Text = sent.ToString("N0");
            TxtErrors.Text = errors.ToString("N0");
        }, DispatcherPriority.Background);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_isStopping) return;

        if (!_isRunning)
        {
            // Start
            _isRunning = true;
            BtnStop.IsEnabled = false;
            BtnStop.Content = "Starting...";
            BtnToggleFilter.IsEnabled = false;

            try
            {
                _service = new AgentService(_host, _port, _protocol, _isFilterOn);
                await _service.StartAsync();

                _service.LogReceived += OnLogReceived;
                _service.MetricsUpdated += OnMetricsUpdated;

                _uptimeTimer.Start();

                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                StatusText.Text = "RUNNING";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                BtnStop.Content = "Stop";
                BtnToggleFilter.IsEnabled = true;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                MessageBox.Show($"Failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnStop.Content = "Start";
            }
            finally
            {
                BtnStop.IsEnabled = true;
            }
            return;
        }

        // Stop
        _isStopping = true;
        _isRunning = false;

        BtnStop.IsEnabled = false;
        BtnStop.Content = "Stopping...";
        BtnToggleFilter.IsEnabled = false;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        StatusText.Text = "STOPPING";
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

        try
        {
            _uptimeTimer.Stop();
            _service.LogReceived -= OnLogReceived;
            _service.MetricsUpdated -= OnMetricsUpdated;
            await _service.StopAsync();
            _service.Dispose();
        }
        catch { }

        _isStopping = false;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        StatusText.Text = "STOPPED";
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        BtnStop.Content = "Start";
        BtnStop.IsEnabled = true;
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // If already force-closed (second invocation after cleanup), allow it
        if (_forceClose) return;

        // Cancel this close event — run async cleanup first, then re-invoke Close()
        e.Cancel = true;

        if (!_isStopping && _isRunning)
        {
            _isStopping = true;
            _isRunning = false;
            _uptimeTimer.Stop();
            _service.LogReceived -= OnLogReceived;
            _service.MetricsUpdated -= OnMetricsUpdated;

            try
            {
                await _service.StopAsync();
                _service.Dispose();
            }
            catch { }
        }

        _forceClose = true;
        Close(); // Re-invoke; _forceClose bypasses the cancellation above
    }
}
