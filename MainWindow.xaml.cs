using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GWChanger
{
    public class ProgressMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length < 4 ||
                !(values[0] is double) ||
                !(values[1] is double) ||
                !(values[2] is double) ||
                !(values[3] is double))
                return 0d;

            double value = (double)values[0];
            double minimum = (double)values[1];
            double maximum = (double)values[2];
            double width = (double)values[3];

            if (maximum == minimum)
                return 0d;

            double percent = (value - minimum) / (maximum - minimum);
            return percent * (width - 2);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        private string _currentGateway;
        private readonly List<GatewayItem> _gatewayItems = new List<GatewayItem>();

        public MainWindow()
        {
            InitializeComponent();
            SetupWindow();
            LoadGatewayList();
            GetCurrentGateway();
        }

        private void SetupWindow()
        {
            // Set window style
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Set logo click event
            LogoImage.MouseDown += (s, e) => Process.Start("http://thanhdiep.com");
        }

        private void LoadGatewayList()
        {
            string filePath = "lines.txt";

            if (!File.Exists(filePath))
            {
                CreateDefaultGatewayFile(filePath);
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var parts = line.Split(new[] { '-' }, 2);
                        if (parts.Length == 2)
                        {
                            var name = parts[0].Trim();
                            var ip = parts[1].Trim();
                            _gatewayItems.Add(new GatewayItem { Name = name, IP = ip });
                        }
                    }
                }

                GatewayComboBox.ItemsSource = _gatewayItems;
                GatewayComboBox.DisplayMemberPath = "DisplayName";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đọc file gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateDefaultGatewayFile(string filePath)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("Gateway 1 - 192.168.1.1");
                    writer.WriteLine("Gateway 2 - 192.168.1.2");
                    writer.WriteLine("Gateway 3 - 192.168.1.3");
                    writer.WriteLine("Gateway 4 - 192.168.1.4");
                }

                MessageBox.Show("Đã tạo file cấu hình gateway mặc định.\nVui lòng thay đổi cấu hình trong file lines.txt sau đó khởi động lại ứng dụng!",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                Process.Start("notepad.exe", filePath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tạo file gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void GetCurrentGateway()
        {
            try
            {
                // Lấy gateway bằng cách chạy lệnh ipconfig
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c ipconfig",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Phân tích output để tìm gateway mặc định
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Default Gateway") || line.Contains("Gateway mặc định"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            _currentGateway = parts[1].Trim();
                            CurrentGatewayText.Text = _currentGateway;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(_currentGateway))
                {
                    _currentGateway = "Không xác định";
                    CurrentGatewayText.Text = _currentGateway;
                }
            }
            catch (Exception)
            {
                _currentGateway = "Không xác định";
                CurrentGatewayText.Text = _currentGateway;
            }
        }

        private async void GatewayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GatewayComboBox.SelectedItem is GatewayItem selectedGateway)
            {
                try
                {
                    ChangeButton.IsEnabled = false;
                    StatusText.Text = "Đang thực hiện...";

                    await Task.Run(() =>
                    {
                        try
                        {
                            // Delete existing default route
                            ExecuteCommand("route delete 0.0.0.0 mask 0.0.0.0");

                            // Add new default route
                            ExecuteCommand($"route -p add 0.0.0.0 mask 0.0.0.0 {selectedGateway.IP}");
                        }
                        catch (Exception ex)
                        {
                            // Bắt lỗi trong thread riêng và chuyển nó ra ngoài
                            Dispatcher.Invoke(() => {
                                MessageBox.Show($"Lỗi khi thực thi lệnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            throw;
                        }
                    });

                    await AnimateProgressBar();

                    _currentGateway = selectedGateway.IP;
                    CurrentGatewayText.Text = _currentGateway;

                    MessageBox.Show($"Đã đổi sang Gateway {selectedGateway.IP}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusText.Text = "Hoàn tất";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đổi gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "Lỗi";
                }
                finally
                {
                    ChangeButton.IsEnabled = true;
                    ProgressBar.Value = 0;
                }
            }
        }

        private void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas" // Run as administrator
            };

            var process = new Process { StartInfo = processInfo };
            process.Start();
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                throw new Exception($"Lỗi thực thi lệnh: {error}");
            }
        }

        private async Task AnimateProgressBar()
        {
            for (int i = 0; i <= 100; i++)
            {
                // Dispatch to UI thread
                await Dispatcher.InvokeAsync(() => {
                    ProgressBar.Value = i;
                });
                await Task.Delay(20);
            }
        }

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (GatewayComboBox.SelectedItem is GatewayItem selectedGateway)
            {
                GatewayComboBox_SelectionChanged(GatewayComboBox, null);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn gateway để đổi", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

    public class GatewayItem
    {
        public string Name { get; set; }
        public string IP { get; set; }
        public string DisplayName => $"{Name} - {IP}";
    }
}