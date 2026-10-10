using System.Globalization;
using System.Runtime.InteropServices;

namespace Backdrop;

internal sealed class BackgroundSettingsDialog : Form
{
    private readonly BackdropDropdown _mode = new();
    private readonly BackdropDropdown _pattern = new();
    private readonly BackdropNumberSelector _autoGradientLightenPercent = new() { Minimum = 0, Maximum = 100, Increment = 5 };
    private readonly ColorSelectionRow _color1;
    private readonly ColorSelectionRow _color2;
    private readonly TableLayoutPanel _fields = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 5, BackColor = BackdropPalette.Surface, Margin = Padding.Empty };
    private readonly Control _autoGradientLightenRow;
    private readonly Control _patternRow;

    public event EventHandler? DraftChanged;

    public BackgroundMode SelectedMode => (BackgroundMode)_mode.SelectedIndex;
    public BackgroundPattern SelectedPattern => (BackgroundPattern)_pattern.SelectedIndex;
    public int AutoGradientLightenPercent => (int)_autoGradientLightenPercent.Value;
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
        _pattern.AccessibleName = "Pattern style";
        _pattern.Items.AddRange(["Soft grain", "Dots"]);
        _pattern.SelectedIndex = (int)settings.BackgroundPattern;
        _autoGradientLightenPercent.Value = settings.AutoGradientLightenPercent;
        _autoGradientLightenPercent.BackColor = BackdropPalette.Field;
        _autoGradientLightenPercent.ForeColor = BackdropPalette.Text;
        _autoGradientLightenPercent.AccessibleName = "Automatic gradient color lightening";
        _autoGradientLightenPercent.AccessibleDescription = "Blend automatic gradient colors with white by this percentage. Zero keeps the sampled colors.";
        _color1 = new ColorSelectionRow("Base color", settings.BackgroundColor1Hex);
        _color2 = new ColorSelectionRow("End color", settings.BackgroundColor2Hex);

        for (var i = 0; i < 5; i++)
            _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _fields.Controls.Add(CreateComboRow("Background mode", _mode), 0, 0);
        _autoGradientLightenRow = CreateNumericRow("Lighten colors (%)", _autoGradientLightenPercent);
        _fields.Controls.Add(_autoGradientLightenRow, 0, 1);
        _fields.Controls.Add(_color1, 0, 2);
        _fields.Controls.Add(_color2, 0, 3);
        _patternRow = CreateComboRow("Pattern style", _pattern);
        _fields.Controls.Add(_patternRow, 0, 4);

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
        var apply = new BackdropButton("Apply", primary: true) { Width = 96 };
        apply.Click += (_, _) =>
        {
            var invalidColor = new[] { _color1, _color2 }.FirstOrDefault(row => row.Visible && !row.HasValidHex);
            if (invalidColor is not null)
            {
                invalidColor.FocusEditor();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
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
        _autoGradientLightenPercent.ValueChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        _pattern.SelectedIndexChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        _color1.ColorChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        _color2.ColorChanged += (_, _) => DraftChanged?.Invoke(this, EventArgs.Empty);
        UpdateVisibleFields();
    }

    private void UpdateVisibleFields()
    {
        _autoGradientLightenRow.Visible = SelectedMode == BackgroundMode.AutoGradient;
        _color1.Visible = SelectedMode is BackgroundMode.SolidColor or BackgroundMode.CustomGradient or BackgroundMode.Pattern;
        _color2.Visible = SelectedMode == BackgroundMode.CustomGradient;
        _patternRow.Visible = SelectedMode == BackgroundMode.Pattern;
    }

    private static Control CreateComboRow(string labelText, BackdropDropdown combo)
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
        combo.Height = 38;
        combo.MinimumSize = new Size(0, 38);
        combo.Margin = new Padding(0);
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(combo, 1, 0);
        return row;
    }

    private static Control CreateNumericRow(string labelText, BackdropNumberSelector numeric)
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
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = BackdropPalette.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        numeric.Dock = DockStyle.Fill;
        numeric.Height = 38;
        numeric.MinimumSize = new Size(0, 38);
        numeric.Font = new Font("Segoe UI", 10F);
        numeric.Margin = new Padding(0);
        numeric.RefreshAccessibility();
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(numeric, 1, 0);
        return row;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private sealed class ColorSelectionRow : TableLayoutPanel
    {
        private static readonly string[] QuickColors =
        [
            "#334155", "#1E3A5F", "#267A69", "#A7564A",
            "#D5B98A", "#EEE3D2", "#DCE8E6", "#E6D8EA"
        ];
        private readonly TextBox _hexEditor;
        private readonly Panel _swatch;
        private readonly string _labelText;
        private Color _color;

        public event EventHandler? ColorChanged;
        public string ColorHex => $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
        public bool HasValidHex { get; private set; } = true;

        public ColorSelectionRow(string labelText, string colorHex)
        {
            _labelText = labelText;
            ColumnCount = 2;
            RowCount = 2;
            Width = 460;
            Height = 68;
            MinimumSize = new Size(460, 68);
            Dock = DockStyle.Fill;
            BackColor = BackdropPalette.Surface;
            Margin = new Padding(0, 0, 0, 6);
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = BackdropPalette.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };
            var controls = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = BackdropPalette.Surface,
                Margin = Padding.Empty
            };
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _hexEditor = new TextBox
            {
                Dock = DockStyle.Fill,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 7,
                BackColor = BackdropPalette.Field,
                ForeColor = BackdropPalette.Text,
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = $"{labelText} hex color",
                AccessibleDescription = "Enter an opaque color in the format #RRGGBB."
            };
            _swatch = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 6, 0, 6),
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = $"{labelText} swatch"
            };
            var choose = new BackdropButton("Advanced")
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 2, 0, 2),
                AccessibleName = $"Open advanced picker for {labelText.ToLowerInvariant()}"
            };
            choose.Click += (_, _) => ChooseColor();
            controls.Controls.Add(_hexEditor, 0, 0);
            controls.Controls.Add(_swatch, 1, 0);
            controls.Controls.Add(choose, 2, 0);

            var paletteLabel = new Label
            {
                Text = "Quick colors",
                AutoSize = true,
                ForeColor = BackdropPalette.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };
            var palette = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackdropPalette.Surface,
                Margin = Padding.Empty
            };
            foreach (var hex in QuickColors)
            {
                var color = ColorTranslator.FromHtml(hex);
                var swatchButton = new Button
                {
                    Width = 23,
                    Height = 21,
                    Margin = new Padding(0, 2, 5, 1),
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false,
                    BackColor = color,
                    AccessibleRole = AccessibleRole.PushButton,
                    AccessibleName = $"Set {labelText.ToLowerInvariant()} to {hex}"
                };
                swatchButton.FlatAppearance.BorderSize = 1;
                swatchButton.FlatAppearance.BorderColor = BackdropPalette.Border;
                swatchButton.Click += (_, _) => SetColor(color);
                palette.Controls.Add(swatchButton);
            }

            Controls.Add(label, 0, 0);
            Controls.Add(controls, 1, 0);
            Controls.Add(paletteLabel, 0, 1);
            Controls.Add(palette, 1, 1);
            _hexEditor.TextChanged += HexEditor_TextChanged;
            SetColor(colorHex);
        }

        private void ChooseColor()
        {
            using var dialog = new BackdropColorPickerDialog(_color);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            SetColor(dialog.SelectedColor);
        }

        private void SetColor(string hex)
        {
            if (!TryParseHex(hex, out var color))
                throw new ArgumentException("Enter a color in the format #RRGGBB.", nameof(hex));
            SetColor(color);
        }

        public void FocusEditor() => _hexEditor.Focus();

        private void SetColor(Color color)
        {
            var changed = _color.ToArgb() != Color.FromArgb(color.R, color.G, color.B).ToArgb();
            _color = Color.FromArgb(color.R, color.G, color.B);
            HasValidHex = true;
            _hexEditor.BackColor = BackdropPalette.Field;
            if (_hexEditor.Text != ColorHex)
                _hexEditor.Text = ColorHex;
            _swatch.BackColor = _color;
            if (changed)
                ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HexEditor_TextChanged(object? sender, EventArgs e)
        {
            if (!TryParseHex(_hexEditor.Text, out var color))
            {
                HasValidHex = false;
                _hexEditor.BackColor = Color.FromArgb(74, 42, 47);
                return;
            }

            var changed = !HasValidHex || _color.ToArgb() != color.ToArgb();
            HasValidHex = true;
            _hexEditor.BackColor = BackdropPalette.Field;
            if (!changed)
                return;
            _color = color;
            _swatch.BackColor = _color;
            if (_hexEditor.Text != ColorHex)
                _hexEditor.Text = ColorHex;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private static bool TryParseHex(string value, out Color color)
        {
            color = default;
            if (value.Length != 7 || value[0] != '#' ||
                !uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var argb))
                return false;
            color = Color.FromArgb((int)(argb >> 16) & 255, (int)(argb >> 8) & 255, (int)argb & 255);
            return true;
        }
    }
}
