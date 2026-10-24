// SPDX-License-Identifier: MIT
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PassGen
{
    // Краткая справка по клавишам: открывается из меню значка в трее и по F1 (или ссылкой) в окне генератора.
    class HelpForm : Form
    {
        static readonly Color Ink = Color.FromArgb(31, 41, 55);
        static readonly Color Muted = Color.FromArgb(107, 114, 128);
        static readonly Color Accent = Color.FromArgb(37, 99, 235);
        static readonly Color AccentHover = Color.FromArgb(29, 78, 216);

        static HelpForm current;
        readonly float k;
        readonly Font keyFont = new Font("Segoe UI Semibold", 9.5f);
        readonly Font headFont = new Font("Segoe UI Semibold", 9f);
        readonly Font noteFont = new Font("Segoe UI", 8.5f);

        int S(int v) { return (int)Math.Round(v * k); }

        // Строки справки: (клавиши, что делают). Тексты берутся из Loc, поэтому окно всегда на текущем языке.
        static string[][] Anywhere()
        {
            return new string[][]
            {
                new string[] { Loc.T("help.k.p"), Loc.T("help.d.p") },
                new string[] { Loc.T("help.k.again"), Loc.T("help.d.again") },
                new string[] { Loc.T("help.k.v"), Loc.T("help.d.v") },
                new string[] { Loc.T("help.k.open"), Loc.T("help.d.open") }
            };
        }

        static string[][] InWindow()
        {
            return new string[][]
            {
                new string[] { Loc.T("help.k.enter"), Loc.T("help.d.enter") },
                new string[] { Loc.T("help.k.esc"), Loc.T("help.d.esc") },
                new string[] { Loc.T("help.k.f5"), Loc.T("help.d.f5") },
                new string[] { Loc.T("help.k.len"), Loc.T("help.d.len") },
                new string[] { Loc.T("help.k.f1"), Loc.T("help.d.f1") }
            };
        }

        public static void ShowHelp()
        {
            if (current == null || current.IsDisposed) current = new HelpForm();
            current.Show();
            current.Activate();
        }

        // При смене языка открытую справку закрываем: в следующий раз она откроется уже на новом языке.
        public static void CloseIfOpen()
        {
            if (current != null && !current.IsDisposed) current.Close();
        }

        HelpForm()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            Text = Loc.T("help.title");
            Icon = TrayApp.LoadIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            ForeColor = Ink;
            Font = new Font("Segoe UI", 9f);

            string[][] anywhere = Anywhere();
            string[][] inWindow = InWindow();

            // колонка с клавишами — по самой длинной надписи (в испанском и русском они длиннее)
            int keyW = 0;
            foreach (string[][] rows in new string[][][] { anywhere, inWindow })
                foreach (string[] r in rows)
                    keyW = Math.Max(keyW, TextRenderer.MeasureText(r[0], keyFont).Width);
            keyW = Math.Min(keyW + S(8), S(250));

            int pad = S(22), gap = S(14), W = S(640);
            int textW = W - 2 * pad - keyW - gap;
            int y = pad;

            Label title = new Label();
            title.Text = Loc.T("help.heading");
            title.Font = new Font("Segoe UI Semibold", 13f);
            title.AutoSize = false;
            title.Bounds = new Rectangle(pad, y, W - 2 * pad, S(30));
            Controls.Add(title);
            y += S(40);

            y = AddSection(Loc.T("help.anywhere"), anywhere, y, pad, keyW, gap, textW);
            y += S(8);
            y = AddSection(Loc.T("help.window"), inWindow, y, pad, keyW, gap, textW);
            y += S(6);

            Label note = new Label();
            note.Text = Loc.T("help.note");
            note.Font = noteFont;
            note.ForeColor = Muted;
            note.AutoSize = false;
            int noteH = TextRenderer.MeasureText(note.Text, noteFont, new Size(W - 2 * pad, 0), TextFormatFlags.WordBreak).Height;
            note.Bounds = new Rectangle(pad, y, W - 2 * pad, noteH);
            Controls.Add(note);
            y += noteH + S(18);

            Button close = new Button();
            close.Text = Loc.T("help.close");
            PopupForm.StyleFlat(close, Accent, Color.White, Accent, AccentHover);
            close.Size = new Size(S(110), S(32));
            close.Location = new Point(W - pad - close.Width, y);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            y += close.Height + pad;

            ClientSize = new Size(W, y);
            AcceptButton = close;
            CancelButton = close;
        }

        int AddSection(string header, string[][] rows, int y, int pad, int keyW, int gap, int textW)
        {
            Label h = new Label();
            h.Text = header;
            h.Font = headFont;
            h.ForeColor = Muted;
            h.AutoSize = false;
            h.Bounds = new Rectangle(pad, y, keyW + gap + textW, S(20));
            Controls.Add(h);
            y += S(26);

            foreach (string[] r in rows)
            {
                Label key = new Label();
                key.Text = r[0];
                key.Font = keyFont;
                key.ForeColor = Accent;
                key.AutoSize = false;
                Label desc = new Label();
                desc.Text = r[1];
                desc.AutoSize = false;
                int hk = TextRenderer.MeasureText(r[0], keyFont, new Size(keyW, 0), TextFormatFlags.WordBreak).Height;
                int hd = TextRenderer.MeasureText(r[1], Font, new Size(textW, 0), TextFormatFlags.WordBreak).Height;
                int rowH = Math.Max(hk, hd);
                key.Bounds = new Rectangle(pad, y, keyW, rowH);
                desc.Bounds = new Rectangle(pad + keyW + gap, y, textW, rowH);
                Controls.Add(key);
                Controls.Add(desc);
                y += rowH + S(9);
            }
            return y;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (current == this) current = null;
            FocusKeeper.Activate(FocusKeeper.Last);             // после закрытия справки вернуть фокус в программу, где были
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                keyFont.Dispose();
                headFont.Dispose();
                noteFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
