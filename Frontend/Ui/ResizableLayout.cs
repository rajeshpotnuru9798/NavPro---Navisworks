using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NavPro.Frontend.Ui
{
    /// <summary>Draggable list/preview regions; existing controls and their event handlers are retained.</summary>
    internal static class ResizableLayout
    {
        internal static SplitContainer Split(Control first, Control second, Orientation orientation, double fraction, string name)
        {
            var split = new FlexibleSplit(orientation, fraction) { Dock = DockStyle.Fill, Name = name, AccessibleName = name,
                AccessibleDescription = "Drag the divider to resize the two areas. Double-click it to reset.", Margin = Padding.Empty };
            first.Dock = second.Dock = DockStyle.Fill;
            split.Panel1.Controls.Add(first); split.Panel2.Controls.Add(second);
            return split;
        }

        // Combines a contiguous range in a single-column table into two resizable groups.
        internal static SplitContainer Rows(TableLayoutPanel table, int first, int last, int upperLast, double fraction, string name)
        {
            if (table.ColumnCount != 1 || first < 0 || last >= table.RowCount || upperLast < first || upperLast >= last)
                throw new ArgumentException("Invalid resizable row range.");
            var entries = table.Controls.Cast<Control>().Select(c => new { Control = c, Row = table.GetRow(c) }).ToList();
            if (entries.Any(e => table.GetRowSpan(e.Control) != 1)) throw new ArgumentException("Resizable rows cannot cross row spans.");
            var styles = table.RowStyles.Cast<RowStyle>().Select(s => new RowStyle(s.SizeType, s.Height)).ToArray();
            var upper = Group(first, upperLast, styles);
            var lower = Group(upperLast + 1, last, styles);
            table.SuspendLayout();
            try
            {
                foreach (var entry in entries.Where(e => e.Row >= first && e.Row <= last))
                {
                    table.Controls.Remove(entry.Control);
                    var target = entry.Row <= upperLast ? upper : lower;
                    target.Controls.Add(entry.Control, 0, entry.Row - (entry.Row <= upperLast ? first : upperLast + 1));
                }
                int removed = last - first;
                foreach (var entry in entries.Where(e => e.Row > last)) table.SetRow(entry.Control, entry.Row - removed);
                table.RowStyles.Clear();
                for (int row = 0; row < styles.Length; row++)
                {
                    if (row == first) table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    else if (row < first || row > last) table.RowStyles.Add(styles[row]);
                }
                table.RowCount -= removed;
                var split = Split(upper, lower, Orientation.Horizontal, fraction, name);
                table.Controls.Add(split, 0, first);
                return split;
            }
            finally { table.ResumeLayout(true); }
        }

        private static TableLayoutPanel Group(int first, int last, RowStyle[] styles)
        {
            var group = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = last - first + 1, Margin = Padding.Empty };
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = first; i <= last; i++) group.RowStyles.Add(new RowStyle(styles[i].SizeType, styles[i].Height));
            if (!group.RowStyles.Cast<RowStyle>().Any(s => s.SizeType == SizeType.Percent))
                group.RowStyles[group.RowStyles.Count - 1] = new RowStyle(SizeType.Percent, 100);
            return group;
        }

        private sealed class FlexibleSplit : SplitContainer
        {
            private readonly double initial;
            private double fraction;
            private bool positioning;
            internal FlexibleSplit(Orientation orientation, double proportion)
            {
                initial = fraction = proportion;
                Size = new Size(160, 160); Orientation = orientation;
                SplitterWidth = 8; Panel1MinSize = 64; Panel2MinSize = 64;
                BorderStyle = BorderStyle.FixedSingle; TabStop = true;
                SplitterMoved += RememberDivider;
                MouseDoubleClick += (s, e) => { if (SplitterRectangle.Contains(e.Location)) { fraction = initial; PositionDivider(); } };
            }
            protected override void OnSizeChanged(EventArgs e)
            {
                bool previous = positioning; positioning = true;
                try { base.OnSizeChanged(e); PositionDivider(); }
                finally { positioning = previous; }
            }
            private void RememberDivider(object sender, SplitterEventArgs e)
            {
                int length = (Orientation == Orientation.Vertical ? ClientSize.Width : ClientSize.Height) - SplitterWidth;
                if (!positioning && length > 0) fraction = (double)SplitterDistance / length;
            }
            private void PositionDivider()
            {
                int length = (Orientation == Orientation.Vertical ? ClientSize.Width : ClientSize.Height) - SplitterWidth;
                if (length < Panel1MinSize + Panel2MinSize || fraction <= 0) return;
                positioning = true;
                try { SplitterDistance = Math.Max(Panel1MinSize, Math.Min(length - Panel2MinSize, (int)Math.Round(length * fraction))); }
                finally { positioning = false; }
            }
        }
    }
}
