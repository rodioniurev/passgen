// SPDX-License-Identifier: MIT
// PassGen — генератор паролей в системном трее.
// Собирается штатным csc из .NET Framework 4 (см. build.bat), ничего устанавливать не нужно.
using System;
using System.Threading;
using System.Windows.Forms;

namespace PassGen
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string arg = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (arg == "/exit")
            {
                // «PassGen.exe /exit» закрывает уже запущенную копию (например, перед пересборкой).
                try { EventWaitHandle.OpenExisting("PassGen.Quit").Set(); }
                catch (Exception) { }
                return;
            }

            bool first;
            EventWaitHandle wake = new EventWaitHandle(false, EventResetMode.AutoReset, "PassGen.Wake", out first);
            bool autostart = arg == "/autostart";

            if (!first)
            {
                // Уже запущен: повторный запуск вручную просто открывает окно уже работающей копии.
                if (!autostart)
                {
                    Native.AllowSetForegroundWindow(-1);
                    wake.Set();
                }
                return;
            }

            EventWaitHandle quit = new EventWaitHandle(false, EventResetMode.AutoReset, "PassGen.Quit");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp(wake, quit, !autostart));
        }
    }
}
