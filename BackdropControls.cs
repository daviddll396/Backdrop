using System.ComponentModel;
using System.Drawing.Drawing2D;

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

    public static void DrawHorizontalChevron(Graphics graphics, Rectangle bounds, bool right, Color color)
    {
        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        var scale = Math.Max(1f, graphics.DpiX / 96f);
        var span = Math.Min(9f * scale, Math.Min(bounds.Width - 2f * scale, bounds.Height - 2f * scale));
        var direction = right ? 1 : -1;
        using var pen = new Pen(color, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, centerX - direction * span / 2, centerY - span / 2,
            centerX + direction * span / 2, centerY);
        graphics.DrawLine(pen, centerX + direction * span / 2, centerY,
            centerX - direction * span / 2, centerY + span / 2);
    }
}

internal sealed class SmallBusySpinner : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 75 };
    private int _angle;

    public SmallBusySpinner()
    {
        Size = new Size(16, 16);
        AccessibleName = "Preview updating";
        AccessibleDescription = "The composition preview is being updated.";
        AccessibleRole = AccessibleRole.ProgressBar;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 30) % 360;
            Invalidate();
        };
    }

    public void StartSpinning()
    {
        Visible = true;
        if (!_timer.Enabled)
            _timer.Start();
    }

    public void StopSpinning()
    {
        _timer.Stop();
        Visible = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(BackdropPalette.Muted, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawArc(pen, RectangleF.Inflate(ClientRectangle, -3, -3), _angle, 245);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
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
        var compactText = Text.Length == 1;
        var textBounds = Rectangle.Inflate(ClientRectangle, compactText ? -3 : -8, -3);
        var textFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (compactText)
            textFlags |= TextFormatFlags.NoPadding;
        if (Text is "‹" or "›")
            BackdropControlPaint.DrawHorizontalChevron(graphics, textBounds, right: Text == "›", textColor);
        else
            TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor, textFlags);

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

internal sealed class BackdropChevronButton : Button
{
    private readonly bool _right;
    private bool _hovered;
    private bool _pressed;

    public BackdropChevronButton(bool right)
    {
        _right = right;
        Text = right ? "›" : "‹";
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        FlatAppearance.BorderSize = 0;
        AccessibleRole = AccessibleRole.PushButton;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal bool PointsRight => _right;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        var fillColor = !Enabled ? BackdropPalette.Surface : _pressed ? BackdropPalette.Selection : _hovered ? Color.FromArgb(54, 55, 61) : BackdropPalette.Field;
        using var fill = new SolidBrush(fillColor);
        graphics.FillPath(fill, path);
        BackdropControlPaint.DrawHorizontalChevron(graphics, Rectangle.Round(bounds), _right,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted);

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
}

internal sealed class BackdropDropdown : UserControl
{
    private readonly ToolStripDropDown _popup = new()
    {
        AutoClose = true,
        AutoSize = false,
        BackColor = BackdropPalette.Border,
        Padding = Padding.Empty,
        DropShadowEnabled = true
    };
    private readonly ToolStripControlHost _popupHost;
    private readonly BackdropDropdownList _popupList;
    private int _selectedIndex = -1;
    private bool _popupIsOpen;
    private bool _committingSelection;
    private bool _restoreFocusAfterClose;
    private bool _popupWasOpenAtMouseDown;

    public BackdropDropdown()
    {
        _popupList = new BackdropDropdownList(this);
        Items = [];
        MinimumSize = new Size(0, 38);
        AccessibleRole = AccessibleRole.ComboBox;
        AccessibleDescription = "Choose an option. Press Enter, Space, or Alt+Down to open the list. Use the arrow keys, then Enter to select.";
        TabStop = true;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _popupList.AccessibleName = "Dropdown options";
        _popupHost = new ToolStripControlHost(_popupList)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _popup.Items.Add(_popupHost);
        _popup.Closed += (_, _) =>
        {
            var restoreFocus = _restoreFocusAfterClose;
            _restoreFocusAfterClose = false;
            _popupIsOpen = false;
            if (!_committingSelection)
                _popupList.SelectedIndex = _selectedIndex;
            Invalidate();
            if (restoreFocus && !Disposing && !IsDisposed && CanFocus)
                Focus();
        };
    }

    internal List<string> Items { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index must refer to an item or be -1.");
            if (_selectedIndex == value)
                return;
            _selectedIndex = value;
            _popupList.CommittedIndex = value;
            _popupList.Invalidate();
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal string? SelectedItem => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool DroppedDown
    {
        get => _popupIsOpen;
        set
        {
            if (value)
                OpenPopup();
            else
                ClosePopup();
        }
    }
    internal Control PopupList => _popupList;
    internal int PopupCheckmarkCount => SelectedIndex >= 0 ? 1 : 0;
    internal event EventHandler? SelectedIndexChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width < 4 || ClientSize.Height < 4)
            return;

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 18f);
        using var fill = new SolidBrush(Enabled ? BackdropPalette.Field : BackdropPalette.Surface);
        using var border = new Pen((ContainsFocus || _popupIsOpen) && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        var arrowWidth = Math.Min(Math.Max(DeviceDpi / 4, 24), ClientSize.Width / 3);
        var textBounds = Rectangle.Inflate(ClientRectangle, -8, -2);
        textBounds.Width = Math.Max(0, textBounds.Width - arrowWidth);
        var selectedText = SelectedItem ?? string.Empty;
        TextRenderer.DrawText(graphics, selectedText, Font, textBounds,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        var arrowBounds = new Rectangle(ClientSize.Width - arrowWidth, 0, arrowWidth, ClientSize.Height);
        BackdropControlPaint.DrawChevron(graphics, arrowBounds, up: _popupIsOpen,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        _popupWasOpenAtMouseDown = _popupIsOpen;
        Focus();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location))
            return;
        if (_popupWasOpenAtMouseDown)
            ClosePopup();
        else
            OpenPopup();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_popupList is not null)
        {
            _popupList.Font = Font;
            Invalidate();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        if (_popupList is not null)
            _popupList.ItemHeight = Math.Max(32, (int)Math.Round(36 * DeviceDpi / 96f));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleKeyCommand(keyData))
            return true;

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (HandleKeyCommand(keyData))
            return true;

        return base.ProcessDialogKey(keyData);
    }

    internal bool HandleKeyCommand(Keys keyData)
    {
        if (Items.Count == 0)
            return false;

        if (_popupIsOpen)
        {
            if (keyData is Keys.Enter)
            {
                CommitPopupSelection();
                return true;
            }
            if (keyData is Keys.Escape)
            {
                ClosePopup(restoreFocus: true);
                return true;
            }
            if (keyData is Keys.Down or Keys.Up or Keys.Home or Keys.End)
            {
                _popupList.SelectedIndex = keyData switch
                {
                    Keys.Down => Math.Min(Items.Count - 1, Math.Max(0, _popupList.SelectedIndex + 1)),
                    Keys.Up => Math.Max(0, _popupList.SelectedIndex < 0 ? 0 : _popupList.SelectedIndex - 1),
                    Keys.Home => 0,
                    _ => Items.Count - 1
                };
                return true;
            }
            return false;
        }

        if (!_popupIsOpen)
        {
            if (keyData is Keys.Enter or Keys.Space or Keys.F4 || keyData == (Keys.Alt | Keys.Down))
            {
                OpenPopup();
                return true;
            }

            if (keyData is Keys.Down or Keys.Up or Keys.Home or Keys.End)
            {
                SelectedIndex = keyData switch
                {
                    Keys.Down => Math.Min(Items.Count - 1, Math.Max(0, SelectedIndex + 1)),
                    Keys.Up => Math.Max(0, SelectedIndex < 0 ? 0 : SelectedIndex - 1),
                    Keys.Home => 0,
                    _ => Items.Count - 1
                };
                return true;
            }
        }
        return false;
    }

    private void OpenPopup()
    {
        if (_popupIsOpen || !Enabled || Items.Count == 0)
            return;

        _popupList.BeginUpdate();
        _popupList.Items.Clear();
        _popupList.Items.AddRange(Items.ToArray());
        _popupList.CommittedIndex = SelectedIndex;
        _popupList.AccessibleName = string.IsNullOrWhiteSpace(AccessibleName)
            ? "Dropdown options"
            : $"{AccessibleName} options";
        _popupList.SelectedIndex = SelectedIndex;
        _popupList.EndUpdate();
        _popupList.Font = Font;
        _popupList.ItemHeight = Math.Max(32, (int)Math.Round(36 * DeviceDpi / 96f));
        _popupList.Width = Math.Max(100, Width - 2);
        _popupList.Height = _popupList.ItemHeight * Math.Min(Items.Count, 8);
        _popupHost.Size = _popupList.Size;
        _popup.Size = new Size(_popupList.Width + 2, _popupList.Height + 2);
        _popupIsOpen = true;
        _committingSelection = false;
        _restoreFocusAfterClose = false;

        var workArea = Screen.FromControl(this).WorkingArea;
        var location = PointToScreen(new Point(0, Height));
        if (location.Y + _popup.Height > workArea.Bottom)
            location.Y = Math.Max(workArea.Top, PointToScreen(Point.Empty).Y - _popup.Height);
        if (location.X + _popup.Width > workArea.Right)
            location.X = Math.Max(workArea.Left, workArea.Right - _popup.Width);
        _popup.Show(location);
        _popupList.Focus();
        _popupList.Invalidate();
        Invalidate();
    }

    private void CommitPopupSelection()
    {
        if (_popupList.SelectedIndex < 0)
            return;
        _committingSelection = true;
        SelectedIndex = _popupList.SelectedIndex;
        ClosePopup(restoreFocus: true);
    }

    private void ClosePopup(bool restoreFocus = false)
    {
        _restoreFocusAfterClose |= restoreFocus;
        if (_popup.Visible)
            _popup.Close();
        else
        {
            _popupIsOpen = false;
            Invalidate();
        }
    }

    private sealed class BackdropDropdownList : ListBox
    {
        private readonly BackdropDropdown _owner;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int CommittedIndex { get; set; } = -1;
        internal BackdropDropdownList(BackdropDropdown owner)
        {
            _owner = owner;
            AccessibleRole = AccessibleRole.List;
            SelectionMode = SelectionMode.One;
            DrawMode = DrawMode.OwnerDrawFixed;
            IntegralHeight = false;
            BorderStyle = BorderStyle.None;
            BackColor = BackdropPalette.Field;
            ForeColor = BackdropPalette.Text;
            ItemHeight = 36;
            TabStop = true;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? BackdropPalette.Selection : BackdropPalette.Field);
            e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index == CommittedIndex)
            {
                var x = e.Bounds.Left + 14;
                var y = e.Bounds.Top + e.Bounds.Height / 2;
                using var check = new Pen(BackdropPalette.Text, Math.Max(1.6f, DeviceDpi / 60f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                e.Graphics.DrawLine(check, x, y, x + 4, y + 4);
                e.Graphics.DrawLine(check, x + 4, y + 4, x + 11, y - 5);
            }
            var textBounds = Rectangle.Inflate(e.Bounds, -34, 0);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, textBounds,
                BackdropPalette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0)
            {
                using var focus = new Pen(BackdropPalette.Focus);
                e.Graphics.DrawRectangle(focus, e.Bounds.Left, e.Bounds.Top, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                var index = IndexFromPoint(e.Location);
                if (index >= 0 && index < Items.Count)
                {
                    SelectedIndex = index;
                    _owner.CommitPopupSelection();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_owner.HandleKeyCommand(e.KeyCode))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else
                base.OnKeyDown(e);
        }
    }
}

internal sealed class BackdropNumberSelector : UserControl
{
    private readonly BackdropChevronButton _decreaseButton;
    private readonly TextBox _valueEditor;
    private readonly BackdropChevronButton _increaseButton;
    private decimal _minimum;
    private decimal _maximum = 100;
    private decimal _increment = 1;
    private decimal _value;
    private bool _thousandsSeparator;

    public BackdropNumberSelector()
    {
        MinimumSize = new Size(0, 38);
        AccessibleRole = AccessibleRole.SpinButton;
        TabStop = false;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

        _decreaseButton = new BackdropChevronButton(right: false) { AccessibleName = "Decrease value", TabIndex = 0, Margin = Padding.Empty };
        _valueEditor = new TextBox
        {
            BorderStyle = BorderStyle.None,
            TextAlign = HorizontalAlignment.Center,
            AccessibleName = "Numeric value",
            TabIndex = 1,
            BackColor = BackdropPalette.Field,
            ForeColor = BackdropPalette.Text
        };
        _increaseButton = new BackdropChevronButton(right: true) { AccessibleName = "Increase value", TabIndex = 2, Margin = Padding.Empty };
        foreach (Control focusable in new Control[] { _decreaseButton, _valueEditor, _increaseButton })
        {
            focusable.GotFocus += (_, _) => Invalidate();
            focusable.LostFocus += (_, _) => Invalidate();
        }
        Controls.Add(_decreaseButton);
        Controls.Add(_valueEditor);
        Controls.Add(_increaseButton);

        _decreaseButton.Click += (_, _) => Step(-1);
        _increaseButton.Click += (_, _) => Step(1);
        _valueEditor.Leave += (_, _) => CommitEdit();
        _valueEditor.KeyDown += OnEditorKeyDown;
        UpdateAccessibleNames();
        UpdateText();
        LayoutChildren();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
                return;
            _minimum = value;
            if (_maximum < value)
                _maximum = value;
            if (_value < value)
                Value = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value)
                return;
            _maximum = value;
            if (_minimum > value)
                _minimum = value;
            if (_value > value)
                Value = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Increment
    {
        get => _increment;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The increment must be greater than zero.");
            _increment = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Value
    {
        get => _value;
        set
        {
            if (value < _minimum || value > _maximum)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The value must be between {_minimum} and {_maximum}.");
            if (_value == value)
                return;
            _value = value;
            UpdateText();
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            if (_thousandsSeparator == value)
                return;
            _thousandsSeparator = value;
            UpdateText();
        }
    }

    internal event EventHandler? ValueChanged;

    internal void RefreshAccessibility() => UpdateAccessibleNames();

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (_valueEditor is not null)
        {
            _valueEditor.ForeColor = Enabled ? BackdropPalette.Text : BackdropPalette.Muted;
            Invalidate();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_valueEditor is null)
            return;
        _valueEditor.Font = Font;
        _decreaseButton.Font = Font;
        _increaseButton.Font = Font;
        LayoutChildren();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutChildren();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutChildren();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        using var fill = new SolidBrush(BackdropPalette.Field);
        using var border = new Pen(ContainsFocus && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!HandleEditorKey(e.KeyCode))
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    internal bool HandleEditorKey(Keys key)
    {
        if (key is not (Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Escape))
            return false;

        if (key == Keys.Escape)
            UpdateText();
        else
        {
            CommitEdit();
            if (key == Keys.Up)
                Step(1);
            else if (key == Keys.Down)
                Step(-1);
            else if (key == Keys.Home)
                Value = Minimum;
            else if (key == Keys.End)
                Value = Maximum;
        }
        return true;
    }

    private void Step(int direction)
    {
        CommitEdit();
        try
        {
            Value = Math.Clamp(_value + _increment * direction, _minimum, _maximum);
        }
        catch (OverflowException)
        {
            Value = direction > 0 ? _maximum : _minimum;
        }
    }

    private void CommitEdit()
    {
        if (decimal.TryParse(_valueEditor.Text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var value) &&
            decimal.Truncate(value) == value &&
            value >= _minimum && value <= _maximum)
            Value = value;
        else
            UpdateText();
    }

    private void UpdateText() =>
        _valueEditor.Text = _value.ToString(
            _thousandsSeparator ? "#,##0.############################" : "0.############################",
            System.Globalization.CultureInfo.CurrentCulture);

    private void UpdateAccessibleNames()
    {
        var name = string.IsNullOrWhiteSpace(AccessibleName) ? "value" : AccessibleName;
        _decreaseButton.AccessibleName = $"Decrease {name}";
        _increaseButton.AccessibleName = $"Increase {name}";
        _decreaseButton.AccessibleDescription = $"Decrease by {_increment}.";
        _increaseButton.AccessibleDescription = $"Increase by {_increment}.";
        _valueEditor.AccessibleName = name;
        _valueEditor.AccessibleDescription = string.IsNullOrWhiteSpace(AccessibleDescription)
            ? "Enter a number. Use Up and Down Arrow to change the value. Home selects the minimum; End selects the maximum."
            : $"{AccessibleDescription} Use Up and Down Arrow to change the value. Home selects the minimum; End selects the maximum.";
    }

    private void LayoutChildren()
    {
        if (_valueEditor is null)
            return;

        var inset = Math.Max(2, DeviceDpi / 48);
        var buttonWidth = Math.Min(Math.Max(DeviceDpi / 3, 24), Math.Max(0, ClientSize.Width / 3 - inset));
        var buttonHeight = Math.Max(0, ClientSize.Height - inset * 2);
        _decreaseButton.Bounds = new Rectangle(inset, inset, buttonWidth, buttonHeight);
        _increaseButton.Bounds = new Rectangle(ClientSize.Width - inset - buttonWidth, inset, buttonWidth, buttonHeight);

        var editorLeft = _decreaseButton.Right + inset;
        var editorRight = _increaseButton.Left - inset;
        var editorHeight = Math.Min(_valueEditor.PreferredHeight, buttonHeight);
        var editorTop = (ClientSize.Height - editorHeight) / 2;
        _valueEditor.Bounds = new Rectangle(editorLeft, editorTop, Math.Max(0, editorRight - editorLeft), editorHeight);
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
        MinimumSize = new Size(0, 38);
        Height = 38;
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
