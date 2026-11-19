using System;
using System.Drawing;
using System.Windows.Forms;
using NavPro.Frontend.Ui;
using NavPro.Classes;

namespace NavPro.Frontend.Dialogs
{
    /// <summary>Read-only NavPro support details. It never opens a mail client or sends mail.</summary>
    public sealed class ContactInfoDialog : Form
    {
        public const string EmailAddress = NavProIdentity.ContactEmail;
        private readonly PictureBox artwork;
        private readonly Label feedback;

        public ContactInfoDialog()
        {
            Text = "NavPro — Contact Info";
            ClientSize = new Size(560, 250);
            MinimumSize = new Size(480, 240);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.White;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            artwork = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.CenterImage };
            using (var stream = typeof(ContactInfoDialog).Assembly.GetManifestResourceStream("NavPro.Assets.ContactInfo32.png"))
            {
                if (stream != null) using (var source = Image.FromStream(stream)) artwork.Image = new Bitmap(source);
            }
            header.Controls.Add(artwork, 0, 0);
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heading.Controls.Add(new Label { Text = "Technical support", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.FromArgb(26, 74, 111), Dock = DockStyle.Fill }, 0, 0);
            heading.Controls.Add(new Label { Text = "Get help with your NavPro workflows", ForeColor = Color.FromArgb(86, 104, 120), Dock = DockStyle.Fill }, 0, 1);
            header.Controls.Add(heading, 1, 0);
            root.Controls.Add(header, 0, 0);

            var details = new GroupBox { Text = "Contact details", Dock = DockStyle.Fill, Padding = new Padding(12), ForeColor = Color.FromArgb(38, 74, 102) };
            details.Controls.Add(new TextBox { Name = "ContactDetails", Text = EmailAddress, ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Color.FromArgb(26, 74, 111), Font = new Font("Segoe UI", 11F), TabStop = true });
            root.Controls.Add(details, 0, 1);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 0) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(0, 110, 104), TextAlign = ContentAlignment.MiddleLeft };
            var copy = new Button { Text = "Copy email", Dock = DockStyle.Fill, UseVisualStyleBackColor = true };
            copy.Click += (_, __) => CopyEmail();
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Fill, UseVisualStyleBackColor = true };
            actions.Controls.Add(feedback, 0, 0);
            actions.Controls.Add(copy, 1, 0);
            actions.Controls.Add(close, 2, 0);
            root.Controls.Add(actions, 0, 2);
            AcceptButton = close;
            CancelButton = close;
            Shown += (_, __) => close.Focus();
            DialogTheme.Apply(this, copy);
        }

        private void CopyEmail()
        {
            try { Clipboard.SetText(EmailAddress); feedback.Text = "Email copied"; }
            catch (Exception) { feedback.Text = "Select and copy the email manually."; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) artwork?.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
