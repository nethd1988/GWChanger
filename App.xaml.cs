using System;
using System.Windows;

namespace GWChanger
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Kiểm tra quyền admin
            if (!IsRunAsAdmin())
            {
                MessageBox.Show("Ứng dụng cần quyền Administrator để thay đổi gateway.\nVui lòng chạy ứng dụng với quyền Administrator.",
                    "Yêu cầu quyền Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }
        }

        private bool IsRunAsAdmin()
        {
            // Kiểm tra xem ứng dụng có đang chạy với quyền Administrator hay không
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
    }
}