using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using SyslogAgent.Desktop.Services;

namespace SyslogAgent.Desktop;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        var host = TxtHost.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            ShowError("Please enter a server IP or hostname.");
            return;
        }

        if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            ShowError("Port must be a number between 1 and 65535.");
            return;
        }

        var protocol = ((System.Windows.Controls.ComboBoxItem)CmbProtocol.SelectedItem).Content.ToString()!;
        var applyFilter = ChkFilter.IsChecked == true;

        BtnStart.IsEnabled = false;
        BtnStart.Content = "Starting...";
        TxtError.Visibility = Visibility.Collapsed;

        try
        {
            var service = new AgentService(host, port, protocol, applyFilter);
            await service.StartAsync();

            var dashboard = new DashboardWindow(service);
            dashboard.Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to start: {ex.Message}");
            BtnStart.IsEnabled = true;
            BtnStart.Content = "Start Monitoring";
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }

    private void TxtPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
    }
}
