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

            // Kiểm tra dịch vụ nhưng không hiển thị bất kỳ thông báo nào
            _servicesRunning = CheckRequiredServices();

            // Luôn tải danh sách gateway bất kể trạng thái dịch vụ
            LoadGatewayList();
            GetCurrentGateway();
        }

        private string GetGatewayFilePath()
        {
            try
            {
                // Lấy đường dẫn thực thi hiện tại
                string exePath = Assembly.GetExecutingAssembly().Location;
                // Lấy ký tự ổ đĩa từ đường dẫn (ví dụ: "E:\")
                string driveLetter = Path.GetPathRoot(exePath);
                // Tạo đường dẫn tới file gateways.txt ở thư mục gốc của ổ đĩa
                return Path.Combine(driveLetter, "gateways.txt");
            }
            catch
            {
                // Nếu có lỗi, sử dụng đường dẫn mặc định là thư mục hiện tại
                return "gateways.txt";
            }
        }

        private bool CheckRequiredServices()
        {
            try
            {
                // Cách 1: Kiểm tra bằng ServiceController
                try
                {
                    ServiceController[] services = ServiceController.GetServices();
                    foreach (string requiredService in _requiredServices)
                    {
                        var service = services.FirstOrDefault(s => s.ServiceName.Equals(requiredService, StringComparison.OrdinalIgnoreCase));
                        if (service != null && service.Status == ServiceControllerStatus.Running)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi kiểm tra dịch vụ bằng ServiceController: {ex.Message}");
                }

                // Cách 2: Kiểm tra bằng WMI
                try
                {
                    foreach (string requiredService in _requiredServices)
                    {
                        string query = $"SELECT * FROM Win32_Service WHERE Name='{requiredService}' AND State='Running'";
                        var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2", query);
                        var results = searcher.Get();

                        if (results.Count > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi kiểm tra dịch vụ bằng WMI: {ex.Message}");
                }

                // Cách 3: Kiểm tra bằng Process
                try
                {
                    foreach (string requiredService in _requiredServices)
                    {
                        Process[] processes = Process.GetProcessesByName(requiredService);
                        if (processes.Length > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi kiểm tra bằng Process: {ex.Message}");
                }

                // Cách 4: Sử dụng lệnh CMD để kiểm tra dịch vụ
                try
                {
                    foreach (string requiredService in _requiredServices)
                    {
                        string output = ExecuteCommandWithOutput($"sc query {requiredService}");
                        if (output.Contains("RUNNING"))
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi kiểm tra dịch vụ bằng SC: {ex.Message}");
                }

                // Không tìm thấy dịch vụ nào đang chạy
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi tổng thể khi kiểm tra dịch vụ: {ex.Message}");
                return false;
            }
        }

        private string ExecuteCommandWithOutput(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = new Process { StartInfo = processInfo };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
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
                        // Định dạng đơn giản: Provider IP
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

                        // Tìm thông tin bổ sung cho gateway hiện tại
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
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
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
                StatusText.Text = $"Đã chọn {selectedGateway.Name}";

                // Chỉ thực hiện đổi gateway nếu dịch vụ đang chạy, không hiển thị thông báo nếu không
                if (_servicesRunning)
                {
                    // Tự động thực hiện đổi gateway khi chọn
                    AutoChangeGateway(selectedGateway.IP);
                }
                else
                {
                    // Không hiển thị bất kỳ thông báo nào về dịch vụ
                    // Chỉ vô hiệu hóa nút thay đổi
                    ChangeButton.IsEnabled = false;
                }
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

                    // Tìm provider cho gateway đã đổi
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

                    // Tự động đóng ứng dụng sau khi hoàn thành (không hiển thị MessageBox)
                    await Task.Delay(500); // Chờ nửa giây để người dùng thấy thông báo "Hoàn tất"
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
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
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
            if (!_servicesRunning)
            {
                // Không hiển thị thông báo về dịch vụ, chỉ đơn giản là không làm gì cả
                return;
            }

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