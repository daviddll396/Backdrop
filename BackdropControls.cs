using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Backdrop;

internal static class BackdropPalette
{
    public static readonly Color Window = Color.FromArgb(24, 25, 28);
    public static readonly Color Surface = Color.FromArgb(32, 33, 37);
    public static readonly Color Field = Color.FromArgb(43, 44, 49);
    public static readonly Color Border = Color.FromArgb(57, 59, 65);
    public static readonly Color Text = Color.FromArgb(235, 236, 239);
    public static readonly Color Muted = Color.FromArgb(158, 160, 167);
    public static readonly Color Preview = Color.FromArgb(18, 19, 22);
    public static readonly Color Accent = Color.FromArgb(231, 232, 235);
    public static readonly Color AccentText = Color.FromArgb(25, 26, 29);
    public static readonly Color Selection = Color.FromArgb(68, 70, 77);
    public static readonly Color Focus = Color.FromArgb(190, 193, 201);
}

internal static class BackdropControlPaint
{
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void DrawChevron(Graphics graphics, Rectangle bounds, bool up, Color color)
    {
        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        var direction = up ? 1 : -1;
        using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, centerX - 4, centerY + direction, centerX, centerY - direction * 3);
        graphics.DrawLine(pen, centerX, centerY - direction * 3, centerX + 4, centerY + direction);
    }
}

internal sealed class BackdropButton : Button
{
    private readonly bool _primary;
    private bool _hovered;
    private bool _pressed;

    public BackdropButton(string text, bool primary = false)
    {
        _primary = primary;
        Text = text;
        AutoSize = false;
        Height = 32;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AccessibleRole = AccessibleRole.PushButton;
        ForeColor = primary ? BackdropPalette.AccentText : BackdropPalette.Text;
        BackColor = primary ? BackdropPalette.Accent : BackdropPalette.Field;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.25F, primary ? FontStyle.Bold : FontStyle.Regular);
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Window);

        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        using var fill = new SolidBrush(GetFillColor());
        graphics.FillPath(fill, path);

        if (!_primary)
        {
            using var border = new Pen(Enabled ? BackdropPalette.Border : BackdropPalette.Surface);
            graphics.DrawPath(border, path);
        }

        var textColor = Enabled ? (_primary ? BackdropPalette.AccentText : BackdropPalette.Text) : BackdropPalette.Muted;
        var textBounds = Rectangle.Inflate(ClientRectangle, -8, -3);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            using var focusPath = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(bounds, -2, -2), DeviceDpi / 20f);
            using var focusPen = new Pen(BackdropPalette.Focus, 1) { DashStyle = DashStyle.Dot };
            graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnKeyUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    private Color GetFillColor()
    {
        if (!Enabled)
            return BackdropPalette.Surface;
        if (_pressed)
            return _primary ? Color.FromArgb(207, 208, 211) : Color.FromArgb(64, 65, 72);
        if (_hovered)
            return _primary ? Color.White : Color.FromArgb(54, 55, 61);
        return _primary ? BackdropPalette.Accent : BackdropPalette.Field;
    }
}

internal sealed class DarkComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmPrint = 0x0317;
    private const int WmPrintClient = 0x0318;

    public DarkComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 23;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
    }

    protected override void WndProc(ref Message m)
    {
        var message = m.Msg;
        base.WndProc(ref m);
        if (message is not (WmPaint or WmPrint or WmPrintClient) || !IsHandleCreated)
            return;

        if (message == WmPaint)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            PaintClosedField(graphics);
        }
        else if (m.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(m.WParam);
            PaintClosedField(graphics);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    private void PaintClosedField(Graphics graphics)
    {
        if (ClientSize.Width < 4 || ClientSize.Height < 4)
            return;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 18f);
        using var fill = new SolidBrush(Enabled ? BackdropPalette.Field : BackdropPalette.Surface);
        using var border = new Pen(Focused && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        var arrowWidth = Math.Min(Math.Max(DeviceDpi / 4, 24), ClientSize.Width / 3);
        var textBounds = Rectangle.Inflate(ClientRectangle, -8, -2);
        textBounds.Width = Math.Max(0, textBounds.Width - arrowWidth);
        var selectedText = SelectedIndex < 0 ? string.Empty : GetItemText(SelectedItem);
        TextRenderer.DrawText(graphics, selectedText, Font, textBounds,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        var arrowBounds = new Rectangle(ClientSize.Width - arrowWidth, 0, arrowWidth, ClientSize.Height);
        BackdropControlPaint.DrawChevron(graphics, arrowBounds, up: DroppedDown,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted);
    }
}

internal sealed class DarkNumericUpDown : NumericUpDown
{
    private const int GwChild = 5;
    private const int GwHwndNext = 2;
    private const int WmPaint = 0x000F;
    private const int WmPrint = 0x0317;
    private const int WmPrintClient = 0x0318;
    private SpinnerButtonsWindow? _spinnerWindow;

    internal bool HasCustomSpinnerWindow => _spinnerWindow is not null;
    internal IntPtr SpinnerWindowHandle => _spinnerWindow?.Handle ?? IntPtr.Zero;
    internal bool SpinnerHookMatchesCurrentChild =>
        _spinnerWindow is not null && _spinnerWindow.Handle != IntPtr.Zero && _spinnerWindow.Handle == FindSpinnerWindow();
    internal int SpinnerPaintCount => _spinnerWindow?.PaintCount ?? 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AttachSpinnerWindow();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DetachSpinnerWindow();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DetachSpinnerWindow();
        base.Dispose(disposing);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        AttachSpinnerWindow();
    }

    protected override void WndProc(ref Message m)
    {
        var message = m.Msg;
        base.WndProc(ref m);
        // NumericUpDown can create or replace its private spinner child after
        // the managed handle, including when a compact layout changes sizes.
        // The handle comparison in AttachSpinnerWindow keeps this scan cheap.
        if (message == WmPaint)
            AttachSpinnerWindow();
        if (message is not (WmPaint or WmPrint or WmPrintClient) || !IsHandleCreated)
            return;

        if (message == WmPaint)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            PaintSpinner(graphics);
        }
        else if (m.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(m.WParam);
            PaintSpinner(graphics);
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        Invalidate();
        base.OnEnter(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        Invalidate();
        base.OnLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        InvalidateSpinner();
        base.OnEnabledChanged(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        InvalidateSpinner();
        base.OnFontChanged(e);
    }

    private void PaintSpinner(Graphics graphics)
    {
        if (ClientSize.Width < 4 || ClientSize.Height < 4)
            return;

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var buttonWidth = Math.Min(Math.Max(SystemInformation.VerticalScrollBarWidth, DeviceDpi / 5), ClientSize.Width / 3);
        var left = ClientSize.Width - buttonWidth;
        var fillColor = Enabled ? BackdropPalette.Field : BackdropPalette.Surface;
        using (var fill = new SolidBrush(fillColor))
            graphics.FillRectangle(fill, left, 0, buttonWidth, ClientSize.Height);

        using var divider = new Pen(BackdropPalette.Border);
        graphics.DrawLine(divider, left, 2, left, ClientSize.Height - 2);
        var glyphColor = Enabled ? BackdropPalette.Text : BackdropPalette.Muted;
        var top = new Rectangle(left, 0, buttonWidth, ClientSize.Height / 2 + 1);
        var bottom = new Rectangle(left, ClientSize.Height / 2, buttonWidth, ClientSize.Height - ClientSize.Height / 2);
        TextRenderer.DrawText(graphics, "+", Font, top, glyphColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(graphics, "−", Font, bottom, glyphColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        using var border = new Pen(ContainsFocus && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void AttachSpinnerWindow()
    {
        if (!IsHandleCreated || IsDisposed)
            return;

        var candidate = FindSpinnerWindow();
        if (candidate == IntPtr.Zero || _spinnerWindow?.Handle == candidate)
            return;
        DetachSpinnerWindow();
        _spinnerWindow = new SpinnerButtonsWindow(this);
        _spinnerWindow.AssignHandle(candidate);
        _ = InvalidateRect(candidate, IntPtr.Zero, false);
        _ = UpdateWindow(candidate);
    }

    private IntPtr FindSpinnerWindow()
    {
        if (!IsHandleCreated || IsDisposed)
            return IntPtr.Zero;
        if (!GetWindowRect(Handle, out var ownerBounds))
            return IntPtr.Zero;
        var minimumWidth = Math.Max(12, DeviceDpi / 10);
        var maximumWidth = Math.Max(30, DeviceDpi / 3);
        var minimumHeight = Math.Max(12, ClientSize.Height * 2 / 3);
        var candidate = IntPtr.Zero;
        for (var child = GetWindow(Handle, GwChild); child != IntPtr.Zero; child = GetWindow(child, GwHwndNext))
        {
            if (GetParent(child) != Handle || !GetWindowRect(child, out var childBounds))
                continue;

            var width = childBounds.Right - childBounds.Left;
            var height = childBounds.Bottom - childBounds.Top;
            if (childBounds.Right >= ownerBounds.Right - 2 &&
                width >= minimumWidth && width <= maximumWidth && height >= minimumHeight)
            {
                candidate = child;
                break;
            }
        }
        return candidate;
    }

    private void DetachSpinnerWindow()
    {
        if (_spinnerWindow is null)
            return;
        _spinnerWindow.ReleaseHandle();
        _spinnerWindow = null;
    }

    private void InvalidateSpinner()
    {
        if (_spinnerWindow?.Handle is not { } handle || handle == IntPtr.Zero)
            return;
        _ = InvalidateRect(handle, IntPtr.Zero, false);
        _ = UpdateWindow(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackMouseEventData
    {
        public uint Size;
        public uint Flags;
        public IntPtr Window;
        public uint HoverTime;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr window, IntPtr rect, bool erase);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool TrackMouseEvent(ref TrackMouseEventData eventTrack);

    private sealed class SpinnerButtonsWindow : NativeWindow
    {
        private const int WmPaint = 0x000F;
        private const int WmPrint = 0x0317;
        private const int WmPrintClient = 0x0318;
        private const int WmMouseMove = 0x0200;
        private const int WmLeftButtonDown = 0x0201;
        private const int WmLeftButtonUp = 0x0202;
        private const int WmMouseLeave = 0x02A3;
        private const uint TrackLeave = 0x00000002;
        private const uint RedrawNow = 0x00000100;
        private const uint Invalidate = 0x00000001;
        private readonly DarkNumericUpDown _owner;
        private bool _trackingMouse;
        private bool _hovered;
        private bool _pressed;

        public int PaintCount { get; private set; }

        public SpinnerButtonsWindow(DarkNumericUpDown owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            var message = m.Msg;
            if (message == WmMouseMove && !_trackingMouse)
            {
                _trackingMouse = true;
                var track = new TrackMouseEventData { Size = (uint)Marshal.SizeOf<TrackMouseEventData>(), Flags = TrackLeave, Window = Handle };
                _ = TrackMouseEvent(ref track);
                _hovered = true;
                Redraw();
            }
            else if (message == WmMouseLeave)
            {
                _trackingMouse = false;
                _hovered = false;
                _pressed = false;
                Redraw();
            }
            else if (message == WmLeftButtonDown)
            {
                _pressed = true;
                Redraw();
            }
            else if (message == WmLeftButtonUp)
            {
                _pressed = false;
                Redraw();
            }

            base.WndProc(ref m);
            if (message is WmPaint or WmPrint or WmPrintClient)
            {
                if (message == WmPaint)
                {
                    PaintCount++;
                    using var graphics = Graphics.FromHwnd(Handle);
                    PaintButtons(graphics);
                }
                else if (m.WParam != IntPtr.Zero)
                {
                    using var graphics = Graphics.FromHdc(m.WParam);
                    PaintButtons(graphics);
                }
            }
        }

        private void PaintButtons(Graphics graphics)
        {
            if (Handle == IntPtr.Zero)
                return;
            if (!GetClientRect(Handle, out var client))
                return;
            var bounds = new Rectangle(0, 0, Math.Max(0, client.Right - client.Left), Math.Max(0, client.Bottom - client.Top));
            if (bounds.Width < 4 || bounds.Height < 4)
                return;

            var surface = !_owner.Enabled
                ? BackdropPalette.Surface
                : _pressed
                    ? BackdropPalette.Selection
                    : _hovered
                        ? Color.FromArgb(54, 55, 61)
                        : BackdropPalette.Field;
            using var fill = new SolidBrush(surface);
            graphics.FillRectangle(fill, bounds);
            var midpoint = bounds.Height / 2;
            using var separator = new Pen(BackdropPalette.Border);
            graphics.DrawLine(separator, 2, midpoint, bounds.Width - 3, midpoint);
            using var outline = new Pen(_owner.ContainsFocus ? BackdropPalette.Focus : BackdropPalette.Border);
            graphics.DrawRectangle(outline, 0, 0, bounds.Width - 1, bounds.Height - 1);

            var glyph = _owner.Enabled ? BackdropPalette.Text : BackdropPalette.Muted;
            var font = _owner.Font;
            TextRenderer.DrawText(graphics, "+", font, new Rectangle(0, 0, bounds.Width, midpoint), glyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(graphics, "−", font, new Rectangle(0, midpoint, bounds.Width, bounds.Height - midpoint), glyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        private void Redraw()
        {
            if (Handle != IntPtr.Zero)
            {
                _ = InvalidateRect(Handle, IntPtr.Zero, false);
                _ = UpdateWindow(Handle);
            }
        }
    }
}

internal sealed class SegmentedChoiceControl : UserControl
{
    private readonly IReadOnlyList<string> _items;
    private readonly SegmentedRadioButton[] _options;

    public event EventHandler? SelectedIndexChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Items => _items;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => Array.FindIndex(_options, option => option.Checked);
        set
        {
            if (value < 0 || value >= _options.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_options[value].Checked)
                return;
            _options[value].Checked = true;
        }
    }

    public SegmentedChoiceControl(params string[] items)
    {
        if (items.Length < 2 || items.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A choice group needs at least two named options.", nameof(items));

        _items = Array.AsReadOnly(items.ToArray());
        _options = new SegmentedRadioButton[_items.Count];
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = false;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
        Margin = Padding.Empty;

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = _items.Count,
            RowCount = 1,
            Padding = new Padding(2),
            Margin = Padding.Empty,
            BackColor = BackdropPalette.Field
        };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var index = 0; index < _items.Count; index++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _items.Count));
            var option = new SegmentedRadioButton(_items[index])
            {
                Dock = DockStyle.Fill,
                TabIndex = index,
                TabStop = index == 0,
                Margin = new Padding(index == 0 ? 0 : 1, 0, index == _items.Count - 1 ? 0 : 1, 0)
            };
            option.CheckedChanged += (_, _) =>
            {
                if (option.Checked)
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };
            _options[index] = option;
            row.Controls.Add(option, index, 0);
        }
        Controls.Add(row);
        _options[0].Checked = true;
    }
}

internal sealed class SegmentedRadioButton : RadioButton
{
    private const float CornerRadius = 5;
    private bool _hovered;

    public SegmentedRadioButton(string text)
    {
        Text = text;
        Font = new Font("Segoe UI", 8.5F);
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        AccessibleRole = AccessibleRole.RadioButton;
        AccessibleName = text;
        ForeColor = BackdropPalette.Text;
        BackColor = BackdropPalette.Field;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Field);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, CornerRadius * DeviceDpi / 96f);
        var fillColor = !Enabled
            ? BackdropPalette.Surface
            : Checked
                ? BackdropPalette.Selection
                : _hovered
                    ? Color.FromArgb(54, 55, 61)
                    : BackdropPalette.Field;
        using var fill = new SolidBrush(fillColor);
        graphics.FillPath(fill, path);
        using var border = new Pen(Checked || Focused ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.DrawPath(border, path);
        TextRenderer.DrawText(graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -1, -2),
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }
}
