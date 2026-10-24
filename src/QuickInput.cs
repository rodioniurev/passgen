// SPDX-License-Identifier: MIT
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PassGen
{
    // ───────────────────────── быстрый ввод: Right Alt+P ─────────────────────────

    static class Win
    {
        public static uint Pid(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return 0;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

        public static uint ForegroundPid()
        {
            return Pid(Native.GetForegroundWindow());
        }

        // Заголовок окна для описания записи: без переводов строки и не длиннее 120 знаков.
        public static string Title(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            StringBuilder sb = new StringBuilder(256);
            Native.GetWindowText(hwnd, sb, 256);
            string t = sb.ToString().Replace('\r', ' ').Replace('\n', ' ').Trim();
            return t.Length > 120 ? t.Substring(0, 120).TrimEnd() : t;
        }
    }

    // Печатает текст «клавишами»: каждый знак уходит как Unicode-символ (KEYEVENTF_UNICODE), поэтому ввод не зависит
    // от раскладки (русская, английская — неважно) и работает на сайтах, где вставка из буфера запрещена.
    static class Typer
    {
        public const uint KeyUp = 0x0002, KeyUnicode = 0x0004;
        static readonly int Size = Marshal.SizeOf(typeof(Native.INPUT));
        // Эти клавиши не должны быть зажаты, иначе приложение увидит не «знак», а Alt+знак или Ctrl+знак.
        static readonly int[] Modifiers = { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C };

        public static Native.INPUT Key(ushort vk, ushort scan, uint flags)
        {
            Native.INPUT i = new Native.INPUT();
            i.type = 1;                                         // INPUT_KEYBOARD
            i.u.ki.vk = vk;
            i.u.ki.scan = scan;
            i.u.ki.flags = flags;
            i.u.ki.extraInfo = KeyHook.OwnTag;                  // хук не должен реагировать на наши собственные «нажатия»
            return i;
        }

        // Готовит текст из буфера к посимвольному вводу: пробелы и переводы строк по краям убираем; переводы строк
        // и табуляции внутри заменяем пробелом (настоящий Enter мог бы отправить форму); прочие управляющие знаки убираем.
        public static string Clean(string text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in text.Trim())
            {
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                }
                else if (!char.IsControl(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        // Ждёт, пока физически отпустят Alt/Ctrl/Shift/Win. false — если за timeoutMs так и не отпустили.
        public static bool WaitReleased(int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (true)
            {
                bool held = false;
                foreach (int vk in Modifiers)
                {
                    if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0) { held = true; break; }
                }
                if (!held)
                {
                    Thread.Sleep(30);                           // даём приложению заметить, что клавиши отпущены
                    return true;
                }
                if (sw.ElapsedMilliseconds > timeoutMs) return false;
                Thread.Sleep(10);
            }
        }

        // 0 — напечатано; 1 — система не приняла ввод (например, окно запущено от администратора);
        // 2 — активным стало окно другой программы: остаток пароля не печатаем, чтобы он не попал не туда.
        public static int TypeText(string text, int delayMs, uint targetPid)
        {
            Native.timeBeginPeriod(1);                          // иначе пауза в 8 мс на деле 15,6 мс — шаг таймера Windows
            try
            {
                foreach (char c in text)
                {
                    if (Win.ForegroundPid() != targetPid) return 2;
                    Native.INPUT[] i = new Native.INPUT[2];
                    i[0] = Key(0, c, KeyUnicode);
                    i[1] = Key(0, c, KeyUnicode | KeyUp);
                    if (Native.SendInput(2, i, Size) != 2) return 1;
                    if (delayMs > 0) Thread.Sleep(delayMs);
                }
                return 0;
            }
            finally
            {
                Native.timeEndPeriod(1);
            }
        }
    }

    // Глобальный перехват клавиатуры (WH_KEYBOARD_LL). Нужен, чтобы отличить правый Alt от левого и «съесть» букву
    // (P или V), иначе она уйдёт в приложение. Живёт в своём потоке: даже если окно PassGen занято, ввод не тормозит.
    static class KeyHook
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104, WM_QUIT = 0x0012;
        const uint VkP = 0x50, VkV = 0x56, VkLShift = 0xA0, VkRShift = 0xA1, VkLCtrl = 0xA2, VkRCtrl = 0xA3;
        const uint VkLAlt = 0xA4, VkRAlt = 0xA5, VkLWin = 0x5B, VkRWin = 0x5C;
        // В раскладках с AltGr (испанская, русская «машинопись» и др.) Windows сама «нажимает» левый Ctrl
        // вместе с правым Alt — у этого ненастоящего Ctrl скан-код 0x21D.
        const uint FakeCtrlScan = 0x21D;

        public static readonly IntPtr OwnTag = (IntPtr)0x50415353;      // метка наших собственных «нажатий»
        public static Action<char, bool> Pressed;                       // в потоке хука: 'P' или 'V' и нажат ли ещё Shift

        [StructLayout(LayoutKind.Sequential)]
        struct KbdLlHook { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        static readonly Native.HookProc Proc = Callback;                // ссылка нужна, иначе делегат соберёт GC
        static IntPtr hook;
        static uint threadId;
        static Thread thread;
        static int swallowed;                                           // какие клавиши мы «съели» и ждём отпускания: P — бит 1, V — бит 2
        static bool realLCtrl, realRCtrl;

        public static bool Running
        {
            get { return thread != null; }
        }

        public static bool Start()
        {
            if (thread != null) return true;
            ManualResetEvent ready = new ManualResetEvent(false);
            Thread t = new Thread(delegate()
            {
                threadId = Native.GetCurrentThreadId();
                Native.MSG msg;
                Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);      // создаёт очередь сообщений потока
                hook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, Proc, Native.GetModuleHandle(null), 0);
                ready.Set();
                if (hook == IntPtr.Zero) return;
                while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)   // без цикла сообщений хук не работает
                {
                    Native.TranslateMessage(ref msg);
                    Native.DispatchMessage(ref msg);
                }
                Native.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            });
            t.IsBackground = true;
            t.Start();
            ready.WaitOne(3000);
            if (hook == IntPtr.Zero) return false;
            thread = t;
            return true;
        }

        public static void Stop()
        {
            if (thread == null) return;
            Native.PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(1000);
            thread = null;
            swallowed = 0;
        }

        static bool Down(uint vk)
        {
            return (Native.GetAsyncKeyState((int)vk) & 0x8000) != 0;
        }

        // Правый Alt (один, без Win и без настоящего Ctrl) + P или V. Shift допускается (с P это «повторить пароль»).
        static bool IsCombo(out bool shift)
        {
            shift = Down(VkLShift) || Down(VkRShift);
            if (!Down(VkRAlt) || Down(VkLAlt) || Down(VkLWin) || Down(VkRWin)) return false;
            // Настоящий Ctrl — другое сочетание: Ctrl+Alt+P открывает окно генератора.
            if (realLCtrl && Down(VkLCtrl)) return false;
            if (realRCtrl && Down(VkRCtrl)) return false;
            return true;
        }

        // Если Alt отпустить, ничего между ним не нажав, многие программы (Блокнот, Firefox…) открывают меню и уводят
        // фокус из поля ввода. Поэтому, «съев» P, сами нажимаем «пустую» клавишу 0xE8 — как это делает AutoHotkey.
        static void MaskAlt()
        {
            Native.INPUT[] i = new Native.INPUT[2];
            i[0] = Typer.Key(0xE8, 0, 0);
            i[1] = Typer.Key(0xE8, 0, Typer.KeyUp);
            Native.SendInput(2, i, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        // Должна отрабатывать мгновенно: если хук задумается больше чем на ~300 мс, Windows его отключит.
        static IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0)
                {
                    KbdLlHook k = (KbdLlHook)Marshal.PtrToStructure(lParam, typeof(KbdLlHook));
                    if (k.dwExtraInfo != OwnTag)
                    {
                        int msg = wParam.ToInt32();
                        bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                        if (k.vkCode == VkLCtrl && k.scanCode != FakeCtrlScan) realLCtrl = down;
                        else if (k.vkCode == VkRCtrl) realRCtrl = down;
                        else if (k.vkCode == VkP || k.vkCode == VkV)
                        {
                            int bit = k.vkCode == VkP ? 1 : 2;
                            if (down)
                            {
                                if ((swallowed & bit) != 0)
                                {
                                    if (Down(VkRAlt)) return (IntPtr)1;         // автоповтор удерживаемой клавиши
                                    swallowed &= ~bit;                          // Alt уже отпущен, а «клавиша вверх» к нам не дошла
                                }
                                bool shift;
                                if (IsCombo(out shift))
                                {
                                    swallowed |= bit;
                                    MaskAlt();
                                    Action<char, bool> h = Pressed;
                                    if (h != null) h(k.vkCode == VkP ? 'P' : 'V', shift);
                                    return (IntPtr)1;
                                }
                            }
                            else if ((swallowed & bit) != 0)
                            {
                                swallowed &= ~bit;
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return Native.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }
    }
}
