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
    public class SimpleProgressBarConverter : System.Windows.Data.IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double value = (double)values[0];
            double min = (double)values[1];
            double max = (double)values[2];
            double width = (double)values[3];

            if (max == min)
                return 0.0;

            return (value - min) * width / (max - min);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GatewayItem
    {
        public string Provider { get; set; }
        public string IP { get; set; }
        public string DisplayName { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string _currentGateway;
        private List<GatewayItem> _gatewayItems = new List<GatewayItem>();
        private readonly string[] _requiredServices = { "KzoneClient", "KzoneSyncService" };
        private bool _servicesRunning = true; // Đổi thành true để phù hợp với AutoIt

        public MainWindow()
        {
            InitializeComponent();

            // Bỏ kiểm tra services để giống với AutoIt
            _servicesRunning = true;
            StatusText.Text = "Đang tải...";
            LoadGatewayList();
            GetCurrentGateway();
        }

        private string GetGatewayFilePath()
        {
            try
            {
                // Sử dụng file lines.txt như trong AutoIt
                string exePath = Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                return Path.Combine(exeDir, "lines.txt");
            }
            catch
            {
                return "lines.txt";
            }
        }

        private async void LoadGatewayList()
        {
            string filePath = GetGatewayFilePath();

            if (!File.Exists(filePath))
            {
                await CreateDefaultGatewayFile(filePath);
                return;
            }

            try
            {
                await Task.Run(() => {
                    var lines = File.ReadAllLines(filePath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            // Sử dụng định dạng "Gateway X - 192.168.x.x" như trong AutoIt
                            var parts = line.Split(new[] { '-' }, 2);
                            if (parts.Length == 2)
                            {
                                var provider = parts[0].Trim();
                                var ip = parts[1].Trim();

                                _gatewayItems.Add(new GatewayItem
                                {
                                    Provider = provider,
                                    IP = ip,
                                    DisplayName = line
                                });
                            }
                        }
                    }
                });

                Dispatcher.Invoke(() => {
                    GatewayComboBox.ItemsSource = _gatewayItems;
                    if (_gatewayItems.Count > 0)
                    {
                        StatusText.Text = "Sẵn sàng";
                    }
                    else
                    {
                        StatusText.Text = "Không có Gateway nào";
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    StatusText.Text = "Lỗi đọc cấu hình";
                    MessageBox.Show($"Lỗi khi đọc file gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task CreateDefaultGatewayFile(string filePath)
        {
            try
            {
                await Task.Run(() => {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        // Sử dụng định dạng giống như AutoIt
                        writer.WriteLine("Gateway 1 - 192.168.1.1");
                        writer.WriteLine("Gateway 2 - 192.168.1.2");
                        writer.WriteLine("Gateway 3 - 192.168.1.3");
                        writer.WriteLine("Gateway 4 - 192.168.1.4");
                    }
                });

                Dispatcher.Invoke(() => {
                    MessageBox.Show($"Đã tạo file cấu hình gateway mặc định tại:\n{filePath}\nVui lòng thay đổi cấu hình trong file lines.txt sau đó khởi động lại ứng dụng!",
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start("notepad.exe", filePath);
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    MessageBox.Show($"Không thể tạo file gateway: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                });
            }
        }

        private async void GetCurrentGateway()
        {
            try
            {
                string gateway = await Task.Run(() => GetDefaultGateway());

                _currentGateway = string.IsNullOrEmpty(gateway) ? "Không xác định" : gateway;
                bool found = false;
                foreach (var item in _gatewayItems)
                {
                    if (item.IP == _currentGateway)
                    {
                        CurrentGatewayText.Text = item.Provider;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    CurrentGatewayText.Text = _currentGateway;
                }
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
            // Bỏ tự động thay đổi Gateway khi chọn từ combo box để giống với AutoIt
            if (GatewayComboBox.SelectedItem != null)
            {
                var selectedGateway = GatewayComboBox.SelectedItem as GatewayItem;
                if (selectedGateway != null)
                {
                    StatusText.Text = $"Đã chọn {selectedGateway.DisplayName}";
                    // Không tự động đổi gateway ở đây nữa
                }
            }
        }

        private async void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (GatewayComboBox.SelectedItem != null)
            {
                var selectedGateway = GatewayComboBox.SelectedItem as GatewayItem;
                if (selectedGateway != null)
                {
                    await ChangeGateway(selectedGateway.IP);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn gateway để đổi", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ChangeGateway(string gatewayIP)
        {
            bool success = false;
            try
            {
                ChangeButton.IsEnabled = false;
                ExitButton.IsEnabled = false;
                GatewayComboBox.IsEnabled = false;
                StatusText.Text = "Đang thực hiện...";

                // Chạy lệnh route giống như AutoIt
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
                    // Sử dụng thời gian chờ lâu hơn để giống với AutoIt (khoảng 6 giây)
                    await AnimateProgressBarLike_AutoIt();

                    _currentGateway = gatewayIP;

                    // Tìm tên hiển thị phù hợp
                    string displayText = gatewayIP;
                    foreach (var item in _gatewayItems)
                    {
                        if (item.IP == gatewayIP)
                        {
                            displayText = item.DisplayName;
                            break;
                        }
                    }

                    CurrentGatewayText.Text = displayText;
                    StatusText.Text = "Hoàn tất";

                    MessageBox.Show($"Đã đổi sang Gateway {gatewayIP}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Bật lại các controls (không tự động đóng ứng dụng)
                    ChangeButton.IsEnabled = true;
                    ExitButton.IsEnabled = true;
                    GatewayComboBox.IsEnabled = true;
                }
                else
                {
                    StatusText.Text = "Lỗi thay đổi Gateway";
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

        private async Task AnimateProgressBarLike_AutoIt()
        {
            // Mô phỏng thời gian chờ giống như trong AutoIt
            // AutoIt sử dụng vòng lặp 0 đến 240 với thời gian chờ là 25ms mỗi bước
            ProgressBar.Maximum = 240;
            ProgressBar.Value = 0;

            for (int i = 0; i <= 240; i++)
            {
                ProgressBar.Value = i;
                await Task.Delay(25); // 25ms mỗi bước, giống AutoIt
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

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}