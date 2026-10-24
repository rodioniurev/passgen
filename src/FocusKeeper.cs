// SPDX-License-Identifier: MIT
using System;
using System.Text;

namespace PassGen
{
    // ───────────────────────── фокус ─────────────────────────

    // Помнит последнее «настоящее» окно, где работал пользователь, и умеет надёжно отдавать фокус окнам.
    // Клик по значку в трее передаёт фокус панели задач, и после закрытия окна PassGen Windows не всегда
    // возвращает его программе — поэтому возвращаем сами.
    static class FocusKeeper
    {
        const uint EventSystemForeground = 0x0003;
        const uint OutOfContext = 0x0000, SkipOwnProcess = 0x0002;
        static readonly Native.WinEventProc Proc = OnForeground;     // ссылка нужна, иначе делегат соберёт GC
        static readonly string[] ShellClasses =
        {
            "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TrayNotifyWnd", "Progman", "WorkerW",
            "#32768", "ForegroundStaging", "MultitaskingViewFrame", "XamlExplorerHostIslandWindow",
            "Windows.UI.Core.CoreWindow", "TaskListThumbnailWnd", "SysShadow"
        };
        static IntPtr hook, last;

        public static void Start()
        {
            if (hook != IntPtr.Zero) return;
            hook = Native.SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, Proc, 0, 0, OutOfContext | SkipOwnProcess);
            Remember(Native.GetForegroundWindow());
        }

        public static void Stop()
        {
            if (hook == IntPtr.Zero) return;
            Native.UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }

        static void OnForeground(IntPtr h, uint eventType, IntPtr hwnd, int idObject, int idChild, uint threadId, uint time)
        {
            Remember(hwnd);
        }

        static void Remember(IntPtr h)
        {
            if (h != IntPtr.Zero && !IsShell(h)) last = h;
        }

        static bool IsShell(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(64);
            Native.GetClassName(h, sb, 64);
            string c = sb.ToString();
            foreach (string s in ShellClasses)
                if (c == s) return true;
            return false;
        }

        static bool Usable(IntPtr h)
        {
            return h != IntPtr.Zero && Native.IsWindow(h) && Native.IsWindowVisible(h) && !Native.IsIconic(h);
        }

        // Окно программы, где пользователь был до PassGen (если оно ещё открыто и не свёрнуто).
        public static IntPtr Last
        {
            get { return Usable(last) ? last : IntPtr.Zero; }
        }

        // Отдать окну фокус, даже если Windows считает, что наш процесс не вправе этого делать:
        // подключаемся к потоку текущего активного окна — тогда запрет не действует.
        public static void Activate(IntPtr h)
        {
            if (!Usable(h)) return;
            if (Native.GetForegroundWindow() == h) return;
            Native.SetForegroundWindow(h);
            if (Native.GetForegroundWindow() == h) return;

            IntPtr fg = Native.GetForegroundWindow();
            uint unused;
            uint fgThread = fg == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(fg, out unused);
            uint me = Native.GetCurrentThreadId();
            bool attached = fgThread != 0 && fgThread != me && Native.AttachThreadInput(me, fgThread, true);
            try
            {
                Native.SetForegroundWindow(h);
                Native.BringWindowToTop(h);
            }
            finally
            {
                if (attached) Native.AttachThreadInput(me, fgThread, false);
            }
            if (Native.GetForegroundWindow() != h) Native.SwitchToThisWindow(h, true);
        }
    }
}
