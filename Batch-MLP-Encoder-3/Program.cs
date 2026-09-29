using System;
using System.Text;
using System.Windows.Forms;

namespace SadPencil.BatchMLPEncoder3 {
    static class Program {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main() {
            //GB2312(CP936)、CP949 等传统代码页在 .NET Core 之后不再内置。
            //必须在任何 Encoding.GetEncoding 调用之前注册，否则 LegacyTextEncoding 会失败。
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //高 DPI：旧版靠 app.config 的 EnableWindowsFormsHighDpiAutoResizing=true 加
            //app.manifest 里的 <dpiAware>true</dpiAware>（两项都已删除），.NET 10 下改为在这里显式设置。
            //必须在创建任何窗口之前调用。
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            //.NET Framework 4.6 的注册表检测已经删除：
            //.NET 10 程序在启动前就由宿主完成了运行时检查，改由“缺少桌面运行时”的错误对话框提示。
            if ((new LanguageForm()).ShowDialog() == DialogResult.OK) {
                Application.Run(new MainForm());
            }
        }

    }
}
