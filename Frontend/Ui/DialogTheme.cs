using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NavPro.Frontend.Ui
{
    /// <summary>NavPro magenta theme sampled from the supplied swatch; see Docs/Jacobs-Branding.md.</summary>
    public static class DialogTheme
    {
        public static readonly Color Canvas = Color.FromArgb(250, 247, 250); // light magenta-tinted workspace
        public static readonly Color Ink = Color.FromArgb(51, 43, 55); // high-contrast neutral ink
        public static readonly Color Brand = Color.FromArgb(165, 0, 164); // sampled swatch #A500A4
        public static readonly Color BrandDeep = Color.FromArgb(124, 0, 123);
        public static readonly Color Primary = Brand;
        public static readonly Color Highlight = Color.FromArgb(211, 112, 207); // magenta focus/accent
        public static readonly Color Warm = Color.FromArgb(255, 160, 20); // Yellow P1
        public static readonly Color Neutral = Color.FromArgb(235, 231, 235); // disabled/read-only neutral
        public static readonly Color Selection = Color.FromArgb(244, 221, 243); // light magenta selection
        public static readonly string FontFamilyName = ResolveFontFamily();
        private static string ResolveFontFamily()
        {
            using(var fonts=new System.Drawing.Text.InstalledFontCollection())
            {
                var names=fonts.Families.Select(f=>f.Name).ToArray();
                return names.FirstOrDefault(n=>string.Equals(n,"Jacobs Chronos",System.StringComparison.OrdinalIgnoreCase))
                    ?? names.FirstOrDefault(n=>string.Equals(n,"Arial",System.StringComparison.OrdinalIgnoreCase)) ?? SystemFonts.MessageBoxFont.FontFamily.Name;
            }
        }
        public static Font CreateFont(float size,FontStyle style=FontStyle.Regular)=>new Font(FontFamilyName,size,style);
        public static readonly Color Surface = Color.White;

        public static void Apply(Form form, params Button[] primaryActions)
        {
            if (form == null) return;
            WindowPreferences.Attach(form);
            form.BackColor = Canvas;
            form.ForeColor = Ink;
            // Preserve point sizes, emphasis and event/keyboard wiring; replace family only.
            Style(form);
            foreach (var button in (primaryActions ?? new Button[0]).Where(b => b != null))
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.BackColor = Primary;
                button.ForeColor = Color.White;
                button.FlatAppearance.MouseOverBackColor = BrandDeep;
                button.FlatAppearance.MouseDownBackColor = BrandDeep;
                button.Font = new Font(button.Font, FontStyle.Bold);
                System.Action refresh=()=>{button.BackColor=button.Enabled?Primary:Neutral;button.ForeColor=button.Enabled?Color.White:Ink;button.FlatAppearance.BorderSize=button.Enabled?0:1;button.FlatAppearance.BorderColor=Color.FromArgb(165,165,165);};
                button.EnabledChanged+=(_,__)=>refresh();refresh();
            }
        }

        private static void Style(Control control,bool onBrand=false)
        {
            if(control.Font.FontFamily.Name!=FontFamilyName)control.Font=CreateFont(control.Font.SizeInPoints,control.Font.Style);
            onBrand=onBrand||IsLegacyHeader(control.BackColor)||control.BackColor==Brand;
            if (control is Form) control.BackColor = Canvas;
            else if (IsLegacyHeader(control.BackColor)) control.BackColor = Brand;
            else if (control is TabPage) control.BackColor = Canvas;
            else if(control.BackColor==Color.FromArgb(241,244,248)||control.BackColor==Color.FromArgb(244,247,249))control.BackColor=Canvas;
            if(onBrand)control.ForeColor=Color.White;
            else if(control is Label || control is CheckBox || control is RadioButton)control.ForeColor=Ink;

            if (control is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(165,165,165);
                if (!IsLegacyHeader(button.BackColor)) button.BackColor = Surface;
                if (button.ForeColor == Color.White && IsLegacyHeader(button.BackColor)) button.ForeColor = Color.White;
                else button.ForeColor = Ink;
                button.FlatAppearance.MouseOverBackColor = Selection;
                button.Cursor = Cursors.Hand;
            }
            else if (control is DataGridView grid)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Brand;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.Font=CreateFont(grid.Font.SizeInPoints,FontStyle.Bold);
                grid.DefaultCellStyle.Font=grid.Font;
                grid.DefaultCellStyle.ForeColor = Ink;
                grid.DefaultCellStyle.SelectionBackColor = Selection;
                grid.DefaultCellStyle.SelectionForeColor = Ink;
                grid.BackgroundColor = Surface;
                grid.GridColor = Neutral;
            }
            else if (control is TextBox || control is ComboBox || control is ListBox || control is CheckedListBox)
            {
                control.BackColor = control is TextBox text && text.ReadOnly ? Neutral : Surface;
                control.ForeColor = Ink;
            }
            else if (control is GroupBox) { control.BackColor = Surface; control.ForeColor = Primary; }
            else if(control is TabControl tabs)
            {
                if(tabs.DrawMode!=TabDrawMode.OwnerDrawFixed)
                {
                    tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.Padding=new Point(12,6);
                    tabs.DrawItem+=(_,e)=>{bool selected=e.Index==tabs.SelectedIndex;using(var brush=new SolidBrush(selected?Primary:Neutral))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,e.Bounds,selected?Color.White:Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
                }
            }

            foreach (Control child in control.Controls) Style(child,onBrand);
        }

        private static bool IsLegacyHeader(Color color) =>
            color == Color.FromArgb(20, 39, 58) || color == Color.FromArgb(25, 42, 59) || color == Color.FromArgb(16, 37, 59) || color == Color.FromArgb(36,84,115) || color == Color.FromArgb(35,30,220) || color == Color.FromArgb(0,111,139) || color == Color.FromArgb(0,112,135) || color == Color.FromArgb(165,0,164);
    }
}
