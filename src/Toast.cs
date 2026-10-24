// SPDX-License-Identifier: MIT
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PassGen
{
    // Небольшая плашка «Скопировано» — не отнимает фокус у активного окна.
    class Toast : Form
    {
        readonly Label label = new Label();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly float k;

        public Toast()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 10f);
            label.AutoSize = true;
            label.ForeColor = Color.White;
            label.Location = new Point((int)(14 * k), (int)(9 * k));
            Controls.Add(label);
            timer.Tick += delegate { timer.Stop(); Hide(); };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;          // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void Flash(string text, bool ok)
        {
            if (text.Length > 110) text = text.Substring(0, 109) + "…";
            label.Text = text;
            BackColor = ok ? Color.FromArgb(31, 41, 55) : Color.FromArgb(185, 28, 28);
            Size ps = label.PreferredSize;
            ClientSize = new Size(ps.Width + (int)(28 * k), ps.Height + (int)(18 * k));
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - (int)(12 * k), wa.Bottom - Height - (int)(12 * k));
            Show();
            timer.Stop();
            timer.Interval = ok ? 1600 : 5000;
            timer.Start();
        }
    }
}
