using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NavPro.Frontend.Ui
{
    /// <summary>Local UI preferences only. Never persists model rows, checked items or service credentials.</summary>
    internal static class WindowPreferences
    {
        internal static string StorageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NavPro", "WindowPreferences");
        private static readonly ConditionalWeakTable<Form, object> Attached = new ConditionalWeakTable<Form, object>();
        public sealed class State
        {
            public int Schema { get; set; } = 1;
            public int X { get; set; } public int Y { get; set; } public int Width { get; set; } public int Height { get; set; }
            public bool Maximized { get; set; }
            public float Dpi { get; set; } = 96;
            public Dictionary<string, double> Dividers { get; set; } = new Dictionary<string, double>();
            public Dictionary<string, string> Filters { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, int> Columns { get; set; } = new Dictionary<string, int>();
        }
        internal static IEnumerable<Control> Descendants(Control parent)
        { foreach (Control child in parent.Controls) { yield return child; foreach (var next in Descendants(child)) yield return next; } }
        private static string Key(Control control)
        {
            if (!string.IsNullOrEmpty(control.Name)) return control.GetType().Name + ":" + control.Name;
            if (!string.IsNullOrEmpty(control.AccessibleName)) return control.GetType().Name + ":" + control.AccessibleName;
            return (control.Parent is Form ? "" : control.Parent == null ? "" : Key(control.Parent) + "/") + control.GetType().Name + ":" + (control.Parent?.Controls.GetChildIndex(control) ?? 0);
        }
        private static bool IsFilter(TextBox box) => !box.UseSystemPasswordChar && ((box.AccessibleName ?? "").IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0 || box.Name.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0);
        private static string PathFor(Form form) => Path.Combine(StorageRoot, form.GetType().FullName + (string.IsNullOrEmpty(form.Name) ? "" : "." + form.Name) + ".json");
        internal static void Attach(Form form)
        {
            if (Attached.TryGetValue(form, out _)) return;
            Attached.Add(form, new object());
            form.Shown += (s, e) => Restore(form);
            form.FormClosed += (s, e) => Save(form);
        }
        internal static void Save(Form form)
        {
            string temp = null;
            try
            {
                var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
                if (!Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, bounds).Width >= 100 && Rectangle.Intersect(screen.WorkingArea, bounds).Height >= 80)) return;
                var state = new State { X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Maximized = form.WindowState == FormWindowState.Maximized, Dpi = form.DeviceDpi };
                foreach (var control in Descendants(form))
                {
                    if (control is SplitContainer split)
                    {
                        int extent = (split.Orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height) - split.SplitterWidth;
                        if (extent > 0) state.Dividers[Key(split)] = (double)split.SplitterDistance / extent;
                    }
                    if (control is TextBox box && IsFilter(box)) state.Filters[Key(box)] = box.Text.Substring(0, Math.Min(box.Text.Length, 2000));
                    if (control is DataGridView grid)
                        foreach (DataGridViewColumn column in grid.Columns) if (column.Visible) state.Columns[Key(grid) + "/" + column.Name + "/" + column.HeaderText] = column.Width;
                }
                Directory.CreateDirectory(StorageRoot);
                string path = PathFor(form); temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(state));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            { form.AccessibleDescription = "Window preferences could not be saved. " + ex.GetType().Name; }
            finally { if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }
        internal static void Restore(Form form)
        {
            try
            {
                string path = PathFor(form); if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
                var state = new JavaScriptSerializer().Deserialize<State>(File.ReadAllText(path));
                if (state == null || state.Schema != 1 || state.Width < 100 || state.Height < 100 || state.Width > 20000 || state.Height > 20000 || state.X < -100000 || state.X > 100000 || state.Y < -100000 || state.Y > 100000) return;
                float scale = form.DeviceDpi / (state.Dpi >= 48 && state.Dpi <= 768 ? state.Dpi : 96f);
                var saved = new Rectangle(state.X, state.Y, (int)(state.Width * scale), (int)(state.Height * scale));
                var screen = Screen.FromRectangle(saved).WorkingArea;
                int width = Math.Max(form.MinimumSize.Width, Math.Min(saved.Width, screen.Width));
                int height = Math.Max(form.MinimumSize.Height, Math.Min(saved.Height, screen.Height));
                if (form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow)
                    form.Size = new Size(width, height);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(Math.Max(screen.Left, Math.Min(saved.X, screen.Right - Math.Min(form.Width, screen.Width))), Math.Max(screen.Top, Math.Min(saved.Y, screen.Bottom - Math.Min(form.Height, screen.Height))));
                foreach (var control in Descendants(form))
                {
                    if (control is SplitContainer split && state.Dividers != null && state.Dividers.TryGetValue(Key(split), out double ratio) && ratio > 0 && ratio < 1)
                    {
                        int length = (split.Orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height) - split.SplitterWidth;
                        if (length >= split.Panel1MinSize + split.Panel2MinSize) split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(length - split.Panel2MinSize, (int)Math.Round(length * ratio)));
                    }
                    if (control is TextBox box && IsFilter(box) && state.Filters != null && state.Filters.TryGetValue(Key(box), out string value)) box.Text = (value ?? "").Substring(0, Math.Min(value?.Length ?? 0, 2000));
                    if (control is DataGridView grid && state.Columns != null)
                        foreach (DataGridViewColumn column in grid.Columns)
                            if (column.Visible && state.Columns.TryGetValue(Key(grid) + "/" + column.Name + "/" + column.HeaderText, out int columnWidth) && columnWidth >= 20 && columnWidth <= 5000)
                            { column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; column.Width = Math.Max(column.MinimumWidth, (int)(columnWidth * scale)); }
                }
                if (state.Maximized && form.MaximizeBox) form.WindowState = FormWindowState.Maximized;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            { form.AccessibleDescription = "Saved window preferences were ignored. " + ex.GetType().Name; }
        }
    }
}
