using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GWChanger
{
    public partial class MainWindow : Window
    {
        private string _currentGateway;
        private List<dynamic> _gatewayItems = new List<dynamic>();

        public MainWindow()
        {
            InitializeComponent();
            LoadGatewayList();
            GetCurrentGateway();
        }

        private void LoadGatewayList()
        {
            string filePath = "gateways.txt";

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
                        // Hỗ trợ cả định dạng cũ và mới
                        if (line.Contains("-"))
                        {
                            var parts = line.Split(new[] { '-' }, 2);
                            if (parts.Length == 2)
                            {
                                var name = parts[0].Trim();
                                var ip = parts[1].Trim();
                                _gatewayItems.Add(new
                                {
                                    Name = name,
                                    IP = ip,
                                    Provider = "",
                                    Speed = "",
                                    DisplayName = $"{ip} / {name}"
                                });
                            }
                        }
                        else if (line.Contains("/"))
                        {
                            var parts = line.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var ip = parts[0].Trim();
                                var name = parts[1].Trim();
                                var provider = parts.Length > 2 ? parts[2].Trim() : "";
                                var speed = parts.Length > 3 ? parts[3].Trim() : "";

                                string displayName = ip;
                                if (!string.IsNullOrEmpty(name))
                                    displayName += $" / {name}";
                                if (!string.IsNullOrEmpty(provider))
                                    displayName += $" / {provider}";
                                if (!string.IsNullOrEmpty(speed))
                                    displayName += $" / {speed}";

                                _gatewayItems.Add(new
                                {
                                    Name = name,
                                    IP = ip,
                                    Provider = provider,
                                    Speed = speed,
                                    DisplayName = displayName
                                });
                            }
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
                    writer.WriteLine("192.168.1.1 / Gateway 1 / Viettel / 300Mbps");
                    writer.WriteLine("192.168.1.2 / Gateway 2 / VNPT / 200Mbps");
                    writer.WriteLine("192.168.1.3 / Gateway 3 / FPT / 100Mbps");
                    writer.WriteLine("192.168.1.4 / Gateway 4 / CMC / 150Mbps");
                }

                MessageBox.Show("Đã tạo file cấu hình gateway mặc định.\nVui lòng thay đổi cấu hình trong file gateways.txt sau đó khởi động lại ứng dụng!",
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
                        CurrentGatewayText.Text = _currentGateway;

                        // Tìm thông tin bổ sung cho gateway hiện tại
                        foreach (dynamic item in _gatewayItems)
                        {
                            if (item.IP == _currentGateway)
                            {
                                CurrentGatewayText.Text = item.DisplayName;
                                break;
                            }
                        }
                    });
                });
            }
            catch (Exception)
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
            if (GatewayComboBox.SelectedItem != null)
            {
                dynamic selectedGateway = GatewayComboBox.SelectedItem;
                // Chỉ thông báo, không thay đổi gateway ngay
                StatusText.Text = $"Đã chọn {selectedGateway.Name}";
            }
        }

        private async Task ChangeGateway(string gatewayIP)
        {
            try
            {
                ChangeButton.IsEnabled = false;
                StatusText.Text = "Đang thực hiện...";

                bool success = await Task.Run(() =>
                {
                    try
                    {
                        // Xóa gateway mặc định hiện tại
                        ExecuteCommand("route delete 0.0.0.0 mask 0.0.0.0");

                        // Thêm gateway mới
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

                    // Cập nhật hiển thị với thông tin đầy đủ nếu có
                    dynamic selectedGateway = GatewayComboBox.SelectedItem;
                    CurrentGatewayText.Text = selectedGateway.DisplayName;

                    StatusText.Text = "Hoàn tất";
                    MessageBox.Show($"Đã đổi sang Gateway {gatewayIP}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Lỗi";
                    MessageBox.Show("Không thể thay đổi Gateway. Hãy chắc chắn bạn đang chạy với quyền Administrator.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Lỗi";
                MessageBox.Show($"Lỗi khi đổi gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ChangeButton.IsEnabled = true;
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

        private async void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (GatewayComboBox.SelectedItem != null)
            {
                dynamic selectedGateway = GatewayComboBox.SelectedItem;
                await ChangeGateway(selectedGateway.IP);
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