// SPDX-License-Identifier: MIT
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace PassGen
{
    // ───────────────────────── окно генератора ─────────────────────────

    class PopupForm : Form
    {
        const int HotkeyId = 1;
        static readonly Color Ink = Color.FromArgb(31, 41, 55);
        static readonly Color Muted = Color.FromArgb(107, 114, 128);
        static readonly Color Weak = Color.FromArgb(200, 70, 40);
        static readonly Color Accent = Color.FromArgb(37, 99, 235);
        static readonly Color AccentHover = Color.FromArgb(29, 78, 216);

        readonly Settings cfg;
        readonly float k;                                  // масштаб экрана (1.0 = 96 dpi)
        readonly TextBox pw = new TextBox();
        readonly TextBox desc = new TextBox();
        readonly Button btnNew = new Button();
        readonly Button btnSave = new Button();
        readonly TrackBar trk = new TrackBar();
        readonly NumericUpDown nudLen = new NumericUpDown();
        readonly Label lblBits = new Label();
        readonly CheckBox cbUpper = new CheckBox();
        readonly CheckBox cbLower = new CheckBox();
        readonly CheckBox cbDigits = new CheckBox();
        readonly CheckBox cbSym = new CheckBox();
        readonly ComboBox cmbSym = new ComboBox();
        readonly NumericUpDown nudMin = new NumericUpDown();
        Label lblMin;
        readonly CheckBox cbEasy = new CheckBox();
        readonly CheckBox cbBlocks = new CheckBox();
        readonly CheckBox cbLetter = new CheckBox();
        readonly ToolTip tip = new ToolTip();
        readonly LinkLabel lnkHelp = new LinkLabel();
        bool loading, hiding;
        IntPtr returnTo;                                   // окно, из которого открыли генератор
        readonly System.Windows.Forms.Timer focusTimer = new System.Windows.Forms.Timer();
        int focusTries;

        public int ShownAt;
        public int HiddenAt = unchecked(Environment.TickCount - 100000);
        public bool HotkeyOk;
        public event EventHandler HotkeyPressed;
        public event EventHandler HelpWanted;
        public Action<string, string> Confirmed;               // (пароль, описание)

        int S(int v) { return (int)Math.Round(v * k); }

        public PopupForm(Settings cfg)
        {
            this.cfg = cfg;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            focusTimer.Interval = 60;
            focusTimer.Tick += delegate { FocusWatchTick(); };

            Text = "PassGen";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(172, 180, 192);         // рамка в 1 пиксель
            Padding = new Padding(1);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.White;
            body.ForeColor = Ink;
            Controls.Add(body);

            int pad = S(14), gap = S(8);
            int W = S(400);
            int inner = W - 2 - 2 * pad;
            int y = pad;

            // Пароль + «другой»
            pw.Font = new Font("Consolas", 12.5f);
            pw.BorderStyle = BorderStyle.FixedSingle;
            pw.BackColor = Color.FromArgb(244, 247, 252);
            pw.Location = new Point(pad, y);
            StyleFlat(btnNew, Color.White, Ink, Color.FromArgb(205, 211, 221), Color.FromArgb(236, 240, 246));
            btnNew.Paint += PaintRefreshIcon;
            btnNew.Size = new Size(pw.Height, pw.Height);
            btnNew.Location = new Point(pad + inner - btnNew.Width, y);
            pw.Width = inner - btnNew.Width - S(6);
            y += pw.Height + gap;

            // Описание
            desc.Font = new Font("Segoe UI", 10f);
            desc.BorderStyle = BorderStyle.FixedSingle;
            desc.Location = new Point(pad, y);
            desc.Width = inner;
            desc.MaxLength = 200;
            desc.HandleCreated += delegate
            {
                Native.SendMessage(desc.Handle, 0x1501, (IntPtr)1, Loc.T("popup.cue"));   // EM_SETCUEBANNER
            };
            y += desc.Height + gap + S(4);

            // Длина: ползунок + число + оценка стойкости
            nudLen.Minimum = 4;
            nudLen.Maximum = 128;
            nudLen.TextAlign = HorizontalAlignment.Center;
            nudLen.Width = S(54);
            int rowH = nudLen.Height;
            Label lblLen = AddLabel(body, Loc.T("popup.length"), pad, y, rowH);
            trk.Minimum = 4;
            trk.Maximum = 64;
            trk.TickStyle = TickStyle.None;
            trk.AutoSize = false;
            trk.BackColor = Color.White;
            trk.SmallChange = 1;
            trk.LargeChange = 4;
            trk.Bounds = new Rectangle(lblLen.Right + S(2), y - S(2), S(170), rowH + S(4));
            nudLen.Location = new Point(trk.Right + S(4), y);
            lblBits.AutoSize = false;
            lblBits.Font = new Font("Segoe UI", 8.5f);
            lblBits.ForeColor = Muted;
            lblBits.TextAlign = ContentAlignment.MiddleRight;
            lblBits.Bounds = new Rectangle(nudLen.Right + S(6), y, pad + inner - (nudLen.Right + S(6)), rowH);
            y += rowH + gap;

            // Наборы знаков
            cbUpper.Text = "A–Z";
            cbLower.Text = "a–z";
            cbDigits.Text = "0–9";
            cbSym.Text = Loc.T("popup.symbols");
            y += Place(body, y, pad, S(12), cbUpper, cbLower, cbDigits, cbSym) + gap;

            // Какие именно спецзнаки и сколько минимум
            cmbSym.Font = new Font("Consolas", 10f);
            cmbSym.DropDownStyle = ComboBoxStyle.DropDown;
            cmbSym.MaxLength = 40;
            cmbSym.Items.AddRange(Gen.SymbolPresets);
            nudMin.Minimum = 1;
            nudMin.Maximum = 8;
            nudMin.TextAlign = HorizontalAlignment.Center;
            nudMin.Width = S(48);
            int row2 = Math.Max(cmbSym.Height, nudMin.Height);
            nudMin.Location = new Point(pad + inner - nudMin.Width, y + (row2 - nudMin.Height) / 2);
            // подпись меняется («Минимум» ↔ «Ровно»), поэтому ширину берём по более длинной
            int lw = Math.Max(TextRenderer.MeasureText(Loc.T("popup.min"), Font).Width,
                              TextRenderer.MeasureText(Loc.T("popup.exact"), Font).Width) + S(2);
            lblMin = AddLabel(body, Loc.T("popup.min"), nudMin.Left - lw - S(2), y, row2);
            lblMin.Width = lw;
            lblMin.TextAlign = ContentAlignment.MiddleRight;
            cmbSym.Location = new Point(pad, y + (row2 - cmbSym.Height) / 2);
            cmbSym.Width = lblMin.Left - S(8) - pad;
            y += row2 + gap;

            // Дополнительно
            cbEasy.Text = Loc.T("popup.easy");
            cbBlocks.Text = Loc.T("popup.blocks");
            cbLetter.Text = Loc.T("popup.letter");
            y += Place(body, y, pad, S(12), cbEasy, cbBlocks, cbLetter) + S(14);

            // Главная кнопка
            btnSave.Text = Loc.T("popup.save");
            btnSave.Font = new Font("Segoe UI Semibold", 10f);
            StyleFlat(btnSave, Accent, Color.White, Accent, AccentHover);
            btnSave.Bounds = new Rectangle(pad, y, inner, S(36));
            y += btnSave.Height + S(6);

            // Ссылка на справку по клавишам
            lnkHelp.Text = Loc.T("popup.help");
            lnkHelp.Font = new Font("Segoe UI", 8.5f);
            lnkHelp.LinkColor = Muted;
            lnkHelp.ActiveLinkColor = Accent;
            lnkHelp.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkHelp.AutoSize = false;
            lnkHelp.TextAlign = ContentAlignment.MiddleCenter;
            lnkHelp.TabStop = false;
            lnkHelp.Bounds = new Rectangle(pad, y, inner, S(20));
            lnkHelp.LinkClicked += delegate { RaiseHelp(); };
            y += lnkHelp.Height + S(8);

            body.Controls.AddRange(new Control[] { pw, btnNew, desc, trk, nudLen, lblBits, cmbSym, nudMin, btnSave, lnkHelp });
            ClientSize = new Size(W, y + 2);

            desc.TabIndex = 0; pw.TabIndex = 1; btnNew.TabIndex = 2; trk.TabIndex = 3; nudLen.TabIndex = 4;
            cbUpper.TabIndex = 5; cbLower.TabIndex = 6; cbDigits.TabIndex = 7; cbSym.TabIndex = 8;
            cmbSym.TabIndex = 9; nudMin.TabIndex = 10;
            cbEasy.TabIndex = 11; cbBlocks.TabIndex = 12; cbLetter.TabIndex = 13; btnSave.TabIndex = 14;
            AcceptButton = btnSave;
            ActiveControl = desc;

            tip.SetToolTip(btnNew, Loc.T("tip.new"));
            tip.SetToolTip(lblBits, Loc.T("tip.bits"));
            tip.SetToolTip(cmbSym, Loc.T("tip.symset"));
            tip.SetToolTip(nudMin, Loc.T("tip.min"));
            tip.SetToolTip(cbEasy, Loc.T("tip.easy"));
            tip.SetToolTip(cbBlocks, Loc.T("tip.blocks"));
            tip.SetToolTip(cbLetter, Loc.T("tip.letter"));

            LoadControls();

            btnNew.Click += delegate { Regenerate(); desc.Focus(); };
            btnSave.Click += delegate { Confirm(); };
            trk.ValueChanged += delegate
            {
                if (loading) return;
                loading = true;
                nudLen.Value = trk.Value;
                loading = false;
                Changed();
            };
            nudLen.ValueChanged += delegate
            {
                if (loading) return;
                loading = true;
                trk.Value = Math.Max(trk.Minimum, Math.Min(trk.Maximum, (int)nudLen.Value));
                loading = false;
                Changed();
            };
            EventHandler classChanged = ClassChanged;
            cbUpper.CheckedChanged += classChanged;
            cbLower.CheckedChanged += classChanged;
            cbDigits.CheckedChanged += classChanged;
            cbSym.CheckedChanged += classChanged;
            cmbSym.TextChanged += delegate { Changed(); };
            nudMin.ValueChanged += delegate { Changed(); };
            cbEasy.CheckedChanged += delegate { Changed(); };
            cbBlocks.CheckedChanged += delegate { Changed(); };
            cbLetter.CheckedChanged += delegate { Changed(); };
        }

        // ——— построение ———

        internal static void StyleFlat(Button b, Color back, Color fore, Color border, Color hover)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = back;
            b.ForeColor = fore;
            b.FlatAppearance.BorderColor = border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.FlatAppearance.MouseDownBackColor = hover;
            b.Cursor = Cursors.Hand;
        }

        // Значок «обновить» (круговая стрелка) рисуем сами: не зависит от шрифтов и масштаба экрана.
        void PaintRefreshIcon(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float size = Math.Min(btnNew.Width, btnNew.Height);
            float cx = btnNew.Width / 2f, cy = btnNew.Height / 2f, r = size * 0.26f;
            float start = -50f, sweep = 280f;
            double end = (start + sweep) * Math.PI / 180.0;                   // конец дуги
            using (Pen pen = new Pen(Ink, Math.Max(1.5f, size * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                g.DrawArc(pen, cx - r, cy - r, 2 * r, 2 * r, start, sweep);
            }
            float ex = cx + r * (float)Math.Cos(end), ey = cy + r * (float)Math.Sin(end);
            float tx = -(float)Math.Sin(end), ty = (float)Math.Cos(end);      // куда идёт дуга (по часовой)
            float nx = (float)Math.Cos(end), ny = (float)Math.Sin(end);       // «наружу» от центра
            float hl = size * 0.20f, hw = size * 0.15f;
            using (Brush b = new SolidBrush(Ink))
                g.FillPolygon(b, new PointF[]
                {
                    new PointF(ex + tx * hl, ey + ty * hl),
                    new PointF(ex + nx * hw, ey + ny * hw),
                    new PointF(ex - nx * hw, ey - ny * hw)
                });
        }

        Label AddLabel(Control parent, string text, int x, int yy, int h)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Muted;
            l.Bounds = new Rectangle(x, yy, TextRenderer.MeasureText(text, Font).Width + S(2), h);
            parent.Controls.Add(l);
            return l;
        }

        // Раскладывает флажки в ряд, возвращает высоту ряда.
        int Place(Control parent, int yy, int x, int gapX, params CheckBox[] boxes)
        {
            int h = 0;
            foreach (CheckBox c in boxes)
            {
                c.AutoSize = true;
                c.ForeColor = Ink;
                c.Location = new Point(x, yy);
                parent.Controls.Add(c);
                Size ps = c.PreferredSize;
                x += ps.Width + gapX;
                h = Math.Max(h, ps.Height);
            }
            return h;
        }

        // ——— настройки ⇄ элементы ———

        void LoadControls()
        {
            loading = true;
            nudLen.Value = Math.Max(nudLen.Minimum, Math.Min(nudLen.Maximum, cfg.Length));
            trk.Value = Math.Max(trk.Minimum, Math.Min(trk.Maximum, cfg.Length));
            cbUpper.Checked = cfg.Upper;
            cbLower.Checked = cfg.Lower;
            cbDigits.Checked = cfg.Digits;
            cbSym.Checked = cfg.Symbols;
            cmbSym.Text = cfg.SymbolSet;
            nudMin.Value = Math.Max(nudMin.Minimum, Math.Min(nudMin.Maximum, cfg.MinSymbols));
            cbEasy.Checked = cfg.Easy;
            cbBlocks.Checked = cfg.Blocks;
            cbLetter.Checked = cfg.StartLetter;
            loading = false;
            UpdateEnabled();
        }

        void ReadControls()
        {
            cfg.Length = (int)nudLen.Value;
            cfg.Upper = cbUpper.Checked;
            cfg.Lower = cbLower.Checked;
            cfg.Digits = cbDigits.Checked;
            cfg.Symbols = cbSym.Checked;
            cfg.SymbolSet = cmbSym.Text;
            cfg.MinSymbols = (int)nudMin.Value;
            cfg.Easy = cbEasy.Checked;
            cfg.Blocks = cbBlocks.Checked;
            cfg.StartLetter = cbLetter.Checked;
        }

        void UpdateEnabled()
        {
            bool sym = cbSym.Checked;
            cmbSym.Enabled = sym;
            nudMin.Enabled = sym;
            lblMin.Enabled = sym;
            lblMin.Text = Loc.T(cbEasy.Checked ? "popup.exact" : "popup.min");
            cbLetter.Enabled = cbUpper.Checked || cbLower.Checked;
        }

        void ClassChanged(object sender, EventArgs e)
        {
            if (loading) return;
            if (!cbUpper.Checked && !cbLower.Checked && !cbDigits.Checked && !cbSym.Checked)
            {
                // хотя бы один набор знаков должен остаться
                loading = true;
                ((CheckBox)sender).Checked = true;
                loading = false;
            }
            Changed();
        }

        void Changed()
        {
            if (loading) return;
            ReadControls();
            UpdateEnabled();
            cfg.Save();
            Regenerate();
        }

        void Regenerate()
        {
            double bits;
            pw.Text = Gen.Make(cfg, out bits);
            int b = (int)Math.Round(bits);
            lblBits.Text = Loc.T("popup.bits", b);
            lblBits.ForeColor = b < 60 ? Weak : Muted;
        }

        // Если число в поле ввели, но не нажали Tab — всё равно учтём его.
        static void Commit(NumericUpDown n)
        {
            decimal v;
            if (decimal.TryParse(n.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out v))
                n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));
        }

        // ——— показ / скрытие ———

        public void Prepare()
        {
            Touch(this);                                        // создаём окна заранее — открывается мгновенно
            Regenerate();
        }

        static void Touch(Control c)
        {
            IntPtr h = c.Handle;
            foreach (Control x in c.Controls) Touch(x);
        }

        public void Display()
        {
            Regenerate();
            desc.Clear();
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - S(12), wa.Bottom - Height - S(12));
            ShownAt = Environment.TickCount;
            returnTo = FocusKeeper.Last;                        // куда вернуть фокус, когда окно закроется
            ActiveControl = desc;
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            desc.Focus();
            StartFocusWatch();
        }

        // Простое скрытие: так окно закрывается само, когда пользователь кликнул в другое окно, — фокус не трогаем.
        public void Dismiss()
        {
            if (hiding || !Visible) return;
            hiding = true;
            try { Hide(); }
            finally { hiding = false; }
            HiddenAt = Environment.TickCount;
        }

        // Закрытие по Enter / Esc / повторному нажатию: возвращаем фокус программе, где пользователь был.
        public void DismissAndReturn()
        {
            Dismiss();
            IntPtr back = FocusKeeper.Last;
            if (returnTo != IntPtr.Zero && Native.IsWindow(returnTo) && Native.IsWindowVisible(returnTo) && !Native.IsIconic(returnTo)) back = returnTo;
            FocusKeeper.Activate(back);
        }

        void Confirm()
        {
            Commit(nudLen);
            Commit(nudMin);
            string p = pw.Text.Trim();
            if (p.Length == 0) return;
            string d = desc.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            DismissAndReturn();                                 // сначала прячем окно и возвращаем фокус в прежнюю программу
            Action<string, string> cb = Confirmed;
            if (cb != null) cb(p, d);
        }

        void RaiseHelp()
        {
            EventHandler h = HelpWanted;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    DismissAndReturn();
                    return true;
                case Keys.F1:
                    RaiseHelp();
                    return true;
                case Keys.F5:
                case Keys.Control | Keys.R:
                    Regenerate();
                    return true;
                case Keys.Control | Keys.Up:
                    nudLen.Value = Math.Min(nudLen.Maximum, nudLen.Value + 1);
                    return true;
                case Keys.Control | Keys.Down:
                    nudLen.Value = Math.Max(nudLen.Minimum, nudLen.Value - 1);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            StartFocusWatch();
        }

        // Если Windows отдала окну фокус «силой» или с опозданием, курсор остаётся на самой форме, а не в поле описания,
        // и Enter не срабатывает. Поэтому несколько раз с паузой проверяем и ставим курсор в поле описания.
        void StartFocusWatch()
        {
            focusTries = 0;
            focusTimer.Stop();
            focusTimer.Start();
        }

        void FocusWatchTick()
        {
            if (!Visible || ContainsFocusedChild() || ++focusTries >= 10)
            {
                focusTimer.Stop();
                return;
            }
            if (Native.GetForegroundWindow() == Handle) desc.Focus();
        }

        bool ContainsFocusedChild()
        {
            foreach (Control c in new Control[] { pw, desc, btnNew, btnSave, trk, nudLen, cmbSym, nudMin, cbUpper, cbLower, cbDigits, cbSym, cbEasy, cbBlocks, cbLetter })
                if (c.ContainsFocus) return true;
            return false;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Dismiss();                                          // клик мимо окна — закрыть
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;                                // Alt+F4 лишь прячет окно
                Dismiss();
                return;
            }
            base.OnFormClosing(e);
        }

        // Ctrl+Alt+P из любого приложения
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            HotkeyOk = Native.RegisterHotKey(Handle, HotkeyId, 0x0002 | 0x0001 | 0x4000, 0x50);   // CONTROL | ALT | NOREPEAT, 'P'
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(e);
        }

        // Окно пересоздаётся при смене языка — таймер и подсказки за собой убираем.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                focusTimer.Stop();
                focusTimer.Dispose();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam == (IntPtr)HotkeyId)    // WM_HOTKEY
            {
                EventHandler h = HotkeyPressed;
                if (h != null) h(this, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;                    // CS_DROPSHADOW
                return cp;
            }
        }
    }
}
