using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NavPro.Frontend.Ui;

namespace NavPro.Frontend.Controls
{
    /// <summary>Compact rounded circular progress indicator for long-running NavPro work.</summary>
    public sealed class RoundedProgressIndicator : Control
    {
        private readonly Timer animation;
        private int progress;
        private int rotation;
        private bool indeterminate;

        public int Progress
        {
            get => progress;
            set
            {
                int next = Math.Max(0, Math.Min(100, value));
                if (progress == next) return;
                progress = next;
                Invalidate();
            }
        }

        public bool Indeterminate
        {
            get => indeterminate;
            set
            {
                if (indeterminate == value) return;
                indeterminate = value;
                if (indeterminate) animation.Start(); else animation.Stop();
                Invalidate();
            }
        }

        public RoundedProgressIndicator()
        {
            Size = new Size(72, 72);
            MinimumSize = new Size(64, 64);
            MaximumSize = new Size(80, 80);
            BackColor = DialogTheme.Brand;
            ForeColor = Color.White;
            Font = DialogTheme.CreateFont(10F, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            animation = new Timer { Interval = 45 };
            animation.Tick += (s, e) => { rotation = (rotation + 8) % 360; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const float thickness = 7F;
            var bounds = new RectangleF(thickness / 2 + 4, thickness / 2 + 4,
                Width - thickness - 8, Height - thickness - 8);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var track = new Pen(Color.FromArgb(0,30,85), thickness))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                e.Graphics.DrawArc(track, bounds, -90, 359.5F);
            }

            float sweep = indeterminate ? 92F : 3.6F * progress;
            if (sweep > 0)
            {
                using (var arc = new Pen(DialogTheme.Highlight, thickness))
                {
                    arc.StartCap = LineCap.Round;
                    arc.EndCap = LineCap.Round;
                    e.Graphics.DrawArc(arc, bounds, indeterminate ? rotation - 90 : -90,
                        indeterminate ? sweep : Math.Min(sweep, 359.5F));
                }
            }

            string centerText = indeterminate ? "…" : progress.ToString() + "%";
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(ForeColor))
                e.Graphics.DrawString(centerText, Font, brush, ClientRectangle, format);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }
    }
}
