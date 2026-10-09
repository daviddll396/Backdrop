using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Backdrop;

internal sealed class MainForm : Form
{
    private const string ShellPackageName = "Backdrop.ShellPrototype";
    private static readonly Color WindowColor = BackdropPalette.Window;
    private static readonly Color SurfaceColor = BackdropPalette.Surface;
    private static readonly Color FieldColor = BackdropPalette.Field;
    private static readonly Color BorderColor = BackdropPalette.Border;
    private static readonly Color TextColor = BackdropPalette.Text;
    private static readonly Color MutedColor = BackdropPalette.Muted;
    private static readonly Color PreviewColor = Color.FromArgb(34, 35, 40);
    private static readonly Color AccentColor = BackdropPalette.Accent;
    private static readonly Color AccentTextColor = BackdropPalette.AccentText;

    private readonly DarkNumericUpDown _padding = new() { Minimum = 0, Maximum = 25, Increment = 1 };
    private readonly DarkNumericUpDown _shadow = new() { Minimum = 0, Maximum = 40, Increment = 2 };
    private readonly DarkNumericUpDown _outputWidth = new() { Minimum = 640, Maximum = 4096, Increment = 160, ThousandsSeparator = true };
    private readonly ToolTip _settingTooltips = new();
    private readonly DarkComboBox _ratio = new();
    private readonly SegmentedChoiceControl _layout = new("Auto", "Row", "Grid");
    private readonly ListBox _imageList = new() { IntegralHeight = false, SelectionMode = SelectionMode.One, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22 };
    private readonly PictureBox _preview = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = PreviewColor, Visible = false };
    private readonly Panel _previewFrame = new() { BackColor = Color.FromArgb(42, 43, 48), Padding = new Padding(14) };
    private readonly Label _previewPlaceholder = new() { Text = "Preparing preview…", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = MutedColor, BackColor = PreviewColor };
    private readonly Label _previewCaption = new() { AutoSize = true, ForeColor = MutedColor, Text = "A sample composition will appear here." };
    private readonly Label _previewImageCount = new() { AutoSize = false, ForeColor = MutedColor, Text = "SAMPLE" };
    private readonly Button _backgroundButton = MakeButton("Background");
    private readonly Label _backgroundModeLabel = new() { AutoSize = true, ForeColor = MutedColor, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _selectedCount = new() { AutoSize = true, ForeColor = MutedColor };
    private readonly Label _status = new() { AutoSize = true, ForeColor = MutedColor, Text = "Add images to begin. Preview updates as you edit." };
    private readonly Button _moveUp = MakeButton("Up");
    private readonly Button _moveDown = MakeButton("Down");
    private readonly Button _removeSelectedButton = MakeButton("Remove");
    private readonly Button _clearImagesButton = MakeButton("Clear");
    private readonly Button _generateButton = MakeButton("Create PNG", primary: true);
    private readonly Button _shortcutButton = MakeButton("Enable right-click shortcut");
    private readonly Button _openImageButton = MakeButton("Open image");
    private readonly Button _openFolderButton = MakeButton("Show folder");
    private readonly FlowLayoutPanel _outputActions = new() { Dock = DockStyle.Fill, WrapContents = false, Visible = false, Margin = Padding.Empty };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 200 };
    private readonly SemaphoreSlim _previewGate = new(1, 1);
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _runningPreviewCancellation;
    private CancellationTokenSource? _exportCancellation;
    private string[] _imagePaths = [];
    private string? _generatedOutput;
    private int _previewRequest;
    private bool _isGenerating;
    private bool _isShellMenuChanging;
    private bool _shellMenuScriptsAvailable;
    private bool _classicShellHelperAvailable;
    private BackgroundMode _backgroundMode;
    private string _backgroundColor1Hex = "#303137";
    private string _backgroundColor2Hex = "#4B4C53";
    private BackgroundPattern _backgroundPattern;

    public MainForm(AppSettings settings, IReadOnlyList<string>? initialPaths = null, string? previewFile = null)
    {
        var initialSettings = settings.IsValid() ? settings.Copy() : new AppSettings();
        Text = "Backdrop";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(800, 600);
        ClientSize = new Size(960, 720);
        Font = new Font("Segoe UI", 9.5F);
        BackColor = WindowColor;
        ForeColor = TextColor;
        AccessibleName = "Backdrop image composer";
        AllowDrop = true;

        _padding.Value = initialSettings.PaddingPercent;
        _shadow.Value = (decimal)Math.Round(initialSettings.ShadowStrength * 100);
        _outputWidth.Value = initialSettings.OutputWidth;
        _backgroundMode = initialSettings.BackgroundMode;
        _backgroundColor1Hex = initialSettings.BackgroundColor1Hex;
        _backgroundColor2Hex = initialSettings.BackgroundColor2Hex;
        _backgroundPattern = initialSettings.BackgroundPattern;
        _ratio.Items.AddRange(["Auto", "Wide 16:9", "Square 1:1", "Portrait 4:5"]);
        _ratio.SelectedIndex = initialSettings.CanvasRatio switch
        {
            CanvasRatio.Auto => 0,
            CanvasRatio.Square1x1 => 2,
            CanvasRatio.Portrait4x5 => 3,
            _ => 1
        };
        _layout.SelectedIndex = (int)initialSettings.Layout;

        BuildLayout();
        _imagePaths = initialPaths?.ToArray() ?? [];
        UpdateImageList();
        if (_imagePaths.Length > 0)
            _status.Text = $"{_imagePaths.Length} images ready.";
        _ = RefreshExplorerMenuStatusAsync();
        WirePreviewEvents();
        _previewTimer.Tick += PreviewTimer_Tick;

        if (!string.IsNullOrWhiteSpace(previewFile) && File.Exists(previewFile))
        {
            ReplacePreview(LoadPreviewFile(previewFile));
            _previewCaption.Text = _imagePaths.Length == 0
                ? "Sample only · add images to preview your composition."
                : "Generated composition preview.";
        }
        else
        {
            QueuePreview();
        }

        SetDropTarget(this);
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Delete && _imageList.ContainsFocus && _imageList.SelectedIndex >= 0)
        {
            RemoveSelectedImage();
            return true;
        }

        if ((keyData & Keys.Modifiers) == Keys.Alt)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Up)
            {
                MoveSelectedImage(-1);
                return true;
            }
            if (key == Keys.Down)
            {
                MoveSelectedImage(1);
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isGenerating || _isShellMenuChanging)
        {
            e.Cancel = true;
            _status.Text = _isGenerating
                ? "PNG creation is still running. Please wait for it to finish."
                : "Windows menu setup is still running. Please wait for it to finish.";
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _previewTimer.Stop();
        _previewTimer.Dispose();
        _settingTooltips.Dispose();
        _previewCancellation?.Cancel();
        if (!ReferenceEquals(_previewCancellation, _runningPreviewCancellation))
            _previewCancellation?.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = WindowColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = WindowColor, Margin = new Padding(0, 10, 0, 10) };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        workspace.Controls.Add(BuildPreviewCard(), 0, 0);
        workspace.Controls.Add(BuildSidebar(), 1, 0);
        root.Controls.Add(workspace, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = WindowColor, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = WindowColor };
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.Controls.Add(new Label { Text = "Backdrop", AutoSize = true, ForeColor = TextColor, Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Margin = Padding.Empty }, 0, 0);
        brand.Controls.Add(new Label { Text = "A clean composition from your images.", AutoSize = true, ForeColor = MutedColor, Margin = Padding.Empty }, 0, 1);
        header.Controls.Add(brand, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = WindowColor,
            Padding = new Padding(0, 7, 0, 0),
            Margin = Padding.Empty
        };
        var saveButton = MakeButton("Save preferences");
        saveButton.Width = 138;
        saveButton.Height = 34;
        saveButton.Click += (_, _) => SavePreferences();
        _shortcutButton.AutoSize = false;
        _shortcutButton.Width = 204;
        _shortcutButton.Height = 34;
        _shortcutButton.Click += (_, _) => ToggleExplorerMenu();
        actions.Controls.Add(saveButton);
        actions.Controls.Add(_shortcutButton);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private Control BuildPreviewCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(0, 0, 14, 0);
        var body = CreateCardInterior();
        card.Controls.Add(body);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = SurfaceColor, Padding = new Padding(15) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = SurfaceColor, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(SectionLabel("PREVIEW"), 0, 0);
        var backgroundActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        _backgroundButton.Width = 104;
        _backgroundButton.Height = 32;
        _backgroundButton.AccessibleName = "Edit composition background";
        _backgroundButton.AccessibleDescription = "Choose automatic, solid, gradient, or patterned background colors.";
        _backgroundButton.Click += (_, _) => EditBackground();
        _settingTooltips.SetToolTip(_backgroundButton, "Choose and preview a background. Save preferences to keep changes.");
        _backgroundModeLabel.Margin = new Padding(7, 0, 0, 0);
        _backgroundModeLabel.AutoSize = false;
        _backgroundModeLabel.Width = 68;
        _backgroundModeLabel.Dock = DockStyle.Fill;
        backgroundActions.Controls.Add(_backgroundButton);
        backgroundActions.Controls.Add(_backgroundModeLabel);
        heading.Controls.Add(backgroundActions, 1, 0);
        _previewImageCount.TextAlign = ContentAlignment.MiddleRight;
        _previewImageCount.Dock = DockStyle.Fill;
        heading.Controls.Add(_previewImageCount, 2, 0);
        UpdateBackgroundModeLabel();
        layout.Controls.Add(heading, 0, 0);

        _preview.Dock = DockStyle.Fill;
        _preview.AccessibleName = "Composition preview";
        _previewFrame.Dock = DockStyle.Fill;
        _previewFrame.AccessibleName = "Preview image area";
        _previewFrame.Controls.Add(_preview);
        _previewFrame.Controls.Add(_previewPlaceholder);
        layout.Controls.Add(_previewFrame, 0, 1);
        layout.Controls.Add(_previewCaption, 0, 2);
        body.Controls.Add(layout);
        SetDropTarget(_previewFrame);
        SetDropTarget(_preview);
        SetDropTarget(_previewPlaceholder);
        return card;
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = WindowColor };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        sidebar.Controls.Add(BuildSettingsCard(), 0, 0);
        var images = BuildImagesCard();
        images.Margin = new Padding(0, 12, 0, 0);
        sidebar.Controls.Add(images, 0, 1);
        return sidebar;
    }

    private Control BuildSettingsCard()
    {
        var card = CreateCard();
        var body = CreateCardInterior();
        card.Controls.Add(body);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = SurfaceColor, Padding = new Padding(10) };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(SectionLabel("COMPOSITION"), 0, 0);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = SurfaceColor, Margin = Padding.Empty };
        for (var column = 0; column < 3; column++)
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        for (var row = 0; row < 3; row++)
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        var ratioField = MakeSettingField("Canvas ratio", _ratio);
        fields.Controls.Add(ratioField, 0, 0);
        fields.SetColumnSpan(ratioField, 3);
        var layoutField = MakeSettingField("Layout", _layout);
        fields.Controls.Add(layoutField, 0, 1);
        fields.SetColumnSpan(layoutField, 3);
        _ratio.DrawItem += DrawComboItem;
        _outputWidth.AccessibleName = "Canvas long edge in pixels";
        _outputWidth.AccessibleDescription = "Output canvas long edge in pixels, from 640 to 4096.";
        _padding.AccessibleName = "Padding percentage";
        _padding.AccessibleDescription = "Space around each picture, as a percentage of the canvas.";
        _shadow.AccessibleName = "Shadow strength";
        _shadow.AccessibleDescription = "Picture shadow strength, from 0 to 40 percent.";
        _settingTooltips.SetToolTip(_outputWidth, "Canvas long edge in pixels (640–4096)." );
        _settingTooltips.SetToolTip(_padding, "Padding around each picture, as a percent of the canvas.");
        _settingTooltips.SetToolTip(_shadow, "Shadow strength, as a percent.");
        fields.Controls.Add(MakeSettingField("Size", _outputWidth), 0, 2);
        fields.Controls.Add(MakeSettingField("Padding", _padding), 1, 2);
        fields.Controls.Add(MakeSettingField("Shadow", _shadow), 2, 2);
        content.Controls.Add(fields, 0, 1);
        body.Controls.Add(content);
        return card;
    }

    private Control BuildImagesCard()
    {
        var card = CreateCard();
        var body = CreateCardInterior();
        card.Controls.Add(body);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = SurfaceColor, Padding = new Padding(10) };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = SurfaceColor };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        heading.Controls.Add(SectionLabel("IMAGES"), 0, 0);
        _selectedCount.Dock = DockStyle.Fill;
        _selectedCount.TextAlign = ContentAlignment.MiddleRight;
        heading.Controls.Add(_selectedCount, 1, 0);
        content.Controls.Add(heading, 0, 0);

        var listActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = SurfaceColor, Margin = Padding.Empty };
        listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        listActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var addButton = MakeButton("Add images");
        addButton.Dock = DockStyle.Fill;
        addButton.Margin = new Padding(0, 0, 4, 0);
        addButton.AccessibleName = "Add up to nine images";
        addButton.Click += (_, _) => ChooseImages();
        listActions.Controls.Add(addButton, 0, 0);
        _clearImagesButton.Dock = DockStyle.Fill;
        _clearImagesButton.Margin = new Padding(4, 0, 0, 0);
        _clearImagesButton.AccessibleName = "Clear all images";
        _clearImagesButton.Click += (_, _) => ClearImages();
        _settingTooltips.SetToolTip(_clearImagesButton, "Remove all images from the list.");
        listActions.Controls.Add(_clearImagesButton, 1, 0);
        content.Controls.Add(listActions, 0, 1);

        _imageList.Dock = DockStyle.Fill;
        _imageList.BackColor = FieldColor;
        _imageList.ForeColor = TextColor;
        _imageList.BorderStyle = BorderStyle.FixedSingle;
        _imageList.Font = new Font(Font.FontFamily, 9.5F);
        _imageList.AccessibleName = "Images in composition order";
        _imageList.DrawItem += DrawImageListItem;
        _imageList.SelectedIndexChanged += (_, _) => UpdateMoveButtons();
        content.Controls.Add(_imageList, 0, 2);

        var moveButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = SurfaceColor, Margin = Padding.Empty };
        moveButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        moveButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        moveButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        moveButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _moveUp.Dock = DockStyle.Fill;
        _moveDown.Dock = DockStyle.Fill;
        _moveUp.Margin = new Padding(0, 3, 4, 0);
        _moveDown.Margin = new Padding(4, 3, 0, 0);
        _moveUp.AccessibleName = "Move selected image up";
        _moveDown.AccessibleName = "Move selected image down";
        _settingTooltips.SetToolTip(_moveUp, "Move the selected image up. Shortcut: Alt + Up.");
        _settingTooltips.SetToolTip(_moveDown, "Move the selected image down. Shortcut: Alt + Down.");
        _moveUp.Click += (_, _) => MoveSelectedImage(-1);
        _moveDown.Click += (_, _) => MoveSelectedImage(1);
        _removeSelectedButton.Dock = DockStyle.Fill;
        _removeSelectedButton.Margin = new Padding(4, 3, 0, 0);
        _removeSelectedButton.AccessibleName = "Remove selected image";
        _removeSelectedButton.Click += (_, _) => RemoveSelectedImage();
        _settingTooltips.SetToolTip(_removeSelectedButton, "Remove the selected image from the list. You can also press Delete.");
        moveButtons.Controls.Add(_moveUp, 0, 0);
        moveButtons.Controls.Add(_moveDown, 1, 0);
        moveButtons.Controls.Add(_removeSelectedButton, 2, 0);
        content.Controls.Add(moveButtons, 0, 3);

        var hint = new Label { Text = "Drop files here · Alt + ↑ / ↓ to reorder", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, AutoSize = true, Font = Font, ForeColor = MutedColor };
        content.RowStyles[4] = new RowStyle(SizeType.Absolute, hint.PreferredHeight + 2);
        hint.AutoSize = false;
        hint.AccessibleName = "Image reorder instructions";
        content.Controls.Add(hint, 0, 4);
        body.Controls.Add(content);
        _imageList.DragEnter += AcceptImageDrop;
        _imageList.DragDrop += AddDroppedImages;
        SetDropTarget(card);
        SetDropTarget(body);
        SetDropTarget(hint);
        return card;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = WindowColor, Padding = new Padding(0, 4, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = WindowColor, Margin = Padding.Empty };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        _status.Dock = DockStyle.Fill;
        _status.AutoEllipsis = true;
        left.Controls.Add(_status, 0, 0);
        _outputActions.Controls.Add(_openImageButton);
        _outputActions.Controls.Add(_openFolderButton);
        _openImageButton.AutoSize = true;
        _openFolderButton.AutoSize = true;
        _openImageButton.Height = 29;
        _openFolderButton.Height = 29;
        _openImageButton.Click += (_, _) => OpenGeneratedImage();
        _openFolderButton.Click += (_, _) => OpenGeneratedFolder();
        left.Controls.Add(_outputActions, 0, 1);
        footer.Controls.Add(left, 0, 0);
        _generateButton.Dock = DockStyle.Fill;
        _generateButton.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        _generateButton.TextAlign = ContentAlignment.MiddleCenter;
        _generateButton.Margin = new Padding(12, 0, 0, 0);
        _generateButton.AccessibleName = "Create PNG composition";
        _generateButton.Click += async (_, _) => await GenerateImageAsync();
        footer.Controls.Add(_generateButton, 1, 0);
        return footer;
    }

    private static Panel CreateCard() => new() { Dock = DockStyle.Fill, BackColor = BorderColor, Padding = new Padding(1) };

    private static Panel CreateCardInterior() => new() { Dock = DockStyle.Fill, BackColor = SurfaceColor, Padding = Padding.Empty };

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = TextColor,
        Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        AutoSize = true
    };

    private static Control MakeSettingField(string labelText, Control input)
    {
        var field = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = SurfaceColor, Margin = new Padding(3, 0, 3, 0) };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        field.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, ForeColor = MutedColor, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 2), AccessibleName = labelText }, 0, 0);
        input.Dock = DockStyle.Fill;
        input.Margin = Padding.Empty;
        input.ForeColor = TextColor;
        input.BackColor = FieldColor;
        if (string.IsNullOrWhiteSpace(input.AccessibleName))
            input.AccessibleName = labelText;
        if (input is ComboBox combo)
            combo.FlatStyle = FlatStyle.Flat;
        if (input is DarkNumericUpDown number)
        {
            number.AutoSize = false;
            number.BorderStyle = BorderStyle.None;
            number.Font = new Font("Segoe UI", 10F);
            number.MinimumSize = new Size(0, 28);
        }
        field.Controls.Add(input, 0, 1);
        return field;
    }

    private static BackdropButton MakeButton(string text, bool primary = false) => new(text, primary);

    private void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? Color.FromArgb(68, 70, 77) : FieldColor);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var textBounds = Rectangle.Inflate(e.Bounds, -26, 0);
        TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]), combo.Font, textBounds, TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (combo.SelectedIndex == e.Index)
        {
            var x = e.Bounds.Left + 12;
            var y = e.Bounds.Top + e.Bounds.Height / 2;
            using var check = new Pen(TextColor, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(check, x, y, x + 3, y + 3);
            e.Graphics.DrawLine(check, x + 3, y + 3, x + 8, y - 4);
        }
        if ((e.State & DrawItemState.Focus) != 0)
        {
            using var focus = new Pen(BackdropPalette.Focus);
            e.Graphics.DrawRectangle(focus, e.Bounds.Left, e.Bounds.Top, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
    }

    private void DrawImageListItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? Color.FromArgb(68, 70, 77) : FieldColor);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, _imageList.Items[e.Index]?.ToString() ?? "", _imageList.Font, e.Bounds, TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }

    private void WirePreviewEvents()
    {
        _padding.ValueChanged += (_, _) => QueuePreview();
        _shadow.ValueChanged += (_, _) => QueuePreview();
        _outputWidth.ValueChanged += (_, _) => QueuePreview();
        _ratio.SelectedIndexChanged += (_, _) => QueuePreview();
        _layout.SelectedIndexChanged += (_, _) => QueuePreview();
    }

    private void QueuePreview()
    {
        if (IsDisposed || Disposing)
            return;
        var previous = _previewCancellation;
        previous?.Cancel();
        if (!ReferenceEquals(previous, _runningPreviewCancellation))
            previous?.Dispose();
        _previewCancellation = new CancellationTokenSource();
        _previewRequest++;
        _previewTimer.Stop();
        _previewTimer.Start();
        _previewPlaceholder.Text = _imagePaths.Length == 0 ? "Updating sample preview…" : "Updating preview…";
        _previewPlaceholder.Visible = _preview.Image is null;
        _previewCaption.Text = "Updating preview…";
        _status.Text = "Updating preview…";
    }

    private async void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        var cancellation = _previewCancellation;
        if (cancellation is null)
            return;
        _runningPreviewCancellation = cancellation;

        var request = _previewRequest;
        var paths = _imagePaths.ToArray();
        var settings = CurrentSettings();
        Bitmap? image = null;
        try
        {
            await _previewGate.WaitAsync(cancellation.Token);
            try
            {
                image = await Task.Run(
                    () => paths.Length == 0
                        ? BackdropRenderer.CreateSamplePreview(settings, BackdropRenderer.MaximumPreviewLongEdge)
                        : BackdropRenderer.RenderPreviewFiles(paths, settings, cancellation.Token),
                    cancellation.Token);
            }
            finally
            {
                _previewGate.Release();
            }

            if (cancellation.IsCancellationRequested || request != _previewRequest || IsDisposed)
                return;
            ReplacePreview(image);
            image = null;
            _previewCaption.Text = paths.Length == 0
                ? "Sample only · add images to preview your composition."
                : $"{paths.Length} image{(paths.Length == 1 ? "" : "s")} · no cropping.";
            _previewImageCount.Text = paths.Length == 0 ? "SAMPLE" : $"{paths.Length} IMAGE{(paths.Length == 1 ? "" : "S")}";
            _status.Text = "Preview ready.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (request == _previewRequest && !IsDisposed)
            {
                _previewCaption.Text = $"Preview could not be updated: {ex.Message}";
                _previewPlaceholder.Text = "Preview unavailable";
                _status.Text = "Check that the selected images are valid and available.";
            }
        }
        finally
        {
            image?.Dispose();
            cancellation.Dispose();
            if (ReferenceEquals(_runningPreviewCancellation, cancellation))
                _runningPreviewCancellation = null;
            if (ReferenceEquals(_previewCancellation, cancellation))
                _previewCancellation = null;
        }
    }

    private AppSettings CurrentSettings() => new()
    {
        PaddingPercent = (int)_padding.Value,
        ShadowStrength = (double)_shadow.Value / 100,
        OutputWidth = (int)_outputWidth.Value,
        CanvasRatio = _ratio.SelectedIndex switch
        {
            0 => CanvasRatio.Auto,
            2 => CanvasRatio.Square1x1,
            3 => CanvasRatio.Portrait4x5,
            _ => CanvasRatio.Wide16x9
        },
        Layout = _layout.SelectedIndex switch
        {
            1 => CompositionLayout.Row,
            2 => CompositionLayout.Grid,
            _ => CompositionLayout.Auto
        },
        BackgroundMode = _backgroundMode,
        BackgroundColor1Hex = _backgroundColor1Hex,
        BackgroundColor2Hex = _backgroundColor2Hex,
        BackgroundPattern = _backgroundPattern
    };

    private void EditBackground()
    {
        var previousMode = _backgroundMode;
        var previousColor1 = _backgroundColor1Hex;
        var previousColor2 = _backgroundColor2Hex;
        var previousPattern = _backgroundPattern;
        using var dialog = new BackgroundSettingsDialog(CurrentSettings());
        dialog.DraftChanged += (_, _) => ApplyBackgroundDraft(dialog);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ApplyBackgroundDraft(dialog);
            _status.Text = "Background updated. Save preferences to keep it.";
            return;
        }

        _backgroundMode = previousMode;
        _backgroundColor1Hex = previousColor1;
        _backgroundColor2Hex = previousColor2;
        _backgroundPattern = previousPattern;
        UpdateBackgroundModeLabel();
        QueuePreview();
    }

    private void ApplyBackgroundDraft(BackgroundSettingsDialog dialog)
    {
        _backgroundMode = dialog.SelectedMode;
        _backgroundColor1Hex = dialog.Color1Hex;
        _backgroundColor2Hex = dialog.Color2Hex;
        _backgroundPattern = dialog.SelectedPattern;
        UpdateBackgroundModeLabel();
        QueuePreview();
    }

    private void UpdateBackgroundModeLabel()
    {
        _backgroundModeLabel.Text = _backgroundMode switch
        {
            BackgroundMode.AutoGradient => "Auto",
            BackgroundMode.SolidColor => "Solid",
            BackgroundMode.CustomGradient => "Gradient",
            BackgroundMode.Pattern => _backgroundPattern == BackgroundPattern.Dots ? "Dots" : "Grain",
            _ => "Auto"
        };
        _backgroundModeLabel.AccessibleName = $"Current background: {_backgroundModeLabel.Text}";
        _backgroundButton.AccessibleDescription = $"Current background: {_backgroundModeLabel.Text}. Edit background colors and pattern.";
    }

    private void ChooseImages()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add images to the composition",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            Multiselect = true,
            CheckFileExists = true,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            AddImages(dialog.FileNames);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void AddImages(IEnumerable<string> incomingPaths)
    {
        var updated = _imagePaths.ToList();
        var known = new HashSet<string>(_imagePaths, StringComparer.OrdinalIgnoreCase);
        foreach (var path in incomingPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (!BackdropRenderer.IsSupportedImagePath(fullPath))
                throw new InvalidDataException($"'{Path.GetFileName(fullPath)}' is not a supported image.");
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"The selected image was not found: {fullPath}", fullPath);
            if (!known.Add(fullPath))
                continue;
            if (updated.Count == BackdropRenderer.MaximumImages)
                throw new InvalidDataException($"Choose no more than {BackdropRenderer.MaximumImages} images.");
            updated.Add(fullPath);
        }

        if (updated.Count == _imagePaths.Length)
        {
            _status.Text = "Those images are already selected.";
            return;
        }
        _imagePaths = updated.ToArray();
        UpdateImageList(_imagePaths.Length - 1);
        QueuePreview();
        UpdateGenerateButton();
    }

    private void UpdateImageList(int selectedIndex = -1)
    {
        _imageList.BeginUpdate();
        try
        {
            _imageList.Items.Clear();
            for (var i = 0; i < _imagePaths.Length; i++)
                _imageList.Items.Add($"{i + 1}.  {Path.GetFileName(_imagePaths[i])}");
            if (_imagePaths.Length > 0)
                _imageList.SelectedIndex = selectedIndex is >= 0 && selectedIndex < _imagePaths.Length ? selectedIndex : 0;
        }
        finally
        {
            _imageList.EndUpdate();
        }
        _selectedCount.Text = _imagePaths.Length == 0
            ? $"0 / {BackdropRenderer.MaximumImages}"
            : $"{_imagePaths.Length} / {BackdropRenderer.MaximumImages}";
        _previewImageCount.Text = _imagePaths.Length == 0 ? "SAMPLE" : $"{_imagePaths.Length} IMAGE{(_imagePaths.Length == 1 ? "" : "S")}";
        UpdateMoveButtons();
        UpdateGenerateButton();
    }

    private void MoveSelectedImage(int direction)
    {
        var index = _imageList.SelectedIndex;
        var next = index + direction;
        if (index < 0 || next < 0 || next >= _imagePaths.Length)
            return;
        var updated = _imagePaths.ToList();
        (updated[index], updated[next]) = (updated[next], updated[index]);
        _imagePaths = updated.ToArray();
        UpdateImageList(next);
        QueuePreview();
    }

    private void UpdateMoveButtons()
    {
        var index = _imageList.SelectedIndex;
        _moveUp.Enabled = index > 0;
        _moveDown.Enabled = index >= 0 && index < _imagePaths.Length - 1;
        _removeSelectedButton.Enabled = index >= 0 && index < _imagePaths.Length;
        _clearImagesButton.Enabled = _imagePaths.Length > 0;
    }

    private void RemoveSelectedImage()
    {
        var index = _imageList.SelectedIndex;
        if (index < 0 || index >= _imagePaths.Length)
            return;

        var updated = _imagePaths.ToList();
        updated.RemoveAt(index);
        _imagePaths = updated.ToArray();
        UpdateImageList(_imagePaths.Length == 0 ? -1 : Math.Min(index, _imagePaths.Length - 1));
        QueuePreview();
        _status.Text = _imagePaths.Length == 0 ? "Image list cleared." : "Selected image removed.";
    }

    private void ClearImages()
    {
        if (_imagePaths.Length == 0)
            return;

        _imagePaths = [];
        UpdateImageList(-1);
        QueuePreview();
        _status.Text = "Image list cleared.";
    }

    private void UpdateGenerateButton()
    {
        _generateButton.Text = _isGenerating ? "Cancel" : "Create PNG";
        _generateButton.AccessibleName = _isGenerating ? "Cancel PNG creation" : "Create PNG composition";
        _generateButton.Enabled = _isGenerating
            ? _exportCancellation?.IsCancellationRequested != true
            : _imagePaths.Length > 0;
    }

    private async Task GenerateImageAsync()
    {
        if (_isGenerating)
        {
            _exportCancellation?.Cancel();
            _generateButton.Enabled = false;
            _status.Text = "Cancelling PNG creation…";
            return;
        }
        if (_imagePaths.Length == 0)
            return;

        var paths = _imagePaths.ToArray();
        var settings = CurrentSettings();
        _exportCancellation = new CancellationTokenSource();
        var cancellationToken = _exportCancellation.Token;
        _isGenerating = true;
        UpdateGenerateButton();
        _status.Text = "Creating PNG…";
        try
        {
            var output = await Task.Run(() => BackdropRenderer.Generate(paths, settings, cancellationToken), cancellationToken);
            _generatedOutput = Path.GetFullPath(output);
            using var image = Image.FromFile(_generatedOutput);
            _status.Text = $"Created {Path.GetFileName(output)} · {image.Width} × {image.Height} px.";
            _outputActions.Visible = true;
            _previewCaption.Text = "Your full-resolution PNG is ready.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _status.Text = "PNG creation cancelled. No new output was created.";
        }
        catch (Exception ex)
        {
            ShowOperationError(ex);
        }
        finally
        {
            _isGenerating = false;
            _exportCancellation?.Dispose();
            _exportCancellation = null;
            UpdateGenerateButton();
        }
    }

    private void SavePreferences()
    {
        try
        {
            SettingsStore.Save(CurrentSettings());
            _status.Text = "Preferences saved.";
        }
        catch (Exception ex)
        {
            ShowOperationError(ex);
        }
    }

    private async void ToggleExplorerMenu()
    {
        if (!_shellMenuScriptsAvailable)
        {
            ShowError("The Explorer menu scripts are not beside this application. Use the packaged Backdrop build to enable the Windows 11 menu.");
            return;
        }

        _isShellMenuChanging = true;
        _shortcutButton.Enabled = false;
        try
        {
            var installed = await IsExplorerMenuInstalledAsync();
            var scripts = Path.Combine(AppContext.BaseDirectory, "scripts");
            var script = _classicShellHelperAvailable
                ? Path.Combine(scripts, "classic-shell.ps1")
                : Path.Combine(scripts, installed ? "uninstall-shell.ps1" : "install-shell.ps1");
            if (!File.Exists(script))
                throw new FileNotFoundException("The Explorer menu script is missing.", script);

            _status.Text = installed ? "Removing the Windows 11 menu…" : "Enabling the Windows 11 menu…";
            var result = _classicShellHelperAvailable
                ? await RunPowerShellAsync("-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Action", installed ? "Unregister" : "Register")
                : await RunPowerShellAsync("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);

            _status.Text = installed ? "Windows 11 menu removed." : "Windows 11 menu enabled.";
            await RefreshExplorerMenuStatusAsync();
        }
        catch (Exception ex)
        {
            ShowOperationError(ex);
        }
        finally
        {
            _isShellMenuChanging = false;
            if (!IsDisposed)
                _shortcutButton.Enabled = _shellMenuScriptsAvailable;
        }
    }

    private async Task RefreshExplorerMenuStatusAsync()
    {
        var scripts = Path.Combine(AppContext.BaseDirectory, "scripts");
        _classicShellHelperAvailable = File.Exists(Path.Combine(scripts, "classic-shell.ps1"));
        _shellMenuScriptsAvailable = _classicShellHelperAvailable ||
            (File.Exists(Path.Combine(scripts, "install-shell.ps1")) &&
             File.Exists(Path.Combine(scripts, "uninstall-shell.ps1")));
        _shortcutButton.Enabled = _shellMenuScriptsAvailable;
        _shortcutButton.Text = "Enable Explorer menu";
        _shortcutButton.AccessibleName = _shortcutButton.Text;
        if (!_shellMenuScriptsAvailable)
            return;

        _shortcutButton.Enabled = false;
        _shortcutButton.Text = "Checking Explorer menu…";
        try
        {
            var installed = await IsExplorerMenuInstalledAsync();
            if (IsDisposed)
                return;
            _shortcutButton.Text = installed ? "Remove Explorer menu" : "Enable Explorer menu";
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _shortcutButton.Text = "Check Windows 11 menu";
                _status.Text = $"Could not check the Explorer menu: {ex.Message}";
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                _shortcutButton.AccessibleName = _shortcutButton.Text;
                _shortcutButton.Enabled = _shellMenuScriptsAvailable;
            }
        }
    }

    private static async Task<bool> IsExplorerMenuInstalledAsync()
    {
        var classicShellHelper = Path.Combine(AppContext.BaseDirectory, "scripts", "classic-shell.ps1");
        if (File.Exists(classicShellHelper))
        {
            var classicResult = await RunPowerShellAsync("-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", classicShellHelper, "-Action", "Status");
            if (classicResult.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(classicResult.Error) ? classicResult.Output : classicResult.Error);
            return classicResult.Output.Trim().Equals("installed", StringComparison.OrdinalIgnoreCase);
        }

        var command = $"$ErrorActionPreference = 'Stop'; if (Get-AppxPackage -Name '{ShellPackageName}') {{ [Console]::Write('installed') }}";
        var result = await RunPowerShellAsync("-NoProfile", "-NonInteractive", "-Command", command);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        return result.Output.Trim().Equals("installed", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunPowerShellAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PowerShell.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private void ReplacePreview(Image image)
    {
        var old = _preview.Image;
        _preview.Image = image;
        _preview.Visible = true;
        _previewPlaceholder.Visible = false;
        old?.Dispose();
    }

    private void OpenGeneratedImage() => OpenGeneratedPath(_generatedOutput);

    private void OpenGeneratedFolder()
    {
        var folder = _generatedOutput is null ? null : Path.GetDirectoryName(_generatedOutput);
        OpenGeneratedPath(folder);
    }

    private void OpenGeneratedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Could not open the selected location.\n\n{ex.Message}");
        }
    }

    private static Bitmap LoadPreviewFile(string path)
    {
        using var image = Image.FromFile(path);
        return new Bitmap(image);
    }

    private void SetDropTarget(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += AcceptImageDrop;
        control.DragDrop += AddDroppedImages;
    }

    private static void AcceptImageDrop(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void AddDroppedImages(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
            return;
        try
        {
            AddImages(paths);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowOperationError(Exception ex)
    {
        var detail = ex is IOException or UnauthorizedAccessException
            ? $"Check that the selected images are available and that the first image's folder is writable.\n\n{ex.Message}"
            : ex.Message;
        ShowError(detail);
    }

    private void ShowError(string message) => MessageBox.Show(this, message, "Backdrop", MessageBoxButtons.OK, MessageBoxIcon.Error);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
