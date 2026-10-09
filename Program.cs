using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Backdrop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0].Equals("--generate", StringComparison.OrdinalIgnoreCase))
            return Generate(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0].Equals("--self-check", StringComparison.OrdinalIgnoreCase))
            return SelfCheck(args.Skip(1).ToArray());
        if (args.Length > 0)
        {
            MessageBox.Show(null, "Usage: Backdrop.exe [--generate image1 [image2 ...] | --self-check [output-folder]]", "Backdrop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }

        var loaded = SettingsStore.Load();
        if (loaded.Warning is not null)
            MessageBox.Show(null, loaded.Warning, "Backdrop preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        Application.Run(new MainForm(loaded.Settings));
        return 0;
    }

    private static int Generate(string[] paths)
    {
        try
        {
            var loaded = SettingsStore.Load();
            if (loaded.Warning is not null)
                MessageBox.Show(null, loaded.Warning, "Backdrop preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            var output = BackdropRenderer.Generate(paths, loaded.Settings);
            if (!WriteCliOutput(output))
                Application.Run(new GenerateCompletionToast(output));
            return 0;
        }
        catch (Exception ex)
        {
            var message = ex is IOException or UnauthorizedAccessException
                ? $"Check that the selected images are available and that the first image's folder is writable.\n\n{ex.Message}"
                : ex.Message;
            MessageBox.Show(null, message, "Backdrop could not generate the image", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static bool WriteCliOutput(string path)
    {
        _ = AttachConsole(uint.MaxValue);
        var handle = GetStdHandle(-11);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;

        try
        {
            using var writer = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding, 1024, leaveOpen: true)
            {
                AutoFlush = true
            };
            writer.WriteLine(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    private static int SelfCheck(string[] args)
    {
        var output = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "artifacts", "self-check");
        try
        {
            var report = SelfCheckRunner.Run(output);
            Console.WriteLine(report);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class GenerateCompletionToast : Form
{
    private const int ExtendedStyleNoActivate = 0x08000000;
    private const int ExtendedStyleToolWindow = 0x00000080;
    private readonly string _output;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 8000 };

    public GenerateCompletionToast(string output)
    {
        _output = Path.GetFullPath(output);
        Text = "Backdrop created an image";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(360, 152);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 18, area.Bottom - Height - 18);
        BackColor = Color.FromArgb(31, 32, 36);
        ForeColor = Color.FromArgb(235, 236, 239);
        Padding = new Padding(1);
        AccessibleName = "Backdrop image created";

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(15, 11, 15, 12), BackColor = Color.FromArgb(31, 32, 36) };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        content.Controls.Add(new Label { Text = "PNG created", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(235, 236, 239), Font = new Font("Segoe UI", 11F, FontStyle.Bold) }, 0, 0);
        content.Controls.Add(new Label { Text = Path.GetFileName(_output), Dock = DockStyle.Fill, ForeColor = Color.FromArgb(212, 214, 219), AutoEllipsis = true }, 0, 1);
        content.Controls.Add(new Label { Text = Path.GetDirectoryName(_output), Dock = DockStyle.Fill, ForeColor = Color.FromArgb(158, 160, 167), AutoEllipsis = true }, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.FromArgb(31, 32, 36) };
        var folderButton = CreateActionButton("Show folder");
        folderButton.Click += (_, _) => Open(Path.GetDirectoryName(_output)!);
        var imageButton = CreateActionButton("Open image", primary: true);
        imageButton.Click += (_, _) => Open(_output);
        actions.Controls.Add(folderButton);
        actions.Controls.Add(imageButton);
        content.Controls.Add(actions, 0, 3);
        Controls.Add(content);

        _timer.Tick += (_, _) => Close();
        Shown += (_, _) =>
        {
            _timer.Start();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= ExtendedStyleNoActivate | ExtendedStyleToolWindow;
            return parameters;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(65, 67, 74));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private static BackdropButton CreateActionButton(string text, bool primary = false) => new(text, primary)
    {
        Width = 120,
        Height = 32,
        AccessibleName = text,
    };

    private void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the selected location.\n\n{ex.Message}", "Backdrop", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal static class SelfCheckRunner
{
    public static string Run(string outputFolder)
    {
        var root = Path.GetFullPath(outputFolder);
        Directory.CreateDirectory(root);
        var runFolder = Path.Combine(root, $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}");
        var inputFolder = Path.Combine(runFolder, "inputs");
        Directory.CreateDirectory(inputFolder);

        var sourceBitmaps = BackdropRenderer.CreateSampleInputs(inputFolder);
        try
        {
            var paths = new[]
            {
                Path.Combine(inputFolder, "portrait one.png"),
                Path.Combine(inputFolder, "portrait two.png"),
                Path.Combine(inputFolder, "landscape.png"),
                Path.Combine(inputFolder, "portrait three café.png")
            };
            var originalHashes = paths.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
            var settings = new AppSettings();
            Assert(settings.CanvasRatio == CanvasRatio.Wide16x9 && settings.Layout == CompositionLayout.Auto, "New and legacy preferences use the wide automatic defaults");
            Assert(settings.BackgroundMode == BackgroundMode.AutoGradient && settings.BackgroundPattern == BackgroundPattern.SoftGrain,
                "New preferences use automatic gradient and soft-grain defaults");

            var legacyFile = Path.Combine(runFolder, "legacy-settings.json");
            File.WriteAllText(legacyFile, "{\"OutputWidth\":1920,\"PaddingPercent\":10,\"ShadowStrength\":0.18}", Encoding.UTF8);
            var legacySettings = SettingsStore.LoadPath(legacyFile).Settings;
            Assert(legacySettings.CanvasRatio == CanvasRatio.Wide16x9 && legacySettings.Layout == CompositionLayout.Auto, "Old preference files keep wide automatic defaults");
            Assert(legacySettings.BackgroundMode == BackgroundMode.AutoGradient && legacySettings.BackgroundColor1Hex == "#303137" && legacySettings.BackgroundPattern == BackgroundPattern.SoftGrain,
                "Old preference files keep the automatic background defaults");

            var autoSettings = settings.Copy();
            autoSettings.CanvasRatio = CanvasRatio.Auto;
            var autoSingleCanvas = BackdropRenderer.GetCanvasDimensions([new Size(420, 680)], autoSettings);
            Assert(Math.Abs(autoSingleCanvas.Width / (double)autoSingleCanvas.Height - 420d / 680) < 0.001, "Auto single-image canvas follows the image aspect ratio");
            autoSettings.CanvasRatio = CanvasRatio.Square1x1;
            Assert(BackdropRenderer.GetCanvasDimensions([new Size(420, 680)], autoSettings).Width == BackdropRenderer.GetCanvasDimensions([new Size(420, 680)], autoSettings).Height, "Square canvas is 1:1");
            autoSettings.CanvasRatio = CanvasRatio.Portrait4x5;
            var portraitCanvas = BackdropRenderer.GetCanvasDimensions([new Size(420, 680)], autoSettings);
            Assert(Math.Abs(portraitCanvas.Width / (double)portraitCanvas.Height - 0.8) < 0.001, "Portrait canvas is 4:5");

            var rowSettings = settings.Copy();
            rowSettings.Layout = CompositionLayout.Row;
            rowSettings.CanvasRatio = CanvasRatio.Auto;
            var orderedRow = BackdropRenderer.GetImageBounds([new Size(420, 680), new Size(680, 420)], rowSettings);
            Assert(orderedRow[0].Left < orderedRow[1].Left && orderedRow[0].Width < orderedRow[1].Width, "Row keeps image order and each source aspect ratio");
            var clippedRowRatio = BackdropRenderer.GetCanvasDimensions(Enumerable.Repeat(new Size(680, 420), 9).ToArray(), rowSettings);
            Assert(Math.Abs(clippedRowRatio.Width / (double)clippedRowRatio.Height - 3) < 0.01, "Auto row canvas ratio is capped at 3:1");

            var fiveImages = Enumerable.Repeat(new Size(420, 680), 5).ToArray();
            var centeredGrid = BackdropRenderer.GetImageBounds(fiveImages, settings);
            var gridCanvas = BackdropRenderer.GetCanvasDimensions(fiveImages, settings);
            Assert(Math.Abs((centeredGrid[^2].Left + centeredGrid[^1].Right) / 2 - gridCanvas.Width / 2f) < 0.1f, "Grid centers its final incomplete row");

            var single = BackdropRenderer.Generate([paths[0]], settings);
            Assert(Path.GetFileName(single) == "portrait one-backdrop.png", "Single-image output name");
            var singleBounds = BackdropRenderer.GetImageBounds([new Size(420, 680)], settings)[0];
            Assert(Math.Abs(singleBounds.Height - 1080 * 0.8f) < 0.1f && singleBounds.Width <= 1920 * 0.8f + 0.1f, "Single image fills the 80-percent content bound without cropping");
            var singleCollision = BackdropRenderer.Generate([paths[0]], settings);
            Assert(Path.GetFileName(singleCollision) == "portrait one-backdrop-2.png", "Collision-safe single-image name");
            AssertImageSize(single, 1920, 1080);

            var portraitBounds = BackdropRenderer.GetImageBounds([new Size(420, 680), new Size(420, 560)], settings);
            Assert(portraitBounds.Count == 2 && Math.Abs(portraitBounds[0].Height - portraitBounds[1].Height) < 0.1f, "Portrait row uses equal heights");
            var portraits = BackdropRenderer.Generate(paths[..2], settings);
            Assert(Path.GetFileName(portraits) == "backdrop-composition.png", "Portrait selection produces one composition");
            AssertImageSize(portraits, 1920, 1080);
            var threePortraitBounds = BackdropRenderer.GetImageBounds(
                [new Size(420, 680), new Size(420, 560), new Size(440, 700)], settings);
            Assert(threePortraitBounds.Count == 3 && threePortraitBounds.Max(bounds => bounds.Height) - threePortraitBounds.Min(bounds => bounds.Height) < 0.1f, "Three portraits share one equal-height row");
            var threePortraits = BackdropRenderer.Generate([paths[0], paths[1], paths[3]], settings);
            Assert(Path.GetFileName(threePortraits) == "backdrop-composition-2.png", "Three-portrait row is saved as one composition");
            AssertImageSize(threePortraits, 1920, 1080);

            var mixed = BackdropRenderer.Generate(paths, settings);
            Assert(Path.GetFileName(mixed) == "backdrop-composition-3.png", "Mixed selection produces one collision-safe composition");
            AssertImageSize(mixed, 1920, 1080);

            using (var cancelledExport = new CancellationTokenSource())
            {
                cancelledExport.Cancel();
                var cancelledOutput = Path.Combine(inputFolder, "landscape-backdrop.png");
                ExpectThrows<OperationCanceledException>(
                    () => BackdropRenderer.Generate([paths[2]], settings, cancelledExport.Token),
                    "Canceled export exits before creating a PNG");
                Assert(!File.Exists(cancelledOutput) && Directory.GetFiles(inputFolder, ".backdrop-*.tmp").Length == 0,
                    "Canceled export leaves no final or temporary output");
            }

            var gridBounds = BackdropRenderer.GetImageBounds(Enumerable.Repeat(new Size(420, 680), 9).ToArray(), settings);
            Assert(gridBounds.Count == 9, "Automatic grid accepts nine images");

            var sampleFile = Path.Combine(runFolder, "sample-preview.png");
            using (var sample = BackdropRenderer.CreateSamplePreview(settings, BackdropRenderer.MaximumPreviewLongEdge))
            {
                Assert(sample.Width == 1000 && Math.Abs(sample.Width / (double)sample.Height - 16d / 9) < 0.004,
                    "Sample preview uses the shared renderer at the selected wide ratio");
                sample.Save(sampleFile, ImageFormat.Png);
            }
            var sampleAutoSettings = settings.Copy();
            sampleAutoSettings.OutputWidth = 640;
            sampleAutoSettings.CanvasRatio = CanvasRatio.Auto;
            sampleAutoSettings.Layout = CompositionLayout.Auto;
            using (var sampleRow = BackdropRenderer.CreateSamplePreview(sampleAutoSettings, 640))
                Assert(sampleRow.Width == 640 && Math.Abs(sampleRow.Width / (double)sampleRow.Height - 1.58) < 0.02,
                    "Three portrait sample screens use the shared Auto row composition");

            CheckBackgroundModes(settings, runFolder, paths[0]);

            ExpectThrows<ArgumentOutOfRangeException>(
                () => BackdropRenderer.RenderFiles(Enumerable.Repeat("unused.png", 10).ToArray(), settings),
                "Image-count limit is checked before file loading");

            for (var i = 0; i < paths.Length; i++)
                Assert(originalHashes[i].SequenceEqual(SHA256.HashData(File.ReadAllBytes(paths[i]))), "Source image remains unchanged");

            var preferenceFile = Path.Combine(runFolder, "preferences", "settings.json");
            var savedSettings = new AppSettings
            {
                OutputWidth = 1600, PaddingPercent = 12, ShadowStrength = 0.26,
                CanvasRatio = CanvasRatio.Portrait4x5, Layout = CompositionLayout.Row,
                BackgroundMode = BackgroundMode.Pattern, BackgroundColor1Hex = "#29463D",
                BackgroundColor2Hex = "#D7A08A", BackgroundPattern = BackgroundPattern.Dots
            };
            SettingsStore.SavePath(preferenceFile, savedSettings);
            var roundTrip = SettingsStore.LoadPath(preferenceFile).Settings;
            Assert(roundTrip.OutputWidth == 1600 && roundTrip.PaddingPercent == 12 && Math.Abs(roundTrip.ShadowStrength - 0.26) < 0.0001 && roundTrip.CanvasRatio == CanvasRatio.Portrait4x5 && roundTrip.Layout == CompositionLayout.Row && roundTrip.BackgroundMode == BackgroundMode.Pattern && roundTrip.BackgroundPattern == BackgroundPattern.Dots && roundTrip.BackgroundColor1Hex == "#29463D" && roundTrip.BackgroundColor2Hex == "#D7A08A", "Preferences round trip saves composition and background settings");
            var invalidEnumFile = Path.Combine(runFolder, "invalid-enum-settings.json");
            File.WriteAllText(invalidEnumFile, "{\"CanvasRatio\":\"fisheye\",\"Layout\":\"Auto\",\"BackgroundMode\":\"LinearNoise\"}", Encoding.UTF8);
            var invalidEnum = SettingsStore.LoadPath(invalidEnumFile);
            Assert(invalidEnum.Warning is not null && invalidEnum.Settings.CanvasRatio == CanvasRatio.Wide16x9, "Unknown preference enum safely falls back to defaults");
            var invalidBackgroundFile = Path.Combine(runFolder, "invalid-background-settings.json");
            File.WriteAllText(invalidBackgroundFile, "{\"BackgroundMode\":\"solidColor\",\"BackgroundColor1Hex\":\"# FFFFF\"}", Encoding.UTF8);
            var invalidBackground = SettingsStore.LoadPath(invalidBackgroundFile);
            Assert(invalidBackground.Warning is not null && invalidBackground.Settings.BackgroundMode == BackgroundMode.AutoGradient && invalidBackground.Settings.BackgroundColor1Hex == "#303137",
                "Unknown background modes and whitespace in hex colors use safe defaults");
            var corruptFile = Path.Combine(runFolder, "corrupt-settings.json");
            File.WriteAllText(corruptFile, "{not-json", Encoding.UTF8);
            var corrupt = SettingsStore.LoadPath(corruptFile);
            Assert(corrupt.Warning is not null && corrupt.Settings.OutputWidth == 1920, "Corrupt preferences use defaults and report a warning");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                ExpectThrows<OperationCanceledException>(
                    () => BackdropRenderer.RenderFiles([paths[0]], settings, cancelled.Token),
                    "Canceled preview exits before loading the image");
            }

            using (var form = new MainForm(settings, initialPaths: paths[..3], previewFile: sampleFile))
            {
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                form.ClientSize = new Size(960, 720);
                form.PerformLayout();
                Application.DoEvents();
                AssertSidebarLabelSizing(form, "wide");
                AssertBackgroundHeaderSizing(form, "wide");
                CaptureForm(form, Path.Combine(runFolder, "form-preview.png"), new Size(960, 720));
                form.Opacity = 1;
                CaptureNativeForm(form, Path.Combine(runFolder, "form-preview-live-wide.png"));
                form.ClientSize = new Size(800, 600);
                form.PerformLayout();
                Application.DoEvents();
                AssertSidebarLabelSizing(form, "compact");
                AssertBackgroundHeaderSizing(form, "compact");
                CaptureForm(form, Path.Combine(runFolder, "form-preview-compact.png"), new Size(800, 600));
                CaptureNativeForm(form, Path.Combine(runFolder, "form-preview-live-compact.png"));
                AssertDarkControlChrome(form);

                using (var backgroundDialog = new BackgroundSettingsDialog(settings))
                {
                    var combos = Descendants(backgroundDialog).OfType<DarkComboBox>().ToArray();
                    Assert(combos.Length == 2, "Background editor has mode and pattern choices");
                    var draftChanges = 0;
                    backgroundDialog.DraftChanged += (_, _) => draftChanges++;
                    backgroundDialog.Show(form);
                    Application.DoEvents();
                    combos[0].SelectedIndex = (int)BackgroundMode.Pattern;
                    Application.DoEvents();
                    Assert(backgroundDialog.SelectedMode == BackgroundMode.Pattern && draftChanges > 0,
                        "Background editor emits live draft changes when the mode changes");
                    backgroundDialog.PerformLayout();
                    var editorFields = Descendants(backgroundDialog).OfType<TableLayoutPanel>().Single(panel =>
                        panel.ColumnCount == 1 && panel.RowCount == 4 &&
                        panel.Controls.Cast<Control>().Any(control => control.GetType().Name == "ColorSelectionRow"));
                    var editorTitle = Descendants(backgroundDialog).OfType<Label>().Single(label => label.Text == "Choose a background");
                    Assert(editorTitle.Visible && editorTitle.Height >= editorTitle.PreferredHeight,
                        "Background editor title is visible and fits its preferred height");
                    var modeRow = combos[0].Parent!;
                    Assert(modeRow.Visible && combos[0].Visible && combos[0].Width > 0 &&
                        modeRow.Left >= 0 && modeRow.Right <= editorFields.ClientSize.Width && modeRow.Height >= combos[0].Height &&
                        VisibleChildrenFit(modeRow),
                        "Background mode row and dropdown fit inside the editor field area");
                    var modeLabel = modeRow.Controls.OfType<Label>().Single();
                    var modeTextWidth = new[] { "Automatic gradient", "Solid color", "Two-color gradient", "Pattern" }
                        .Max(text => TextRenderer.MeasureText(text, combos[0].Font).Width);
                    Assert(modeLabel.Width >= TextRenderer.MeasureText("Background mode", modeLabel.Font).Width &&
                        combos[0].ClientSize.Width >= modeTextWidth + 32,
                        "Background mode label stays on one line and every mode name fits its dropdown");
                    var colorRows = Descendants(backgroundDialog).OfType<Control>().Where(control =>
                        control.AccessibleName is "Base color hex color" or "End color hex color").ToArray();
                    Assert(colorRows.Length == 2 && colorRows[0].Visible && !colorRows[1].Visible,
                        "Pattern mode shows one base color and hides the gradient end color");
                    var baseColorRow = editorFields.GetControlFromPosition(0, 1)!;
                    Assert(baseColorRow.Visible && baseColorRow.Height >= 40 &&
                        baseColorRow.Left >= 0 && baseColorRow.Right <= editorFields.ClientSize.Width &&
                        VisibleChildrenFit(baseColorRow),
                        $"Pattern base-color row is visible and fits inside the editor field area (row {baseColorRow.Bounds}, area {editorFields.ClientSize}, children {DescribeControls(baseColorRow)})");
                    var patternRow = combos[1].Parent!;
                    Assert(patternRow.Visible && combos[1].Visible && patternRow.Right <= editorFields.ClientSize.Width &&
                        VisibleChildrenFit(patternRow),
                        "Pattern choice row is visible and fits inside the editor field area");
                    CaptureDialog(backgroundDialog, Path.Combine(runFolder, "background-editor.png"));
                    backgroundDialog.BringToFront();
                    Application.DoEvents();
                    CaptureScreenForm(backgroundDialog, Path.Combine(runFolder, "background-editor-live.png"));
                    combos[0].SelectedIndex = (int)BackgroundMode.CustomGradient;
                    Application.DoEvents();
                    Assert(colorRows[0].Visible && colorRows[1].Visible,
                        "Two-color gradient mode shows both color rows");
                    Assert(VisibleChildrenFit(editorFields.GetControlFromPosition(0, 1)!) &&
                        VisibleChildrenFit(editorFields.GetControlFromPosition(0, 2)!),
                        "Both gradient color rows and their controls fit inside the editor");
                    backgroundDialog.Close();
                }

                var imageList = GetField<ListBox>(form, "_imageList");
                var removeImage = GetField<Button>(form, "_removeSelectedButton");
                var clearImages = GetField<Button>(form, "_clearImagesButton");
                var generateButton = GetField<Button>(form, "_generateButton");
                removeImage.PerformClick();
                Assert(imageList.Items.Count == 2 && imageList.SelectedIndex == 0 &&
                    GetField<Label>(form, "_previewImageCount").Text == "2 IMAGES",
                    "Remove selected updates order, selection, and image count");
                clearImages.PerformClick();
                Assert(imageList.Items.Count == 0 && GetField<Label>(form, "_previewImageCount").Text == "SAMPLE" &&
                    !generateButton.Enabled,
                    "Clear resets the list and disables PNG creation");
                form.Close();
            }

            File.Copy(mixed, Path.Combine(runFolder, "render-preview.png"));
            var report = string.Join(Environment.NewLine,
            [
                "Backdrop self-check passed.",
                "Checked: ratio defaults and presets, source aspect, row/grid order and centering, portrait layout, sample preview dimensions and Auto row, background defaults/modes/colors/pattern determinism, live editor visibility and draft events, collision-safe names, source preservation, image-count limit, enum validation, canceled preview/export cleanup, remove/clear list actions, preference round trip, rounded/dark control chrome, layout selection events, numeric bounds/value events, and 960x720/800x600 captures.",
                $"Sample: {sampleFile}",
                $"Render: {Path.Combine(runFolder, "render-preview.png")}",
                $"Form: {Path.Combine(runFolder, "form-preview.png")}",
                $"Compact form: {Path.Combine(runFolder, "form-preview-compact.png")}",
                $"Background editor: {Path.Combine(runFolder, "background-editor.png")}",
                $"Compact native window: {Path.Combine(runFolder, "form-preview-live-compact.png")}",
                $"Native background editor: {Path.Combine(runFolder, "background-editor-live.png")}"
            ]);
            File.WriteAllText(Path.Combine(root, "last-run.txt"), runFolder + Environment.NewLine);
            File.WriteAllText(Path.Combine(runFolder, "self-check-report.txt"), report + Environment.NewLine);
            return report;
        }
        finally
        {
            foreach (var bitmap in sourceBitmaps)
                bitmap.Dispose();
        }
    }

    private static void CheckBackgroundModes(AppSettings defaults, string runFolder, string sourcePath)
    {
        var settings = defaults.Copy();
        settings.OutputWidth = 640;
        settings.CanvasRatio = CanvasRatio.Wide16x9;
        settings.BackgroundColor1Hex = "#204D3C";
        settings.BackgroundColor2Hex = "#FEDCBA";

        settings.BackgroundMode = BackgroundMode.SolidColor;
        using var solid = BackdropRenderer.CreateSamplePreview(settings, 640);
        var expectedSolid = ColorTranslator.FromHtml(settings.BackgroundColor1Hex);
        Assert(solid.GetPixel(0, 0).ToArgb() == expectedSolid.ToArgb(), "Solid background uses the selected opaque color");
        solid.Save(Path.Combine(runFolder, "background-solid.png"), ImageFormat.Png);

        settings.BackgroundMode = BackgroundMode.CustomGradient;
        using var gradient = BackdropRenderer.CreateSamplePreview(settings, 640);
        var expectedEnd = ColorTranslator.FromHtml(settings.BackgroundColor2Hex);
        Assert(IsNearColor(gradient.GetPixel(0, 0), expectedSolid, 3) && IsNearColor(gradient.GetPixel(gradient.Width - 1, 0), expectedEnd, 3),
            "Custom horizontal gradient reaches both selected colors");

        settings.BackgroundMode = BackgroundMode.Pattern;
        settings.BackgroundPattern = BackgroundPattern.SoftGrain;
        using var grainFirst = BackdropRenderer.CreateSamplePreview(settings, 640);
        using var grainSecond = BackdropRenderer.CreateSamplePreview(settings, 640);
        Assert(CountPixelDifferences(grainFirst, grainSecond) == 0, "Soft-grain pattern is deterministic");
        Assert(CountPixelDifferences(solid, grainFirst) > 0, "Soft-grain pattern changes the selected base color");
        grainFirst.Save(Path.Combine(runFolder, "background-grain.png"), ImageFormat.Png);

        settings.BackgroundPattern = BackgroundPattern.Dots;
        using var dotsFirst = BackdropRenderer.CreateSamplePreview(settings, 640);
        using var dotsSecond = BackdropRenderer.CreateSamplePreview(settings, 640);
        Assert(CountPixelDifferences(dotsFirst, dotsSecond) == 0, "Dot pattern is deterministic");
        Assert(CountPixelDifferences(grainFirst, dotsFirst) > 0, "Dot and grain patterns render differently");
        dotsFirst.Save(Path.Combine(runFolder, "background-dots.png"), ImageFormat.Png);

        settings.BackgroundMode = BackgroundMode.SolidColor;
        var solidExport = BackdropRenderer.Generate([sourcePath], settings);
            using var exported = new Bitmap(solidExport);
        Assert(exported.GetPixel(0, 0).ToArgb() == expectedSolid.ToArgb(), "File export uses the same solid background renderer as the sample preview");
    }

    private static bool IsNearColor(Color actual, Color expected, int tolerance) =>
        Math.Abs(actual.R - expected.R) <= tolerance &&
        Math.Abs(actual.G - expected.G) <= tolerance &&
        Math.Abs(actual.B - expected.B) <= tolerance;

    private static int CountPixelDifferences(Bitmap first, Bitmap second)
    {
        Assert(first.Size == second.Size, "Background mode comparison images have matching dimensions");
        var differences = 0;
        for (var y = 0; y < first.Height; y++)
        for (var x = 0; x < first.Width; x++)
        {
            if (first.GetPixel(x, y).ToArgb() != second.GetPixel(x, y).ToArgb())
                differences++;
        }
        return differences;
    }

    private static void CaptureDialog(Form form, string path)
    {
        using var capture = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
        capture.Save(path, ImageFormat.Png);
    }

    private static void CaptureNativeForm(Form form, string path)
    {
        using var capture = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(capture);
        var hdc = graphics.GetHdc();
        bool captured;
        try
        {
            captured = PrintWindow(form.Handle, hdc, 2);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
        Assert(captured, $"Native window capture succeeds for {form.Text}");
        capture.Save(path, ImageFormat.Png);
    }

    private static void CaptureScreenForm(Form form, string path)
    {
        if (!GetWindowRect(form.Handle, out var bounds))
            throw new InvalidOperationException($"Could not read the screen bounds for {form.Text}.");
        var size = new Size(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        using var capture = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(capture))
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, size, CopyPixelOperation.SourceCopy);
        capture.Save(path, ImageFormat.Png);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    private static void CaptureForm(Form form, string path, Size clientSize)
    {
        form.ClientSize = clientSize;
        Application.DoEvents();
        using var capture = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
        capture.Save(path, ImageFormat.Png);
    }

    private static void AssertSidebarLabelSizing(MainForm form, string sizeName)
    {
        var settingLabels = Descendants(form).OfType<Label>().Where(label =>
            label.Parent is TableLayoutPanel field && field.RowCount == 2 && field.ColumnCount == 1 &&
            label.AccessibleName is "Canvas ratio" or "Layout" or "Size" or "Padding" or "Shadow").ToArray();
        Assert(settingLabels.Length == 5, $"All setting labels are present at {sizeName} size");
        foreach (var label in settingLabels)
        {
            var field = (TableLayoutPanel)label.Parent!;
            field.PerformLayout();
            var rows = field.GetRowHeights();
            var required = label.PreferredHeight + label.Margin.Vertical;
            Assert(rows[0] >= required,
                $"{label.Text} label row fits its preferred height at {sizeName} size ({rows[0]} >= {required})");
            var input = field.GetControlFromPosition(0, 1)!;
            Assert(rows[1] >= input.MinimumSize.Height,
                $"{label.Text} input row fits its minimum height at {sizeName} size");
        }

        foreach (var label in Descendants(form).OfType<Label>().Where(label => label.Text is "PREVIEW" or "COMPOSITION" or "IMAGES"))
        {
            var measured = TextRenderer.MeasureText(label.Text, label.Font).Height;
            Assert(label.Height >= measured,
                $"{label.Text} section heading fits its measured text height at {sizeName} size");
        }

        var hint = Descendants(form).OfType<Label>().Single(label => label.AccessibleName == "Image reorder instructions");
        var hintHeight = TextRenderer.MeasureText("Mg", hint.Font).Height;
        Assert(hint.Height >= hintHeight,
            $"Image reorder hint fits its measured text height at {sizeName} size");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static bool VisibleChildrenFit(Control parent) => parent.Controls.Cast<Control>()
        .Where(control => control.Visible)
        .All(control => control.Width > 0 && control.Height > 0 && control.Left >= 0 && control.Top >= 0 &&
            control.Right <= parent.ClientSize.Width && control.Bottom <= parent.ClientSize.Height);

    private static void AssertBackgroundHeaderSizing(MainForm form, string sizeName)
    {
        var button = GetField<Control>(form, "_backgroundButton");
        var mode = GetField<Control>(form, "_backgroundModeLabel");
        var badge = GetField<Label>(form, "_previewImageCount");
        var actions = button.Parent!;
        var heading = actions.Parent!;
        heading.PerformLayout();
        actions.PerformLayout();
        badge.PerformLayout();
        var requiredBadgeWidth = TextRenderer.MeasureText("9 IMAGES", badge.Font).Width;
        Assert(actions.Right <= heading.ClientSize.Width && VisibleChildrenFit(actions),
            $"Background button and mode fit inside the preview heading at {sizeName} size (heading {heading.ClientSize}, actions {actions.Bounds}, children {DescribeControls(actions)})");
        Assert(!badge.AutoSize && badge.Width >= requiredBadgeWidth && badge.Right <= heading.ClientSize.Width,
            $"Sample or image-count badge fits inside the preview heading at {sizeName} size ({badge.Width} >= {requiredBadgeWidth})");
        Assert(TextRenderer.MeasureText("Background", button.Font).Width <= button.ClientSize.Width,
            $"Background editor button label fits at {sizeName} size");
        Assert(mode.Text.Length > 0 && mode.Right <= actions.ClientSize.Width,
            $"Current background mode label fits at {sizeName} size");
    }

    private static string DescribeControls(Control parent) => string.Join(", ", parent.Controls.Cast<Control>()
        .Select(control => $"{control.GetType().Name}:{control.Visible}:{control.Bounds} within {parent.ClientSize}"));

    private static void AssertDarkControlChrome(MainForm form)
    {
        var failures = new List<string>();
        var moveUp = GetField<Button>(form, "_moveUp");
        var moveDown = GetField<Button>(form, "_moveDown");
        Assert(moveUp.Text == "Up" && moveDown.Text == "Down" &&
            TextRenderer.MeasureText(moveUp.Text, moveUp.Font).Width <= moveUp.ClientSize.Width &&
            TextRenderer.MeasureText(moveDown.Text, moveDown.Font).Width <= moveDown.ClientSize.Width &&
            moveUp.AccessibleName == "Move selected image up" && moveDown.AccessibleName == "Move selected image down",
            "Compact reorder buttons fit while keeping their full accessible names");

        var generate = GetField<Control>(form, "_generateButton");
        using (var bitmap = CaptureControl(generate))
        {
            var corner = bitmap.GetPixel(1, 1);
            CheckDarkPixel(failures, "Primary button has a rounded charcoal corner", corner);
        }

        var ratio = GetField<Control>(form, "_ratio");
        using (var bitmap = CaptureControl(ratio))
        {
            var arrowArea = bitmap.GetPixel(bitmap.Width - 8, bitmap.Height / 2);
            CheckDarkPixel(failures, "Canvas ratio dropdown arrow area stays charcoal", arrowArea);
            CheckDarkPixel(failures, "Canvas ratio field has no bright square corners", bitmap.GetPixel(1, 1));
        }

        var edge = GetField<Control>(form, "_outputWidth");
        using (var bitmap = CaptureControl(edge))
        {
            var arrowArea = bitmap.GetPixel(bitmap.Width - 8, bitmap.Height / 4);
            CheckDarkPixel(failures, "Long edge spinner area stays charcoal", arrowArea);
        }

        var layout = GetField<Control>(form, "_layout");
        var selectedIndex = layout.GetType().GetProperty("SelectedIndex")
            ?? throw new InvalidOperationException("Self-check failed: layout choice exposes no SelectedIndex.");
        var indexChanged = layout.GetType().GetEvent("SelectedIndexChanged")
            ?? throw new InvalidOperationException("Self-check failed: layout choice exposes no SelectedIndexChanged event.");
        var items = layout.GetType().GetProperty("Items")?.GetValue(layout);
        var count = items?.GetType().GetProperty("Count")?.GetValue(items);
        Assert(count is int itemCount && itemCount == 3, "Layout choice has Auto, Row, and Grid options");
        var changes = 0;
        EventHandler handler = (_, _) => changes++;
        indexChanged.AddEventHandler(layout, handler);
        var originalIndex = (int)(selectedIndex.GetValue(layout) ?? -1);
        try
        {
            selectedIndex.SetValue(layout, 1);
            selectedIndex.SetValue(layout, originalIndex);
        }
        finally
        {
            indexChanged.RemoveEventHandler(layout, handler);
        }
        Assert(changes == 2, "Layout choice raises SelectedIndexChanged for both selection updates");

        var padding = GetField<NumericUpDown>(form, "_padding");
        Assert(padding.Minimum == 0 && padding.Maximum == 25, "Padding editor preserves its value bounds");
        var edgeEditor = GetField<DarkNumericUpDown>(form, "_outputWidth");
        var shadowEditor = GetField<DarkNumericUpDown>(form, "_shadow");
        var darkPaddingEditor = padding as DarkNumericUpDown
            ?? throw new InvalidOperationException("Self-check failed: padding editor is not dark-themed.");
        var spinnerEditors = new[] { edgeEditor, darkPaddingEditor, shadowEditor };
        foreach (var (name, editor) in new[]
        {
            ("Long edge", edgeEditor),
            ("Padding", darkPaddingEditor),
            ("Shadow", shadowEditor)
        })
        {
            var attached = editor.SpinnerHookMatchesCurrentChild;
            var painted = editor.SpinnerPaintCount > 0;
            Assert(attached && painted,
                $"{name} spinner hook follows its current native child and paints (hook {editor.SpinnerWindowHandle}, attached {attached}, paints {editor.SpinnerPaintCount})");
        }
        var valueChanges = 0;
        padding.ValueChanged += (_, _) => valueChanges++;
        var originalValue = padding.Value;
        padding.Value = originalValue + 1;
        padding.Value = originalValue;
        padding.UpButton();
        Assert(padding.Value == originalValue + padding.Increment, "Native plus control increments within the configured bounds");
        padding.DownButton();
        Assert(padding.Value == originalValue && valueChanges == 4, "Native minus control restores the value and raises ValueChanged");

        if (failures.Count != 0)
            throw new InvalidOperationException("Self-check failed: " + string.Join("; ", failures));
    }

    private static void CheckDarkPixel(List<string> failures, string message, Color color)
    {
        if (Math.Max(color.R, Math.Max(color.G, color.B)) >= 120)
            failures.Add($"{message} (sample #{color.R:X2}{color.G:X2}{color.B:X2})");
    }

    private static T GetField<T>(object target, string name) where T : Control =>
        (T)(typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException($"Self-check failed: control field {name} was not found."));

    private static Bitmap CaptureControl(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    private static void AssertImageSize(string path, int width, int height)
    {
        using var image = Image.FromFile(path);
        Assert(image.Width == width && image.Height == height, $"Output dimensions are {width}x{height}");
    }

    private static void ExpectThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Self-check failed: {message}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Self-check failed: {message}.");
    }
}
