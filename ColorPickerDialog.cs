using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Backdrop;

internal sealed class BackdropColorPickerDialog : Form
{
    private readonly Color _current;
    private readonly ColorSwatch _currentSwatch = new() { AccessibleName = "Current color swatch", AccessibleRole = AccessibleRole.Graphic };
    private readonly ColorSwatch _newSwatch = new() { AccessibleName = "New color swatch", AccessibleRole = AccessibleRole.Graphic };
    private readonly Label _currentHex = new() { AutoSize = true, ForeColor = BackdropPalette.Text };
    private readonly Label _newHex = new() { AutoSize = true, ForeColor = BackdropPalette.Text };
    private readonly TextBox _hex = new()
    {
        CharacterCasing = CharacterCasing.Upper,
        MaxLength = 7,
        BackColor = BackdropPalette.Field,
        ForeColor = BackdropPalette.Text,
        BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = "Hex color",
        AccessibleDescription = "Enter an opaque RGB color in the format #RRGGBB.",
        TabIndex = 2
    };
    private readonly Label _status = new()
    {
        AutoSize = true,
        ForeColor = BackdropPalette.Muted,
        AccessibleName = "Color entry status",
        AccessibleRole = AccessibleRole.StatusBar,
        Text = "Opaque RGB color in #RRGGBB format."
    };
    private readonly HueBar _hue = new() { TabIndex = 1 };
    private readonly SaturationValuePlane _plane = new() { TabIndex = 0 };
    private readonly BackdropButton _apply = new("Apply", primary: true) { Width = 96, Height = 36, TabIndex = 3 };
    private readonly BackdropButton _cancel = new("Cancel") { Width = 96, Height = 36, DialogResult = DialogResult.Cancel, TabIndex = 4 };
    private bool _syncing;
    private bool _updatingHex;
    private Color _candidate;

    public Color SelectedColor => _candidate;

    public BackdropColorPickerDialog(Color initialColor)
    {
        _current = Opaque(initialColor);
        _candidate = _current;
        Text = "Custom color";
        AccessibleName = "Custom color picker";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5F);
        BackColor = BackdropPalette.Window;
        ForeColor = BackdropPalette.Text;

        _currentSwatch.BackColor = _current;
        _currentSwatch.AccessibleDescription = ToHex(_current);
        _currentHex.Text = ToHex(_current);

        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18),
            BackColor = BackdropPalette.Surface,
            Dock = DockStyle.Fill
        };
        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Choose a color",
            Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
            ForeColor = BackdropPalette.Text,
            Margin = new Padding(0, 0, 0, 4)
        });
        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Adjust hue and saturation / brightness, or enter an RGB hex value.",
            ForeColor = BackdropPalette.Muted,
            Margin = new Padding(0, 0, 0, 10)
        });
        content.Controls.Add(CreatePickerRow());
        content.Controls.Add(CreateHexRow());
        _status.Margin = new Padding(122, 0, 0, 12);
        content.Controls.Add(_status);
        content.Controls.Add(CreateButtonRow());
        Controls.Add(content);

        _hue.HueChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _plane.Hue = _hue.Hue;
            UpdateFromPicker();
        };
        _plane.PositionChanged += (_, _) =>
        {
            if (!_syncing)
                UpdateFromPicker();
        };
        _hex.TextChanged += Hex_TextChanged;
        _apply.Click += (_, _) =>
        {
            if (!TryParseHex(_hex.Text, out _))
            {
                _hex.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        AcceptButton = _apply;
        CancelButton = _cancel;
        SetCandidate(_current, updatePicker: true, updateHex: true);
        Shown += (_, _) => _hex.Focus();
    }

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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private Control CreatePickerRow()
    {
        var row = new Panel { Size = new Size(430, 184), BackColor = BackdropPalette.Surface, Margin = new Padding(0, 0, 0, 12) };
        AddLabel(row, "Saturation and brightness", 0, 0);
        AddLabel(row, "Hue", 258, 0);
        AddLabel(row, "Preview", 308, 0);
        _plane.SetBounds(0, 20, 246, 160);
        _hue.SetBounds(267, 20, 20, 160);
        row.Controls.Add(_plane);
        row.Controls.Add(_hue);
        AddSwatch(row, _currentSwatch, _currentHex, "Current", 20);
        AddSwatch(row, _newSwatch, _newHex, "New", 98);
        return row;
    }

    private Control CreateHexRow()
    {
        var row = new Panel { Size = new Size(430, 36), BackColor = BackdropPalette.Surface, Margin = Padding.Empty };
        var label = MakeLabel("Hex color");
        label.AutoSize = false;
        label.SetBounds(0, 0, 114, 36);
        label.TextAlign = ContentAlignment.MiddleLeft;
        var field = new Panel { Location = new Point(122, 0), Size = new Size(308, 36), Padding = new Padding(8, 3, 8, 3), BackColor = BackdropPalette.Surface };
        field.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(field.ClientRectangle, -0.5f, -0.5f), 5);
            using var fill = new SolidBrush(BackdropPalette.Field);
            using var border = new Pen(BackdropPalette.Border);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        };
        _hex.BorderStyle = BorderStyle.None;
        _hex.Dock = DockStyle.Fill;
        field.Controls.Add(_hex);
        row.Controls.AddRange([label, field]);
        return row;
    }

    private static void AddLabel(Control parent, string text, int x, int y)
    {
        var label = MakeLabel(text);
        label.Location = new Point(x, y);
        parent.Controls.Add(label);
    }

    private static void AddSwatch(Control parent, ColorSwatch swatch, Label hex, string label, int y)
    {
        swatch.SetBounds(308, y + 8, 42, 42);
        hex.Font = new Font("Segoe UI", 8.25F);
        hex.Location = new Point(358, y + 27);
        var caption = MakeLabel(label);
        caption.Location = new Point(358, y + 5);
        parent.Controls.AddRange([swatch, caption, hex]);
    }

    private Control CreateButtonRow()
    {
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            BackColor = BackdropPalette.Surface,
            Margin = Padding.Empty
        };
        _apply.Margin = Padding.Empty;
        _cancel.Margin = new Padding(0, 0, 10, 0);
        buttons.Controls.Add(_apply);
        buttons.Controls.Add(_cancel);
        return buttons;
    }

    private static Label MakeLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        ForeColor = BackdropPalette.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 0, 4)
    };

    private sealed class ColorSwatch : Control
    {
        public ColorSwatch() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(ClientRectangle, -0.5f, -0.5f), 5);
            using var fill = new SolidBrush(BackColor);
            using var border = new Pen(BackdropPalette.Border);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }
    }

    private void Hex_TextChanged(object? sender, EventArgs e)
    {
        if (_updatingHex)
            return;
        if (!TryParseHex(_hex.Text, out var color))
        {
            _apply.Enabled = false;
            _hex.BackColor = Color.FromArgb(74, 42, 47);
            _status.Text = "Enter exactly #RRGGBB.";
            _status.ForeColor = Color.FromArgb(238, 129, 135);
            return;
        }
        SetCandidate(color, updatePicker: true, updateHex: false);
    }

    private void UpdateFromPicker() => SetCandidate(
        FromHsv(_hue.Hue, _plane.Saturation, _plane.Brightness), updatePicker: false, updateHex: true);

    private void SetCandidate(Color color, bool updatePicker, bool updateHex)
    {
        _candidate = Opaque(color);
        var hex = ToHex(_candidate);
        _newSwatch.BackColor = _candidate;
        _newSwatch.AccessibleDescription = hex;
        _newHex.Text = hex;
        _hex.BackColor = BackdropPalette.Field;
        _status.Text = "Opaque RGB color in #RRGGBB format.";
        _status.ForeColor = BackdropPalette.Muted;
        _apply.Enabled = true;
        if (updatePicker)
            SetPickerPosition(_candidate);
        if (updateHex && _hex.Text != hex)
        {
            _updatingHex = true;
            _hex.Text = hex;
            _updatingHex = false;
        }
    }

    private void SetPickerPosition(Color color)
    {
        ToHsv(color, out var hue, out var saturation, out var brightness);
        _syncing = true;
        _hue.Hue = hue;
        _plane.Hue = hue;
        _plane.SetPosition(saturation, brightness, notify: false);
        _syncing = false;
    }

    private static Color Opaque(Color color) => Color.FromArgb(color.R, color.G, color.B);
    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryParseHex(string text, out Color color)
    {
        color = default;
        if (text.Length != 7 || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;
        color = Color.FromArgb((int)(value >> 16) & 255, (int)(value >> 8) & 255, (int)value & 255);
        return true;
    }

    private static void ToHsv(Color color, out int hue, out double saturation, out double brightness)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        var degrees = delta == 0 ? 0 : max == red ? 60 * (((green - blue) / delta) % 6) :
            max == green ? 60 * (((blue - red) / delta) + 2) : 60 * (((red - green) / delta) + 4);
        hue = (int)Math.Round((degrees + 360) % 360);
        saturation = max == 0 ? 0 : delta / max;
        brightness = max;
    }

    private static Color FromHsv(int hue, double saturation, double brightness)
    {
        var chroma = brightness * saturation;
        var secondary = chroma * (1 - Math.Abs(hue / 60d % 2 - 1));
        var match = brightness - chroma;
        var (red, green, blue) = (hue / 60) switch
        {
            0 => (chroma, secondary, 0d),
            1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary),
            3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        return Color.FromArgb((int)Math.Round((red + match) * 255),
            (int)Math.Round((green + match) * 255), (int)Math.Round((blue + match) * 255));
    }

    private sealed class HueBar : Control
    {
        private readonly Bitmap _spectrum = MakeSpectrum();
        private int _hue;
        public event EventHandler? HueChanged;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Hue
        {
            get => _hue;
            set
            {
                var next = Math.Clamp(value, 0, 359);
                if (_hue == next) return;
                _hue = next;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                HueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public HueBar()
        {
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = "Hue";
            AccessibleDescription = "Use the arrow keys to change hue; hold Shift for larger steps.";
            TabStop = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            using var path = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(ClientRectangle, -0.5f, -0.5f), 4);
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path);
            e.Graphics.DrawImage(_spectrum, ClientRectangle);
            e.Graphics.Restore(state);
            var y = (int)Math.Round(_hue / 359d * Math.Max(0, Height - 1));
            using var outline = new Pen(Color.Black, 3);
            using var marker = new Pen(Color.White, 1);
            e.Graphics.DrawLine(outline, 0, y, Width - 1, y);
            e.Graphics.DrawLine(marker, 0, y, Width - 1, y);
            using var border = new Pen(BackdropPalette.Border);
            e.Graphics.DrawPath(border, path);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetHue(e.Y); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (Capture) SetHue(e.Y); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var step = e.Shift ? 10 : 1;
            if (e.KeyCode is Keys.Up or Keys.Right) Hue = (Hue + step) % 360;
            else if (e.KeyCode is Keys.Down or Keys.Left) Hue = (Hue - step + 360) % 360;
            else if (e.KeyCode == Keys.Home) Hue = 0;
            else if (e.KeyCode == Keys.End) Hue = 359;
            else { base.OnKeyDown(e); return; }
            e.Handled = e.SuppressKeyPress = true;
            base.OnKeyDown(e);
        }

        private void SetHue(int y)
        {
            if (Height > 1) Hue = (int)Math.Round(Math.Clamp(y, 0, Height - 1) * 359d / (Height - 1));
        }

        private static Bitmap MakeSpectrum()
        {
            var bitmap = new Bitmap(1, 360, PixelFormat.Format32bppPArgb);
            for (var y = 0; y < 360; y++) bitmap.SetPixel(0, y, FromHsv(y, 1, 1));
            return bitmap;
        }

        protected override void Dispose(bool disposing) { if (disposing) _spectrum.Dispose(); base.Dispose(disposing); }
    }

    private sealed class SaturationValuePlane : Control
    {
        private Bitmap? _bitmap;
        private int _hue = -1;
        private double _saturation = 1;
        private double _brightness = 1;
        public event EventHandler? PositionChanged;
        public double Saturation => _saturation;
        public double Brightness => _brightness;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Hue
        {
            get => _hue;
            set
            {
                var next = Math.Clamp(value, 0, 359);
                if (_hue == next) return;
                _hue = next;
                RebuildBitmap();
                Invalidate();
            }
        }

        public SaturationValuePlane()
        {
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = "Saturation and brightness";
            AccessibleDescription = "Use Left and Right to adjust saturation; Up and Down adjust brightness. Hold Shift for larger steps.";
            TabStop = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        public void SetPosition(double saturation, double brightness, bool notify)
        {
            saturation = Math.Clamp(saturation, 0, 1);
            brightness = Math.Clamp(brightness, 0, 1);
            if (Math.Abs(saturation - _saturation) < 0.0001 && Math.Abs(brightness - _brightness) < 0.0001) return;
            _saturation = saturation;
            _brightness = brightness;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            if (notify) PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_bitmap is null) RebuildBitmap();
            using var path = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(ClientRectangle, -0.5f, -0.5f), 4);
            if (_bitmap is not null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                var state = e.Graphics.Save();
                e.Graphics.SetClip(path);
                e.Graphics.DrawImage(_bitmap, ClientRectangle);
                e.Graphics.Restore(state);
            }
            var x = (int)Math.Round(_saturation * Math.Max(0, Width - 1));
            var y = (int)Math.Round((1 - _brightness) * Math.Max(0, Height - 1));
            using var outline = new Pen(Color.Black, 3);
            using var marker = new Pen(Color.White, 1);
            e.Graphics.DrawEllipse(outline, x - 6, y - 6, 12, 12);
            e.Graphics.DrawEllipse(marker, x - 6, y - 6, 12, 12);
            using var border = new Pen(BackdropPalette.Border);
            e.Graphics.DrawPath(border, path);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromPoint(e.Location); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (Capture) SetFromPoint(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var step = e.Shift ? 0.1 : 0.02;
            if (e.KeyCode == Keys.Left) SetPosition(_saturation - step, _brightness, true);
            else if (e.KeyCode == Keys.Right) SetPosition(_saturation + step, _brightness, true);
            else if (e.KeyCode == Keys.Up) SetPosition(_saturation, _brightness + step, true);
            else if (e.KeyCode == Keys.Down) SetPosition(_saturation, _brightness - step, true);
            else if (e.KeyCode == Keys.Home) SetPosition(0, _brightness, true);
            else if (e.KeyCode == Keys.End) SetPosition(1, _brightness, true);
            else { base.OnKeyDown(e); return; }
            e.Handled = e.SuppressKeyPress = true;
            base.OnKeyDown(e);
        }

        private void SetFromPoint(Point point)
        {
            if (Width < 2 || Height < 2) return;
            SetPosition(Math.Clamp(point.X, 0, Width - 1) / (double)(Width - 1),
                1 - Math.Clamp(point.Y, 0, Height - 1) / (double)(Height - 1), true);
        }

        private void RebuildBitmap()
        {
            _bitmap?.Dispose();
            const int width = 256, height = 160;
            _bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var bounds = new Rectangle(0, 0, width, height);
            using var graphics = Graphics.FromImage(_bitmap);
            using var horizontal = new LinearGradientBrush(bounds, Color.White, FromHsv(Math.Max(0, _hue), 1, 1), LinearGradientMode.Horizontal);
            graphics.FillRectangle(horizontal, bounds);
            using var vertical = new LinearGradientBrush(bounds, Color.FromArgb(0, 0, 0, 0), Color.Black, LinearGradientMode.Vertical);
            graphics.FillRectangle(vertical, bounds);
        }

        protected override void Dispose(bool disposing) { if (disposing) _bitmap?.Dispose(); base.Dispose(disposing); }
    }
}
