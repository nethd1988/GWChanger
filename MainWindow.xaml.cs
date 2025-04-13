using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

namespace GWChanger
{
    public partial class MainWindow : Window
    {
        private string _currentGateway;
        private List<dynamic> _gatewayItems = new List<dynamic>();
        private readonly string[] _requiredServices = { "KzoneClient", "KzoneSyncService" };
        private bool _servicesRunning = false;

        public MainWindow()
        {
            InitializeComponent();

            // Kiểm tra trạng thái dịch vụ
            _servicesRunning = CheckRequiredServices();

            if (!_servicesRunning)
            {
                // Nếu cả hai dịch vụ đều không chạy, vô hiệu hóa điều khiển và đóng sau 2 giây
                StatusText.Text = "";
                ChangeButton.IsEnabled = false;
                GatewayComboBox.IsEnabled = false;
                Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                });
                return;
            }

            // Nếu ít nhất một dịch vụ đang chạy, tiếp tục khởi tạo
            LoadGatewayList();
            GetCurrentGateway();
        }

        private string GetGatewayFilePath()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string driveLetter = Path.GetPathRoot(exePath);
                return Path.Combine(driveLetter, "gateways.txt");
            }
            catch
            {
                return "gateways.txt";
            }
        }

        private bool CheckRequiredServices()
        {
            try
            {
                ServiceController[] services = ServiceController.GetServices();
                bool anyServiceRunning = false;

                foreach (string requiredService in _requiredServices)
                {
                    var service = services.FirstOrDefault(s => s.ServiceName.Equals(requiredService, StringComparison.OrdinalIgnoreCase));
                    if (service != null && service.Status == ServiceControllerStatus.Running)
                    {
                        anyServiceRunning = true;
                        break;
                    }
                }

                return anyServiceRunning;
            }
            catch
            {
                return false;
            }
        }

        private void LoadGatewayList()
        {
            string filePath = GetGatewayFilePath();

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
                        var parts = line.Split(new[] { ' ' }, 2);
                        if (parts.Length == 2)
                        {
                            var provider = parts[0].Trim();
                            var ip = parts[1].Trim();

                            _gatewayItems.Add(new
                            {
                                Name = $"{provider} {ip}",
                                IP = ip,
                                Provider = provider,
                                DisplayName = $"{provider} {ip}"
                            });
                        }
                    }
                }

                GatewayComboBox.ItemsSource = _gatewayItems;
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
                    writer.WriteLine("Viettel 192.168.1.1");
                    writer.WriteLine("VNPT 192.168.1.2");
                    writer.WriteLine("FPT 192.168.1.3");
                    writer.WriteLine("CMC 192.168.1.4");
                }

                MessageBox.Show($"Đã tạo file cấu hình gateway mặc định tại:\n{filePath}\nVui lòng thay đổi cấu hình trong file gateways.txt sau đó khởi động lại ứng dụng!",
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

        private async void GetCurrentGateway()
        {
            try
            {
                await Task.Run(() =>
                {
                    string gateway = GetDefaultGateway();
                    Dispatcher.Invoke(() =>
                    {
                        _currentGateway = string.IsNullOrEmpty(gateway) ? "Không xác định" : gateway;
                        bool found = false;
                        foreach (dynamic item in _gatewayItems)
                        {
                            if (item.IP == _currentGateway)
                            {
                                CurrentGatewayText.Text = $"{item.Provider} {item.IP}";
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            CurrentGatewayText.Text = _currentGateway;
                        }
                    });
                });
            }
            catch
            {
                _currentGateway = "Không xác định";
                CurrentGatewayText.Text = _currentGateway;
            }
        }

        private string GetDefaultGateway()
        {
            try
            {
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

                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Default Gateway") || line.Contains("Gateway mặc định"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            return parts[1].Trim();
                        }
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void GatewayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_servicesRunning) return;

            if (GatewayComboBox.SelectedItem != null)
            {
                dynamic selectedGateway = GatewayComboBox.SelectedItem;
                StatusText.Text = $"Đã chọn {selectedGateway.Name}";
                AutoChangeGateway(selectedGateway.IP);
            }
        }

        private async void AutoChangeGateway(string gatewayIP)
        {
            bool success = false;
            try
            {
                ChangeButton.IsEnabled = false;
                ExitButton.IsEnabled = false;
                GatewayComboBox.IsEnabled = false;
                StatusText.Text = "Đang thực hiện...";

                success = await Task.Run(() =>
                {
                    try
                    {
                        ExecuteCommand("route delete 0.0.0.0 mask 0.0.0.0");
                        ExecuteCommand($"route -p add 0.0.0.0 mask 0.0.0.0 {gatewayIP}");
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (success)
                {
                    await AnimateProgressBar();
                    _currentGateway = gatewayIP;

                    string displayText = gatewayIP;
                    foreach (dynamic item in _gatewayItems)
                    {
                        if (item.IP == gatewayIP)
                        {
                            displayText = item.DisplayName;
                            break;
                        }
                    }

                    CurrentGatewayText.Text = displayText;
                    StatusText.Text = "Hoàn tất";
                    await Task.Delay(500);
                    Application.Current.Shutdown();
                }
                else
                {
                    StatusText.Text = "Lỗi";
                    MessageBox.Show("Không thể thay đổi Gateway. Hãy chắc chắn bạn đang chạy với quyền Administrator.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    ChangeButton.IsEnabled = true;
                    ExitButton.IsEnabled = true;
                    GatewayComboBox.IsEnabled = true;
                    ProgressBar.Value = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Lỗi";
                MessageBox.Show($"Lỗi khi đổi gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                ChangeButton.IsEnabled = true;
                ExitButton.IsEnabled = true;
                GatewayComboBox.IsEnabled = true;
                ProgressBar.Value = 0;
            }
        }

        private void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = processInfo };
            process.Start();
            process.WaitForExit();

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
                ProgressBar.Value = i;
                await Task.Delay(20);
            }
        }

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_servicesRunning) return;

            if (GatewayComboBox.SelectedItem != null)
            {
                dynamic selectedGateway = GatewayComboBox.SelectedItem;
                AutoChangeGateway(selectedGateway.IP);
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
}