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
        private bool _servicesRunning = false;

        public MainWindow()
        {
            InitializeComponent();

            StatusText.Text = "Đang kiểm tra...";

            // Kiểm tra services trước
            if (!CheckRequiredServices())
            {
                // Nếu không có services, tự thoát ứng dụng
                Application.Current.Shutdown();
                return;
            }

            StatusText.Text = "Đang tải...";
            LoadGatewayList();
            GetCurrentGateway();
        }

        private bool CheckRequiredServices()
        {
            try
            {
                foreach (var serviceName in _requiredServices)
                {
                    try
                    {
                        var service = new ServiceController(serviceName);
                        if (service.Status != ServiceControllerStatus.Stopped)
                        {
                            _servicesRunning = true;
                            return true; // Tìm thấy ít nhất một service đang chạy
                        }
                    }
                    catch
                    {
                        // Service không tồn tại, tiếp tục kiểm tra service khác
                    }
                }

                // Không tìm thấy service nào
                return false;
            }
            catch
            {
                // Xử lý lỗi im lặng
                return false;
            }
        }

        private string GetGatewayFilePath()
        {
            try
            {
                // Lấy ổ đĩa gốc nơi ứng dụng đang chạy
                string exePath = Assembly.GetExecutingAssembly().Location;
                string driveName = Path.GetPathRoot(exePath);

                // Tạo đường dẫn đến file gateways.txt trong thư mục gốc
                return Path.Combine(driveName, "gateways.txt");
            }
            catch
            {
                // Nếu có lỗi, mặc định về thư mục gốc ổ C
                return @"C:\gateways.txt";
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
                            // Sử dụng định dạng mới "Viettel 192.168.1.1"
                            var parts = line.Split(new[] { ' ' }, 2);
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
            catch
            {
                Dispatcher.Invoke(() => {
                    StatusText.Text = "Lỗi đọc cấu hình";
                });
            }
        }

        private async Task CreateDefaultGatewayFile(string filePath)
        {
            try
            {
                await Task.Run(() => {
                    // Đảm bảo thư mục tồn tại
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        // Sử dụng định dạng nhà mạng theo yêu cầu
                        writer.WriteLine("Viettel 192.168.1.1");
                        writer.WriteLine("VNPT 192.168.1.2");
                        writer.WriteLine("FPT 192.168.1.3");
                        writer.WriteLine("CMC 192.168.1.4");
                    }
                });

                // Mở notepad im lặng không hiện thông báo
                Process.Start("notepad.exe", filePath);

                // Đợi một chút để notepad mở xong trước khi đóng ứng dụng
                await Task.Delay(1000);
                Application.Current.Shutdown();
            }
            catch
            {
                Application.Current.Shutdown();
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

        private async void GatewayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Tự động thay đổi Gateway khi chọn từ combobox
            if (GatewayComboBox.SelectedItem != null)
            {
                var selectedGateway = GatewayComboBox.SelectedItem as GatewayItem;
                if (selectedGateway != null)
                {
                    StatusText.Text = $"Đang thay đổi sang {selectedGateway.Provider}";
                    await ChangeGateway(selectedGateway.IP);
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
                StatusText.Text = "Vui lòng chọn gateway";
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
                    await AnimateProgressBarLike_AutoIt();
                    _currentGateway = gatewayIP;

                    string providerName = gatewayIP;
                    foreach (var item in _gatewayItems)
                    {
                        if (item.IP == gatewayIP)
                        {
                            providerName = item.Provider;
                            break;
                        }
                    }

                    CurrentGatewayText.Text = providerName;
                    StatusText.Text = "Hoàn tất";

                    // Tự động thoát ứng dụng sau khi hoàn thành, không hiện thông báo
                    await Task.Delay(500); // Đợi một chút để người dùng thấy "Hoàn tất"
                    Application.Current.Shutdown();
                }
                else
                {
                    StatusText.Text = "Lỗi thay đổi Gateway";
                    ChangeButton.IsEnabled = true;
                    ExitButton.IsEnabled = true;
                    GatewayComboBox.IsEnabled = true;
                    ProgressBar.Value = 0;
                }
            }
            catch
            {
                StatusText.Text = "Lỗi";
                ChangeButton.IsEnabled = true;
                ExitButton.IsEnabled = true;
                GatewayComboBox.IsEnabled = true;
                ProgressBar.Value = 0;
            }
        }

        private async Task AnimateProgressBarLike_AutoIt()
        {
            ProgressBar.Maximum = 240;
            ProgressBar.Value = 0;

            for (int i = 0; i <= 240; i++)
            {
                ProgressBar.Value = i;
                await Task.Delay(25); // 25ms mỗi bước
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