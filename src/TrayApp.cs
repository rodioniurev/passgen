// SPDX-License-Identifier: MIT
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace PassGen
{
    // ───────────────────────── трей ─────────────────────────

    class TrayApp : ApplicationContext
    {
        readonly Settings cfg = Settings.Load();
        readonly NotifyIcon tray = new NotifyIcon();
        PopupForm popup;                                   // пересоздаётся при смене языка
        readonly Toast toast = new Toast();
        readonly EventWaitHandle wake, quit;
        readonly ToolStripMenuItem miGenerate = new ToolStripMenuItem();
        readonly ToolStripMenuItem miOpenFile = new ToolStripMenuItem();
        readonly ToolStripMenuItem miChooseFile = new ToolStripMenuItem();
        readonly ToolStripMenuItem miQuick = new ToolStripMenuItem();
        readonly ToolStripMenuItem miAutostart = new ToolStripMenuItem();
        readonly ToolStripMenuItem miLanguage = new ToolStripMenuItem();
        readonly ToolStripMenuItem miLangAuto = new ToolStripMenuItem();
        readonly ToolStripMenuItem[] miLangs = new ToolStripMenuItem[Loc.Codes.Length];
        readonly ToolStripMenuItem miHelp = new ToolStripMenuItem();
        readonly ToolStripMenuItem miExit = new ToolStripMenuItem();
        string lastPassword = "";                          // последний пароль (его повторяет Right Alt+Shift+P)
        int typing;                                        // 1, пока идёт ввод

        public TrayApp(EventWaitHandle wake, EventWaitHandle quit, bool openNow)
        {
            this.wake = wake;
            this.quit = quit;
            Loc.Init(cfg.Language);

            CreatePopup();
            FocusKeeper.Start();
            KeyHook.Pressed = OnQuickKey;

            ContextMenuStrip menu = BuildMenu();
            tray.Icon = LoadIcon();
            ApplyQuick();                                   // включает перехват Right Alt+P/V и подписывает значок
            tray.ContextMenuStrip = menu;
            tray.MouseClick += TrayClick;
            tray.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) popup.Display();
            };
            tray.Visible = true;

            Thread t = new Thread(WakeLoop);
            t.IsBackground = true;
            t.Start();

            if (openNow) popup.BeginInvoke(new MethodInvoker(popup.Display));
        }

        // Окно генератора создаётся заново при каждой смене языка — все подписи строятся в конструкторе.
        void CreatePopup()
        {
            popup = new PopupForm(cfg);
            popup.Confirmed = OnConfirmed;
            popup.HelpWanted += delegate
            {
                popup.Dismiss();                            // окно генератора скрывается само, как только фокус уйдёт к справке
                HelpForm.ShowHelp();
            };
            popup.HotkeyPressed += delegate
            {
                if (popup.Visible) popup.DismissAndReturn();
                else popup.Display();
            };
            popup.Prepare();
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            miGenerate.Font = new Font(menu.Font, FontStyle.Bold);
            miGenerate.Click += delegate { popup.Display(); };
            miOpenFile.Click += delegate { OpenFile(); };
            miChooseFile.Click += delegate { ChooseFile(); };

            miQuick.CheckOnClick = true;
            miQuick.Checked = cfg.QuickType;
            miQuick.Click += delegate
            {
                cfg.QuickType = miQuick.Checked;
                cfg.Save();
                ApplyQuick();
            };

            miAutostart.CheckOnClick = true;
            miAutostart.Click += delegate { Autostart.Set(miAutostart.Checked); };

            miLangAuto.Tag = Loc.Auto;
            miLangAuto.Click += LanguageClick;
            miLanguage.DropDownItems.Add(miLangAuto);
            for (int i = 0; i < Loc.Codes.Length; i++)
            {
                miLangs[i] = new ToolStripMenuItem(Loc.Names[i]);   // каждый язык подписан на самом себе
                miLangs[i].Tag = Loc.Codes[i];
                miLangs[i].Click += LanguageClick;
                miLanguage.DropDownItems.Add(miLangs[i]);
            }

            miHelp.Click += delegate { HelpForm.ShowHelp(); };
            miExit.Click += delegate { Quit(); };

            menu.Items.AddRange(new ToolStripItem[]
            {
                miGenerate, miOpenFile, miChooseFile, new ToolStripSeparator(),
                miQuick, miAutostart, miLanguage, new ToolStripSeparator(),
                miHelp, miExit
            });
            menu.Opening += delegate { miAutostart.Checked = Autostart.IsOn(); };
            ApplyMenuTexts();
            return menu;
        }

        void ApplyMenuTexts()
        {
            miGenerate.Text = Loc.T("menu.generate");
            miOpenFile.Text = Loc.T("menu.openfile");
            miChooseFile.Text = Loc.T("menu.choosefile");
            miQuick.Text = Loc.T("menu.quick");
            miQuick.ToolTipText = Loc.T("menu.quick.tip");
            miAutostart.Text = Loc.T("menu.autostart");
            miLanguage.Text = Loc.T("menu.language");
            miLangAuto.Text = Loc.T("menu.language.auto");
            miHelp.Text = Loc.T("menu.help");
            miExit.Text = Loc.T("menu.exit");

            string selected = Array.IndexOf(Loc.Codes, cfg.Language) >= 0 ? cfg.Language : Loc.Auto;
            miLangAuto.Checked = selected == Loc.Auto;
            for (int i = 0; i < miLangs.Length; i++) miLangs[i].Checked = selected == Loc.Codes[i];
        }

        void LanguageClick(object sender, EventArgs e)
        {
            SetLanguage((string)((ToolStripMenuItem)sender).Tag);
        }

        void SetLanguage(string code)
        {
            cfg.Language = code;
            cfg.Save();
            Loc.Init(cfg.Language);

            HelpForm.CloseIfOpen();
            popup.Dispose();                                // заодно освобождает сочетание Ctrl+Alt+P…
            CreatePopup();                                  // …и новое окно регистрирует его снова
            ApplyMenuTexts();
            ApplyQuick();                                   // подсказка значка — уже на новом языке
        }

        public static Icon LoadIcon()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("passgen.ico"))
                    return new Icon(s, SystemInformation.SmallIconSize);
            }
            catch (Exception)
            {
                return SystemIcons.Application;
            }
        }

        // Повторный запуск exe «будит» уже работающую копию, «PassGen.exe /exit» — закрывает её.
        void WakeLoop()
        {
            WaitHandle[] handles = new WaitHandle[] { wake, quit };
            while (true)
            {
                int which = WaitHandle.WaitAny(handles);
                try { popup.BeginInvoke(which == 0 ? new MethodInvoker(popup.Display) : new MethodInvoker(Quit)); }
                catch (Exception) { return; }
            }
        }

        void TrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int now = Environment.TickCount;
            if (popup.Visible && now - popup.ShownAt < 500) return;             // хвост двойного клика
            // Клик по иконке отнимает у окна фокус, и оно успевает закрыться само — тогда это «закрыть», а не «открыть».
            if (popup.Visible || now - popup.HiddenAt < 300) { popup.DismissAndReturn(); return; }
            popup.Display();
        }

        // Дописывает «дата - пароль[ - описание]» в файл. Возвращает текст ошибки или null.
        string SaveEntry(string password, string description)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " - " + password;
            if (description.Length > 0) line += " - " + description;
            try
            {
                Storage.Append(cfg.PasswordFile, line);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ——— Right Alt+P и Right Alt+V ———

        void ApplyQuick()
        {
            if (cfg.QuickType)
            {
                if (!KeyHook.Start())
                {
                    cfg.QuickType = false;
                    miQuick.Checked = false;
                    toast.Flash(Loc.T("toast.quickFail"), false);
                }
            }
            else
            {
                KeyHook.Stop();
            }

            string t = Loc.T("tray.head");
            if (popup.HotkeyOk) t += Loc.T("tray.hotkey");
            t += Loc.T("tray.window");
            if (KeyHook.Running) t += Loc.T("tray.quick");
            if (t.Length > 63) t = t.Substring(0, 63);              // подсказка значка в трее — не длиннее 63 знаков, иначе приложение упадёт
            tray.Text = t;
        }

        // Вызывается из потока хука — переходим в основной поток.
        void OnQuickKey(char key, bool shift)
        {
            try
            {
                popup.BeginInvoke(new MethodInvoker(delegate
                {
                    if (key == 'V') QuickPaste();
                    else QuickType(shift);
                }));
            }
            catch (Exception) { }
        }

        // Right Alt+P: новый пароль → в файл и в буфер → печатаем там, где курсор.
        // Right Alt+Shift+P (again): печатаем последний пароль ещё раз — для поля «повторите пароль».
        void QuickType(bool again)
        {
            IntPtr target = Native.GetForegroundWindow();
            if (target == popup.Handle) return;                                // окно генератора активно — печатать в него незачем
            if (Interlocked.CompareExchange(ref typing, 1, 0) != 0) return;    // предыдущий ввод ещё идёт

            bool started = false;
            try
            {
                string password, saveError = null;
                if (again)
                {
                    password = lastPassword;
                    if (password.Length == 0)
                    {
                        toast.Flash(Loc.T("toast.noPassword"), false);
                        return;
                    }
                }
                else
                {
                    double bits;
                    password = Gen.Make(cfg, out bits);
                    lastPassword = password;
                    saveError = SaveEntry(password, cfg.AutoDesc ? Win.Title(target) : "");
                }
                bool copied = Clip.Copy(password);
                TypeAsync(password, target, delegate(string problem) { QuickDone(again, copied, saveError, problem); });
                started = true;
            }
            finally
            {
                if (!started) Interlocked.Exchange(ref typing, 0);
            }
        }

        // Печатает text в окно target в отдельном потоке: сначала ждёт, пока отпустят Alt/Shift (иначе знаки уйдут как
        // Alt+знак), и следит, чтобы активной оставалась та же программа — если фокус ушёл, остаток не печатается.
        // По окончании в основном потоке вызывает done(problem); problem == null — всё напечатано.
        void TypeAsync(string text, IntPtr target, Action<string> done)
        {
            int delay = cfg.TypeDelay;
            uint targetPid = Win.Pid(target);
            Thread worker = new Thread(delegate()
            {
                string problem = null;
                try
                {
                    uint pid = targetPid;
                    for (int n = 0; n < 20 && pid == 0; n++)            // окна как раз переключались — ждём активное
                    {
                        Thread.Sleep(20);
                        pid = Win.ForegroundPid();
                    }
                    if (pid == 0) problem = Loc.T("prob.noWindow");
                    else if (!Typer.WaitReleased(1500)) problem = Loc.T("prob.keysHeld");
                    else if (Win.ForegroundPid() != pid) problem = Loc.T("prob.windowChanged");
                    else
                    {
                        int r = Typer.TypeText(text, delay, pid);
                        if (r == 1) problem = Loc.T("prob.blocked");
                        else if (r == 2) problem = Loc.T("prob.interrupted");
                    }
                }
                catch (Exception ex)
                {
                    problem = ex.Message;
                }
                try { popup.BeginInvoke(new MethodInvoker(delegate { done(problem); })); }
                catch (Exception) { }
                Interlocked.Exchange(ref typing, 0);
            });
            worker.IsBackground = true;
            worker.Start();
        }

        // Right Alt+V: напечатать по символам то, что лежит в буфере обмена. Для сайтов, где вставка запрещена, а e-mail,
        // логин или код нужно ввести руками. Ничего никуда не записывает и сам буфер не меняет.
        const int MaxPasteChars = 500;

        void QuickPaste()
        {
            IntPtr target = Native.GetForegroundWindow();
            if (target == popup.Handle) return;
            if (Interlocked.CompareExchange(ref typing, 1, 0) != 0) return;

            bool started = false;
            try
            {
                string raw = Clip.GetText();
                string text = raw == null ? "" : Typer.Clean(raw);
                if (text.Length == 0)
                {
                    toast.Flash(Loc.T("toast.clipNoText"), false);
                    return;
                }
                if (text.Length > MaxPasteChars)
                {
                    toast.Flash(Loc.T("toast.clipTooLong", text.Length, MaxPasteChars), false);
                    return;
                }
                TypeAsync(text, target, delegate(string problem) { PasteDone(text.Length, problem); });
                started = true;
            }
            finally
            {
                if (!started) Interlocked.Exchange(ref typing, 0);
            }
        }

        void PasteDone(int count, string problem)
        {
            if (problem != null)
                toast.Flash(Loc.T("toast.notTyped", problem), false);
            else
                toast.Flash(Loc.T("toast.pasted", count, Loc.Plural(count, "unit.char")), true);
        }

        void QuickDone(bool again, bool copied, string saveError, string problem)
        {
            if (problem != null)
                toast.Flash(Loc.T(copied ? "toast.typeFailedCopied" : "toast.typeFailed", problem), false);
            else if (saveError != null)
                toast.Flash(Loc.T("toast.typedNotSaved", saveError), false);
            else
                toast.Flash(Loc.T(again ? "toast.typedAgain" : "toast.typedSaved"), true);
        }

        void OnConfirmed(string password, string description)
        {
            lastPassword = password;
            string error = SaveEntry(password, description);
            bool copied = Clip.Copy(password);

            if (error != null)
            {
                MessageBox.Show(
                    Loc.T(copied ? "msg.copiedNotSaved" : "msg.notCopiedNotSaved") +
                    "\n" + cfg.PasswordFile + "\n\n" + error,
                    "PassGen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (copied)
            {
                toast.Flash(Loc.T("toast.copiedSaved"), true);
            }
            else
            {
                toast.Flash(Loc.T("toast.savedNotCopied"), false);
            }
        }

        void OpenFile()
        {
            try
            {
                if (!File.Exists(cfg.PasswordFile)) Storage.Touch(cfg.PasswordFile);
                Process.Start(new ProcessStartInfo(cfg.PasswordFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PassGen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ChooseFile()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = Loc.T("dlg.saveTitle");
                d.Filter = Loc.T("dlg.filter");
                d.DefaultExt = "txt";
                d.OverwritePrompt = false;                      // писать будем в конец, файл не затираем
                d.CheckPathExists = true;
                d.FileName = Path.GetFileName(cfg.PasswordFile);
                string dir = Path.GetDirectoryName(cfg.PasswordFile);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) d.InitialDirectory = dir;
                if (d.ShowDialog() == DialogResult.OK)
                {
                    cfg.PasswordFile = d.FileName;
                    cfg.Save();
                    toast.Flash(Loc.T("toast.savingTo", Path.GetFileName(d.FileName)), true);
                }
            }
        }

        void Quit()
        {
            KeyHook.Stop();
            FocusKeeper.Stop();
            tray.Visible = false;
            tray.Dispose();
            popup.Dispose();
            toast.Dispose();
            ExitThread();
        }
    }
}
