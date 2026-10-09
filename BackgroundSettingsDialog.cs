using System.Runtime.InteropServices;

namespace Backdrop;

internal sealed class BackgroundSettingsDialog : Form
{
    private readonly DarkComboBox _mode = new();
    private readonly DarkComboBox _pattern = new();
    private readonly ColorSelectionRow _color1;
    private readonly ColorSelectionRow _color2;
    private readonly TableLayoutPanel _fields = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 4, BackColor = BackdropPalette.Surface, Margin = Padding.Empty };
    private readonly Control _patternRow;

    public event EventHandler? DraftChanged;

    public BackgroundMode SelectedMode => (BackgroundMode)_mode.SelectedIndex;
    public BackgroundPattern SelectedPattern => (BackgroundPattern)_pattern.SelectedIndex;
    public string Color1Hex => _color1.ColorHex;
    public string Color2Hex => _color2.ColorHex;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            var darkMode = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int)) != 0)
                _ = DwmSetWindowAttribute(Handle, 19, ref darkMode, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    public BackgroundSettingsDialog(AppSettings settings)
    {
        Text = "Background";
        AccessibleName = "Background settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5F);
        BackColor = BackdropPalette.Window;
        ForeColor = BackdropPalette.Text;

        _mode.AccessibleName = "Background mode";
        _mode.Items.AddRange(["Automatic gradient", "Solid color", "Two-color gradient", "Pattern"]);
        _mode.SelectedIndex = (int)settings.BackgroundMode;
        _mode.DrawItem += DrawComboItem;
        _pattern.AccessibleName = "Pattern style";
        _pattern.Items.AddRange(["Soft grain", "Dots"]);
        _pattern.SelectedIndex = (int)settings.BackgroundPattern;
        _pattern.DrawItem += DrawComboItem;
        _color1 = new ColorSelectionRow("Base color", settings.BackgroundColor1Hex);
        _color2 = new ColorSelectionRow("End color", settings.BackgroundColor2Hex);

        for (var i = 0; i < 4; i++)
            _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _fields.Controls.Add(CreateComboRow("Background mode", _mode), 0, 0);
        _fields.Controls.Add(_color1, 0, 1);
        _fields.Controls.Add(_color2, 0, 2);
        _patternRow = CreateComboRow("Pattern style", _pattern);
        _fields.Controls.Add(_patternRow, 0, 3);

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Text = "Changes update the preview. Choose Save preferences in the main window to keep them.",
            ForeColor = BackdropPalette.Muted,
            Margin = new Padding(0, 12, 0, 10)
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        var apply = new BackdropButton("Apply", primary: true) { Width = 96, DialogResult = DialogResult.OK };
        var cancel = new BackdropButton("Cancel") { Width = 96, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);

        var content = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = BackdropPalette.Surface,
            Dock = DockStyle.Fill
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        for (var i = 0; i < 4; i++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Choose a background",
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
            ForeColor = BackdropPalette.Text,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);
        content.Controls.Add(_fields, 0, 1);
        content.Controls.Add(note, 0, 2);
        content.Controls.Add(buttons, 0, 3);
        Controls.Add(content);

        AcceptButton = apply;
        CancelButton = cancel;
        MinimumSize = new Size(510, 0);

        _mode.SelectedIndexChanged += (_, _) =>
        {
            UpdateVisibleFields();
            DraftChanged?.Invoke(this, EventArgs.Empty);
        };
        _pattern.SelectedIndexChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        _color1.ColorChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        _color2.ColorChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        UpdateVisibleFields();
    }

    private void UpdateVisibleFields()
    {
        _color1.Visible = SelectedMode is BackgroundMode.SolidColor or BackgroundMode.CustomGradient or BackgroundMode.Pattern;
        _color2.Visible = SelectedMode == BackgroundMode.CustomGradient;
        _patternRow.Visible = SelectedMode == BackgroundMode.Pattern;
    }

    private static Control CreateComboRow(string labelText, DarkComboBox combo)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Width = 460,
            MinimumSize = new Size(460, 40),
            Dock = DockStyle.Fill,
            BackColor = BackdropPalette.Surface,
            Margin = new Padding(0, 0, 0, 6)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = BackdropPalette.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        combo.Dock = DockStyle.Fill;
        combo.Height = 34;
        combo.Margin = new Padding(0);
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(combo, 1, 0);
        return row;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (sender is not ComboBox combo || e.Index < 0)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? BackdropPalette.Selection : BackdropPalette.Field);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]), combo.Font, e.Bounds,
            BackdropPalette.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private sealed class ColorSelectionRow : TableLayoutPanel
    {
        private readonly Label _hexLabel;
        private readonly Panel _swatch;
        private Color _color;

        public event EventHandler? ColorChanged;
        public string ColorHex => $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";

        public ColorSelectionRow(string labelText, string colorHex)
        {
            ColumnCount = 4;
            RowCount = 1;
            Width = 460;
            Height = 40;
            MinimumSize = new Size(460, 40);
            Dock = DockStyle.Fill;
            BackColor = BackdropPalette.Surface;
            Margin = new Padding(0, 0, 0, 6);
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = BackdropPalette.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };
            _hexLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = BackdropPalette.Field,
                ForeColor = BackdropPalette.Text,
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = $"{labelText} hex color"
            };
            _swatch = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 4, 0, 4),
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = $"{labelText} swatch"
            };
            var choose = new BackdropButton("Choose…")
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 3, 0, 3),
                AccessibleName = $"Choose {labelText.ToLowerInvariant()}"
            };
            choose.Click += (_, _) => ChooseColor();
            Controls.Add(label, 0, 0);
            Controls.Add(_hexLabel, 1, 0);
            Controls.Add(_swatch, 2, 0);
            Controls.Add(choose, 3, 0);
            SetColor(colorHex);
        }

        private void ChooseColor()
        {
            using var dialog = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                SolidColorOnly = true,
                Color = _color
            };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            SetColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetColor(string hex)
        {
            _color = ColorTranslator.FromHtml(hex);
            _hexLabel.Text = ColorHex;
            _swatch.BackColor = _color;
        }
    }
}
