using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Backdrop;

internal static class BackdropRenderer
{
    public const int MaximumImages = 9;
    public const int MaximumPreviewLongEdge = 1000;
    private const long MaximumImagePixels = 40_000_000;
    private const long MaximumSelectionPixels = 80_000_000;
    private const double MinimumAutoCompositionRatio = 0.5;
    private const double MaximumAutoCompositionRatio = 3;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };

    public static bool IsSupportedImagePath(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private sealed record ImageInfo(string Path, int Orientation);
    private sealed record LoadedImage(string Path, Bitmap Bitmap);
    private sealed class ColorBucket
    {
        public long Count;
        public long Red;
        public long Green;
        public long Blue;
    }

    public static Bitmap RenderFiles(IReadOnlyList<string> paths, AppSettings settings, CancellationToken cancellationToken = default)
    {
        ValidateRequest(paths, settings);
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = LoadImages(paths, cancellationToken);
        try
        {
            return RenderBitmaps(loaded.Select(item => item.Bitmap).ToArray(), settings, cancellationToken);
        }
        finally
        {
            foreach (var item in loaded)
                item.Bitmap.Dispose();
        }
    }

    public static Bitmap RenderPreviewFiles(IReadOnlyList<string> paths, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var previewSettings = settings.Copy();
        previewSettings.OutputWidth = Math.Min(previewSettings.OutputWidth, MaximumPreviewLongEdge);
        return RenderFiles(paths, previewSettings, cancellationToken);
    }

    public static string Generate(IReadOnlyList<string> paths, AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var image = RenderFiles(paths, settings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var encoded = new MemoryStream();
        image.Save(encoded, ImageFormat.Png);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = encoded.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var folder = Path.GetDirectoryName(Path.GetFullPath(paths[0]))!;
        var baseName = paths.Count == 1
            ? $"{Path.GetFileNameWithoutExtension(paths[0])}-backdrop"
            : "backdrop-composition";

        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = suffix == 1 ? $"{baseName}.png" : $"{baseName}-{suffix}.png";
            var destination = Path.Combine(folder, name);
            if (File.Exists(destination))
                continue;
            var temporary = Path.Combine(folder, $".backdrop-{Guid.NewGuid():N}.tmp");
            var temporaryCreated = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    temporaryCreated = true;
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var count = Math.Min(64 * 1024, bytes.Length - offset);
                        stream.Write(bytes, offset, count);
                        offset += count;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Flush(flushToDisk: true);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporary, destination);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    continue;
                }
                return destination;
            }
            finally
            {
                if (temporaryCreated)
                    try { File.Delete(temporary); } catch { }
            }
        }
    }

    public static Bitmap CreateSamplePreview(AppSettings settings)
    {
        using var first = CreateSampleAppScreen(400, 800, 0);
        using var second = CreateSampleAppScreen(400, 800, 1);
        using var third = CreateSampleAppScreen(400, 800, 2);
        return RenderBitmaps([first, second, third], settings);
    }

    public static Bitmap CreateSamplePreview(AppSettings settings, int maximumLongEdge)
    {
        var previewSettings = settings.Copy();
        previewSettings.OutputWidth = Math.Min(previewSettings.OutputWidth, maximumLongEdge);
        return CreateSamplePreview(previewSettings);
    }

    public static List<Bitmap> CreateSampleInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        var images = new List<Bitmap>
        {
            CreateSampleImage(420, 680, Color.FromArgb(39, 86, 117), Color.FromArgb(237, 182, 106), 0),
            CreateSampleImage(420, 560, Color.FromArgb(55, 104, 91), Color.FromArgb(225, 190, 125), 1),
            CreateSampleImage(680, 420, Color.FromArgb(75, 75, 112), Color.FromArgb(240, 156, 117), 2),
            CreateSampleImage(440, 700, Color.FromArgb(83, 85, 111), Color.FromArgb(234, 160, 111), 1)
        };
        var names = new[] { "portrait one.png", "portrait two.png", "landscape.png", "portrait three café.png" };
        try
        {
            for (var i = 0; i < images.Count; i++)
                images[i].Save(Path.Combine(directory, names[i]), ImageFormat.Png);
            return images;
        }
        catch
        {
            foreach (var image in images)
                image.Dispose();
            throw;
        }
    }

    public static IReadOnlyList<RectangleF> GetImageBounds(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        ValidateGeometry(imageSizes, settings);
        return GetImageBounds(imageSizes, settings, GetCanvasDimensions(imageSizes, settings));
    }

    public static Size GetCanvasDimensions(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        ValidateGeometry(imageSizes, settings);
        var layout = ResolveLayout(imageSizes, settings.Layout);
        var ratio = settings.CanvasRatio switch
        {
            CanvasRatio.Wide16x9 => 16d / 9,
            CanvasRatio.Square1x1 => 1,
            CanvasRatio.Portrait4x5 => 4d / 5,
            _ when imageSizes.Count == 1 => imageSizes[0].Width / (double)imageSizes[0].Height,
            _ when layout == CompositionLayout.Row => NaturalRowRatio(imageSizes),
            _ => NaturalGridRatio(imageSizes)
        };
        var longEdge = settings.OutputWidth;
        return ratio >= 1
            ? new Size(longEdge, Math.Max(1, (int)Math.Round(longEdge / ratio)))
            : new Size(Math.Max(1, (int)Math.Round(longEdge * ratio)), longEdge);
    }

    private static IReadOnlyList<RectangleF> GetImageBounds(IReadOnlyList<Size> imageSizes, AppSettings settings, Size canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;
        var paddingX = width * settings.PaddingPercent / 100f;
        var paddingY = height * settings.PaddingPercent / 100f;
        var content = new RectangleF(paddingX, paddingY, width - 2 * paddingX, height - 2 * paddingY);

        if (imageSizes.Count == 1)
        {
            var box = Fit(imageSizes[0], content.Width, content.Height);
            return [Center(box, content)];
        }

        if (ResolveLayout(imageSizes, settings.Layout) == CompositionLayout.Row)
        {
            var totalRatio = imageSizes.Sum(size => size.Width / (float)size.Height);
            var gapRatio = 0.04f;
            var imageHeight = Math.Min(content.Height, content.Width / (totalRatio + gapRatio * (imageSizes.Count - 1)));
            var gap = imageHeight * gapRatio;
            var totalWidth = totalRatio * imageHeight + gap * (imageSizes.Count - 1);
            var left = content.Left + (content.Width - totalWidth) / 2;
            var bounds = new List<RectangleF>(imageSizes.Count);
            foreach (var size in imageSizes)
            {
                var imageWidth = size.Width / (float)size.Height * imageHeight;
                bounds.Add(new RectangleF(left, content.Top + (content.Height - imageHeight) / 2, imageWidth, imageHeight));
                left += imageWidth + gap;
            }
            return bounds;
        }

        var columns = imageSizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(imageSizes.Count / (double)columns);
        var gapX = content.Width * 0.035f;
        var gapY = content.Height * 0.05f;
        var cellWidth = (content.Width - gapX * (columns - 1)) / columns;
        var cellHeight = (content.Height - gapY * (rows - 1)) / rows;
        var result = new List<RectangleF>(imageSizes.Count);
        for (var i = 0; i < imageSizes.Count; i++)
        {
            var row = i / columns;
            var rowItemCount = Math.Min(columns, imageSizes.Count - row * columns);
            var rowWidth = rowItemCount * cellWidth + (rowItemCount - 1) * gapX;
            var cell = new RectangleF(
                content.Left + (content.Width - rowWidth) / 2 + (i % columns) * (cellWidth + gapX),
                content.Top + row * (cellHeight + gapY),
                cellWidth,
                cellHeight);
            result.Add(Center(Fit(imageSizes[i], cell.Width, cell.Height), cell));
        }
        return result;
    }

    private static void ValidateGeometry(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        if (imageSizes.Count is < 1 or > MaximumImages)
            throw new ArgumentOutOfRangeException(nameof(imageSizes), $"Choose between one and {MaximumImages} images.");
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (imageSizes.Any(size => size.Width <= 0 || size.Height <= 0))
            throw new ArgumentException("Image dimensions must be positive.", nameof(imageSizes));
    }

    private static CompositionLayout ResolveLayout(IReadOnlyList<Size> imageSizes, CompositionLayout layout) =>
        imageSizes.Count == 1 ? CompositionLayout.Row : layout == CompositionLayout.Auto
            ? imageSizes.Count <= 4 && imageSizes.All(size => size.Height > size.Width) ? CompositionLayout.Row : CompositionLayout.Grid
            : layout;

    private static double NaturalRowRatio(IReadOnlyList<Size> imageSizes) => Math.Clamp(
        imageSizes.Sum(size => size.Width / (double)size.Height) + 0.04 * (imageSizes.Count - 1),
        MinimumAutoCompositionRatio,
        MaximumAutoCompositionRatio);

    private static double NaturalGridRatio(IReadOnlyList<Size> imageSizes)
    {
        var columns = imageSizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(imageSizes.Count / (double)columns);
        var averageImageRatio = imageSizes.Average(size => size.Width / (double)size.Height);
        return Math.Clamp(columns * averageImageRatio / rows, MinimumAutoCompositionRatio, MaximumAutoCompositionRatio);
    }

    private static void ValidateRequest(IReadOnlyList<string> paths, AppSettings settings)
    {
        if (paths.Count is < 1 or > MaximumImages)
            throw new ArgumentOutOfRangeException(nameof(paths), $"Choose between one and {MaximumImages} images.");
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    private static List<LoadedImage> LoadImages(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var infos = new List<ImageInfo>(paths.Count);
        long totalPixels = 0;
        foreach (var candidate in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(candidate);
            if (!File.Exists(path))
                throw new FileNotFoundException($"The selected image was not found: {candidate}", path);
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a supported image. Choose PNG, JPEG, BMP, GIF, or TIFF files.");

            try
            {
                using var source = Image.FromFile(path);
                var pixels = (long)source.Width * source.Height;
                if (pixels > MaximumImagePixels)
                    throw new InvalidDataException($"'{Path.GetFileName(path)}' is larger than the 40-megapixel limit.");
                totalPixels += pixels;
                if (totalPixels > MaximumSelectionPixels)
                    throw new InvalidDataException("The selected images exceed the 80-megapixel total limit.");
                infos.Add(new ImageInfo(path, ReadOrientation(source)));
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException or IOException)
            {
                throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be decoded as an image. Check the file and try again.", ex);
            }
        }

        var loaded = new List<LoadedImage>(infos.Count);
        try
        {
            foreach (var info in infos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = Image.FromFile(info.Path);
                Bitmap? bitmap = null;
                try
                {
                    bitmap = new Bitmap(source);
                    ApplyOrientation(bitmap, info.Orientation);
                    loaded.Add(new LoadedImage(info.Path, bitmap));
                    bitmap = null;
                }
                finally
                {
                    bitmap?.Dispose();
                }
            }
            return loaded;
        }
        catch (Exception ex)
        {
            foreach (var image in loaded)
                image.Bitmap.Dispose();
            if (ex is ArgumentException or OutOfMemoryException or ExternalException or IOException)
                throw new InvalidDataException("Backdrop could not load the selected images. Check that they are valid and try again.", ex);
            throw;
        }
    }

    private static int ReadOrientation(Image image)
    {
        const int OrientationId = 0x0112;
        try
        {
            var value = image.GetPropertyItem(OrientationId)?.Value;
            return value is { Length: >= 2 } ? BitConverter.ToUInt16(value, 0) : 1;
        }
        catch (ArgumentException)
        {
            return 1;
        }
        catch (ExternalException)
        {
            return 1;
        }
    }

    private static void ApplyOrientation(Bitmap bitmap, int orientation)
    {
        var transform = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        if (transform != RotateFlipType.RotateNoneFlipNone)
            bitmap.RotateFlip(transform);
    }

    private static Bitmap RenderBitmaps(IReadOnlyList<Bitmap> images, AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
        cancellationToken.ThrowIfCancellationRequested();
        var sizes = images.Select(image => image.Size).ToArray();
        var canvas = GetCanvasDimensions(sizes, settings);
        var bounds = GetImageBounds(sizes, settings, canvas);
        var output = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(output);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            DrawBackground(graphics, output.Size, images, settings, cancellationToken);

            for (var i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DrawShadow(graphics, bounds[i], settings.ShadowStrength, output.Width);
                graphics.DrawImage(images[i], bounds[i]);
            }
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static void DrawBackground(Graphics graphics, Size size, IReadOnlyList<Bitmap> images, AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = ColorTranslator.FromHtml(settings.BackgroundColor1Hex);
        switch (settings.BackgroundMode)
        {
            case BackgroundMode.AutoGradient:
                var sampled = SampleDominantColors(images);
                FillGradient(graphics, size, sampled.First, sampled.Second);
                break;
            case BackgroundMode.SolidColor:
                graphics.Clear(first);
                break;
            case BackgroundMode.CustomGradient:
                FillGradient(graphics, size, first, ColorTranslator.FromHtml(settings.BackgroundColor2Hex));
                break;
            case BackgroundMode.Pattern:
                graphics.Clear(first);
                using (var tile = CreatePatternTile(size, first, settings.BackgroundPattern, cancellationToken))
                using (var brush = new TextureBrush(tile, WrapMode.Tile))
                    graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), "The background mode is invalid.");
        }
    }

    private static void FillGradient(Graphics graphics, Size size, Color first, Color second)
    {
        using var gradient = new LinearGradientBrush(
            new Rectangle(0, 0, size.Width, size.Height), first, second, LinearGradientMode.Horizontal);
        graphics.FillRectangle(gradient, 0, 0, size.Width, size.Height);
    }

    private static Bitmap CreatePatternTile(Size canvasSize, Color baseColor, BackgroundPattern pattern, CancellationToken cancellationToken)
    {
        if (pattern == BackgroundPattern.SoftGrain)
            return CreateGrainTile(baseColor, cancellationToken);
        if (pattern != BackgroundPattern.Dots)
            throw new ArgumentOutOfRangeException(nameof(pattern));

        var longEdge = Math.Max(canvasSize.Width, canvasSize.Height);
        var spacing = Math.Clamp(longEdge / 50, 12, 72);
        var tileSize = spacing * 4;
        var radius = Math.Clamp(longEdge / 1500f, 0.8f, 2.5f);
        var tile = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(tile);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            var contrast = baseColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
            using var brush = new SolidBrush(Color.FromArgb(18, contrast));
            for (var y = spacing / 2f; y < tileSize; y += spacing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = spacing / 2f; x < tileSize; x += spacing)
                    graphics.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            }
            return tile;
        }
        catch
        {
            tile.Dispose();
            throw;
        }
    }

    private static Bitmap CreateGrainTile(Color baseColor, CancellationToken cancellationToken)
    {
        const int size = 128;
        var tile = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var data = tile.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var complete = false;
        try
        {
            var row = new byte[Math.Abs(data.Stride)];
            var random = new Random(0x5EED);
            static byte Shade(byte component, int offset) => (byte)Math.Clamp(
                offset >= 0 ? component + (255 - component) * offset / 64 : component + component * offset / 64,
                0, 255);

            for (var y = 0; y < size; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < size; x++)
                {
                    var offset = random.Next(-64, 65);
                    var pixel = x * 4;
                    row[pixel] = Shade(baseColor.B, offset);
                    row[pixel + 1] = Shade(baseColor.G, offset);
                    row[pixel + 2] = Shade(baseColor.R, offset);
                    row[pixel + 3] = (byte)random.Next(7, 17);
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }
            complete = true;
        }
        finally
        {
            tile.UnlockBits(data);
            if (!complete)
                tile.Dispose();
        }
        return tile;
    }

    private static (Color First, Color Second) SampleDominantColors(IReadOnlyList<Bitmap> images)
    {
        var buckets = new Dictionary<int, ColorBucket>();
        foreach (var image in images)
        {
            const int samplesPerAxis = 24;
            for (var y = 0; y < samplesPerAxis; y++)
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var color = image.GetPixel(
                    Math.Min(image.Width - 1, x * image.Width / samplesPerAxis),
                    Math.Min(image.Height - 1, y * image.Height / samplesPerAxis));
                if (color.A < 24)
                    continue;

                var key = (color.R >> 3) << 10 | (color.G >> 3) << 5 | color.B >> 3;
                if (!buckets.TryGetValue(key, out var bucket))
                    buckets[key] = bucket = new ColorBucket();
                bucket.Count++;
                bucket.Red += color.R;
                bucket.Green += color.G;
                bucket.Blue += color.B;
            }
        }

        var dominant = buckets.OrderByDescending(pair => pair.Value.Count)
            .Select(pair => (Color: Average(pair.Value), pair.Value.Count))
            .ToArray();
        if (dominant.Length == 0)
            return (Color.FromArgb(43, 54, 75), Color.FromArgb(75, 82, 104));

        var colored = dominant.Where(item => item.Color.GetSaturation() >= 0.2f && item.Color.GetBrightness() >= 0.08f).ToArray();
        var candidates = colored.Length > 0 && colored[0].Count >= dominant.Sum(item => item.Count) * 0.05
            ? colored
            : dominant;
        var first = candidates[0].Color;
        var secondBucket = candidates.Skip(1).Select(item => item.Color)
            .FirstOrDefault(color => ColorDistance(first, color) >= 50);
        if (secondBucket.IsEmpty)
            return (first, Blend(first, Color.Black, 0.35f));
        return (first, secondBucket);
    }

    private static Color Average(ColorBucket bucket) => Color.FromArgb(
        (int)(bucket.Red / bucket.Count),
        (int)(bucket.Green / bucket.Count),
        (int)(bucket.Blue / bucket.Count));

    private static double ColorDistance(Color first, Color second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return Math.Sqrt(red * red + green * green + blue * blue);
    }

    private static void DrawShadow(Graphics graphics, RectangleF bounds, double strength, int canvasWidth)
    {
        if (strength <= 0)
            return;

        var blur = Math.Clamp(canvasWidth * 0.012f, 8, 32);
        var offset = blur * 0.2f;
        var padding = (int)Math.Ceiling(blur + offset);
        var scale = Math.Min(1f, 384f / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)Math.Ceiling((bounds.Width + padding * 2) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((bounds.Height + padding * 2) * scale));
        using var mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var maskGraphics = Graphics.FromImage(mask))
        {
            maskGraphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(Math.Clamp((int)Math.Round(strength * 180), 0, 255), Color.Black));
            maskGraphics.FillRectangle(brush, padding * scale, (padding + offset) * scale, bounds.Width * scale, bounds.Height * scale);
        }

        BlurShadowMask(mask, blur * 0.45f * scale);
        graphics.DrawImage(mask,
            new RectangleF(bounds.X - padding, bounds.Y - padding, bounds.Width + padding * 2, bounds.Height + padding * 2),
            new Rectangle(0, 0, width, height),
            GraphicsUnit.Pixel);
    }

    private static void BlurShadowMask(Bitmap mask, float sigma)
    {
        if (sigma < 0.5f)
            return;

        var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = Enumerable.Range(-radius, radius * 2 + 1)
            .Select(offset => (float)Math.Exp(-(offset * offset) / (2 * sigma * sigma)))
            .ToArray();
        var weight = kernel.Sum();
        for (var i = 0; i < kernel.Length; i++)
            kernel[i] /= weight;

        var data = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var bytes = new byte[stride * mask.Height];
            var row = new byte[stride];
            for (var y = 0; y < mask.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, stride);
                Buffer.BlockCopy(row, 0, bytes, y * stride, stride);
            }

            var source = new byte[mask.Width * mask.Height];
            var horizontal = new byte[source.Length];
            var blurred = new byte[source.Length];
            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
                source[y * mask.Width + x] = bytes[y * stride + x * 4 + 3];

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleX = x + k;
                    if ((uint)sampleX < (uint)mask.Width)
                        value += source[y * mask.Width + sampleX] * kernel[k + radius];
                }
                horizontal[y * mask.Width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleY = y + k;
                    if ((uint)sampleY < (uint)mask.Height)
                        value += horizontal[sampleY * mask.Width + x] * kernel[k + radius];
                }
                blurred[y * mask.Width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var pixel = y * stride + x * 4;
                bytes[pixel] = bytes[pixel + 1] = bytes[pixel + 2] = 0;
                bytes[pixel + 3] = blurred[y * mask.Width + x];
            }

            for (var y = 0; y < mask.Height; y++)
            {
                Buffer.BlockCopy(bytes, y * stride, row, 0, stride);
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), stride);
            }
        }
        finally
        {
            mask.UnlockBits(data);
        }
    }

    private static Bitmap CreateSampleImage(int width, int height, Color sky, Color accent, int variation)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var skyBrush = new LinearGradientBrush(new Rectangle(0, 0, width, height), sky, Blend(sky, Color.Black, 0.45f), LinearGradientMode.Vertical))
            graphics.FillRectangle(skyBrush, 0, 0, width, height);
        using (var sunBrush = new SolidBrush(Color.FromArgb(215, accent)))
            graphics.FillEllipse(sunBrush, width * 0.59f, height * (0.18f + variation * 0.02f), width * 0.17f, width * 0.17f);
        using var far = new SolidBrush(Blend(sky, accent, 0.22f));
        using var near = new SolidBrush(Blend(sky, Color.FromArgb(23, 35, 48), 0.45f));
        graphics.FillPolygon(far,
        [
            new PointF(0, height * 0.67f), new PointF(width * 0.28f, height * 0.36f),
            new PointF(width * 0.5f, height * 0.65f), new PointF(width * 0.72f, height * 0.4f),
            new PointF(width, height * 0.69f), new PointF(width, height), new PointF(0, height)
        ]);
        graphics.FillPolygon(near,
        [
            new PointF(0, height * 0.78f), new PointF(width * 0.32f, height * 0.58f),
            new PointF(width * 0.57f, height * 0.82f), new PointF(width * 0.84f, height * 0.61f),
            new PointF(width, height * 0.76f), new PointF(width, height), new PointF(0, height)
        ]);
        return image;
    }

    private static Bitmap CreateSampleAppScreen(int width, int height, int page)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.FromArgb(246, 243, 237));

        var scale = width / 400f;
        float S(float value) => value * scale;
        var ink = Color.FromArgb(48, 48, 46);
        var secondary = Color.FromArgb(118, 116, 111);
        var line = Color.FromArgb(222, 217, 208);
        var card = Color.FromArgb(252, 250, 246);
        var peach = Color.FromArgb(215, 157, 132);
        var lilac = Color.FromArgb(174, 162, 194);
        var sage = Color.FromArgb(143, 163, 143);
        var pageTitle = page switch { 0 => "Overview", 1 => "Activity", _ => "Spending" };
        var subtitle = page switch { 0 => "A clear view of your money", 1 => "Recent transactions", _ => "Monthly plan details" };

        using (var outerBorder = new Pen(line, Math.Max(1, S(1))))
            graphics.DrawRectangle(outerBorder, S(0.5f), S(0.5f), width - S(1), height - S(1));

        DrawSampleText(graphics, "9:41", S(24), S(17), S(12), ink, FontStyle.Bold);
        DrawStatusGlyphs(graphics, S(328), S(23), S(1), secondary);
        DrawSampleText(graphics, "YOUR MONEY", S(24), S(55), S(10), secondary, FontStyle.Bold);
        DrawSampleText(graphics, pageTitle, S(24), S(74), S(28), ink, FontStyle.Bold);
        DrawSampleText(graphics, subtitle, S(24), S(111), S(14), secondary);

        if (page == 0)
            DrawOverviewPage(graphics, S, card, ink, secondary, line, peach, lilac, sage);
        else if (page == 1)
            DrawActivityPage(graphics, S, card, ink, secondary, line, peach, lilac, sage);
        else
            DrawSpendingPage(graphics, S, card, ink, secondary, line, peach, lilac, sage);

        DrawBottomNavigation(graphics, S, ink, secondary, peach, page);
        return image;
    }

    private static void DrawOverviewPage(Graphics graphics, Func<float, float> s, Color card, Color ink, Color secondary, Color line, Color peach, Color lilac, Color sage)
    {
        FillSampleCard(graphics, s, new RectangleF(20, 153, 360, 240), Color.FromArgb(246, 232, 220), line);
        DrawSampleText(graphics, "TOTAL BALANCE", s(38), s(173), s(10), secondary, FontStyle.Bold);
        DrawSampleText(graphics, "$12,480.50", s(38), s(194), s(30), ink, FontStyle.Bold);
        FillSamplePill(graphics, s, new RectangleF(38, 239, 91, 28), Color.FromArgb(243, 231, 222));
        DrawSampleText(graphics, "+ 4.8%", s(50), s(245), s(12), Color.FromArgb(146, 100, 77), FontStyle.Bold);
        DrawSampleText(graphics, "this month", s(139), s(246), s(12), secondary);
        DrawSampleText(graphics, "Balance trend", s(38), s(291), s(12), secondary, FontStyle.Bold);
        DrawChart(graphics, s, new RectangleF(38, 318, 324, 52), peach, line, withFill: true);

        FillSampleCard(graphics, s, new RectangleF(20, 411, 360, 252), card, line);
        DrawSampleText(graphics, "THIS WEEK", s(38), s(431), s(10), secondary, FontStyle.Bold);
        DrawSampleText(graphics, "$684.20", s(38), s(450), s(24), ink, FontStyle.Bold);
        DrawSampleText(graphics, "Spending", s(38), s(489), s(12), secondary);
        DrawWeekBars(graphics, s, new RectangleF(40, 531, 318, 79), [peach, lilac, sage, peach, lilac, sage, peach]);
        DrawSampleText(graphics, "M     T     W     T     F     S     S", s(42), s(619), s(10), secondary);
    }

    private static void DrawActivityPage(Graphics graphics, Func<float, float> s, Color card, Color ink, Color secondary, Color line, Color peach, Color lilac, Color sage)
    {
        FillSampleCard(graphics, s, new RectangleF(20, 153, 360, 151), Color.FromArgb(235, 228, 240), line);
        DrawSampleText(graphics, "MARCH TOTAL", s(38), s(173), s(10), secondary, FontStyle.Bold);
        DrawSampleText(graphics, "$842.60", s(38), s(194), s(28), ink, FontStyle.Bold);
        DrawChart(graphics, s, new RectangleF(218, 195, 138, 69), lilac, line, withFill: true);
        DrawSampleText(graphics, "12 transactions", s(38), s(252), s(12), secondary);

        FillSampleCard(graphics, s, new RectangleF(20, 322, 360, 341), card, line);
        DrawSampleText(graphics, "RECENT", s(38), s(342), s(10), secondary, FontStyle.Bold);
        DrawTransaction(graphics, s, 373, "Market & Co.", "Groceries · Today", "−$58.40", peach, ink, secondary, line);
        DrawTransaction(graphics, s, 441, "Northside Cafe", "Coffee · Yesterday", "−$12.80", lilac, ink, secondary, line);
        DrawTransaction(graphics, s, 509, "Monthly pay", "Income · Mar 12", "+$2,400", sage, ink, secondary, line);
        DrawTransaction(graphics, s, 577, "City transit", "Travel · Mar 11", "−$24.00", peach, ink, secondary, line, separator: false);
    }

    private static void DrawSpendingPage(Graphics graphics, Func<float, float> s, Color card, Color ink, Color secondary, Color line, Color peach, Color lilac, Color sage)
    {
        FillSampleCard(graphics, s, new RectangleF(20, 153, 360, 225), Color.FromArgb(246, 232, 220), line);
        DrawSampleText(graphics, "HOME & BILLS", s(38), s(173), s(10), secondary, FontStyle.Bold);
        DrawSampleText(graphics, "$420", s(38), s(194), s(30), ink, FontStyle.Bold);
        DrawSampleText(graphics, "of $600 monthly plan", s(38), s(235), s(12), secondary);
        FillSamplePill(graphics, s, new RectangleF(38, 265, 324, 10), Color.FromArgb(235, 230, 222));
        FillSamplePill(graphics, s, new RectangleF(38, 265, 227, 10), peach);
        DrawSampleText(graphics, "70% used", s(38), s(289), s(11), secondary);
        DrawSampleText(graphics, "$180 left", s(280), s(289), s(11), secondary, alignment: StringAlignment.Far, width: s(82));
        DrawChart(graphics, s, new RectangleF(38, 320, 324, 40), lilac, line, withFill: false);

        FillSampleCard(graphics, s, new RectangleF(20, 397, 360, 266), card, line);
        DrawSampleText(graphics, "PLAN BREAKDOWN", s(38), s(417), s(10), secondary, FontStyle.Bold);
        DrawBreakdownRow(graphics, s, 455, "Housing", "$320", peach, ink, secondary, line);
        DrawBreakdownRow(graphics, s, 511, "Utilities", "$68", lilac, ink, secondary, line);
        DrawBreakdownRow(graphics, s, 567, "Internet", "$32", sage, ink, secondary, line, separator: false);
        DrawSampleText(graphics, "Updated just now", s(38), s(629), s(10), secondary);
    }

    private static void DrawStatusGlyphs(Graphics graphics, float x, float y, float scale, Color color)
    {
        using var pen = new Pen(color, Math.Max(1, scale * 1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (var i = 0; i < 4; i++)
            graphics.DrawLine(pen, x + i * 4 * scale, y, x + i * 4 * scale, y - (3 + i * 2) * scale);
        graphics.DrawArc(pen, x + 26 * scale, y - 8 * scale, 15 * scale, 13 * scale, 215, 110);
        graphics.DrawArc(pen, x + 30 * scale, y - 4 * scale, 7 * scale, 7 * scale, 220, 100);
        using var battery = new Pen(color, Math.Max(1, scale));
        graphics.DrawRectangle(battery, x + 48 * scale, y - 8 * scale, 22 * scale, 12 * scale);
        using var charge = new SolidBrush(color);
        graphics.FillRectangle(charge, x + 70 * scale, y - 5 * scale, 2 * scale, 6 * scale);
        graphics.FillRectangle(charge, x + 51 * scale, y - 5 * scale, 13 * scale, 6 * scale);
    }

    private static void DrawBottomNavigation(Graphics graphics, Func<float, float> s, Color ink, Color secondary, Color peach, int selectedPage)
    {
        using var pen = new Pen(Color.FromArgb(225, 220, 212), s(1));
        graphics.DrawLine(pen, s(20), s(698), s(380), s(698));
        var labels = new[] { "Home", "Activity", "Plan" };
        for (var i = 0; i < labels.Length; i++)
        {
            var center = s(80 + i * 120);
            var color = i == selectedPage ? ink : secondary;
            using var glyph = new Pen(color, s(1.7f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (i == 0)
            {
                graphics.DrawLine(glyph, center - s(8), s(723), center, s(715));
                graphics.DrawLine(glyph, center, s(715), center + s(8), s(723));
                graphics.DrawRectangle(glyph, center - s(6), s(722), s(12), s(10));
            }
            else if (i == 1)
            {
                graphics.DrawEllipse(glyph, center - s(7), s(715), s(14), s(14));
                graphics.DrawLine(glyph, center, s(718), center, s(726));
                graphics.DrawLine(glyph, center - s(4), s(722), center + s(3), s(722));
            }
            else
            {
                graphics.DrawRectangle(glyph, center - s(8), s(715), s(16), s(14));
                graphics.DrawLine(glyph, center - s(4), s(719), center + s(4), s(719));
                graphics.DrawLine(glyph, center - s(4), s(724), center + s(4), s(724));
            }
            DrawSampleText(graphics, labels[i], center - s(36), s(737), s(10), color, i == selectedPage ? FontStyle.Bold : FontStyle.Regular, StringAlignment.Center, s(72));
            if (i == selectedPage)
            {
                using var mark = new SolidBrush(peach);
                graphics.FillEllipse(mark, center - s(2), s(758), s(4), s(4));
            }
        }
    }

    private static void FillSampleCard(Graphics graphics, Func<float, float> s, RectangleF bounds, Color fill, Color border)
    {
        bounds = Scale(bounds, s);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, s(14));
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, s(1));
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
    }

    private static void FillSamplePill(Graphics graphics, Func<float, float> s, RectangleF bounds, Color fill)
    {
        bounds = Scale(bounds, s);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, bounds.Height / 2);
        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);
    }

    private static void DrawTransaction(Graphics graphics, Func<float, float> s, float top, string title, string detail, string amount, Color color, Color ink, Color secondary, Color line, bool separator = true)
    {
        using (var dot = new SolidBrush(Color.FromArgb(62, color)))
            graphics.FillEllipse(dot, s(38), s(top + 8), s(38), s(38));
        using (var mark = new Pen(color, s(2)))
            graphics.DrawLine(mark, s(49), s(top + 27), s(65), s(top + 27));
        DrawSampleText(graphics, title, s(88), s(top + 3), s(13), ink, FontStyle.Bold);
        DrawSampleText(graphics, detail, s(88), s(top + 24), s(11), secondary);
        DrawSampleText(graphics, amount, s(206), s(top + 10), s(12), ink, FontStyle.Bold, StringAlignment.Far, s(154));
        if (separator)
        {
            using var pen = new Pen(line, s(1));
            graphics.DrawLine(pen, s(88), s(top + 60), s(362), s(top + 60));
        }
    }

    private static void DrawBreakdownRow(Graphics graphics, Func<float, float> s, float top, string title, string amount, Color color, Color ink, Color secondary, Color line, bool separator = true)
    {
        using (var dot = new SolidBrush(color))
            graphics.FillEllipse(dot, s(38), s(top + 7), s(13), s(13));
        DrawSampleText(graphics, title, s(62), s(top + 4), s(13), ink, FontStyle.Bold);
        DrawSampleText(graphics, amount, s(250), s(top + 4), s(13), secondary, alignment: StringAlignment.Far, width: s(112));
        if (separator)
        {
            using var pen = new Pen(line, s(1));
            graphics.DrawLine(pen, s(62), s(top + 39), s(362), s(top + 39));
        }
    }

    private static void DrawWeekBars(Graphics graphics, Func<float, float> s, RectangleF bounds, Color[] colors)
    {
        bounds = Scale(bounds, s);
        using var track = new SolidBrush(Color.FromArgb(235, 230, 222));
        var heights = new[] { .55f, .75f, .42f, .86f, .62f, .92f, .47f };
        var gap = s(7);
        var barWidth = (bounds.Width - gap * (colors.Length - 1)) / colors.Length;
        for (var i = 0; i < colors.Length; i++)
        {
            var height = bounds.Height * heights[i];
            var x = bounds.Left + i * (barWidth + gap);
            var bar = new RectangleF(x, bounds.Bottom - height, barWidth, height);
            using var front = new SolidBrush(Color.FromArgb(70, colors[i]));
            graphics.FillRectangle(track, new RectangleF(bar.X, bounds.Top, bar.Width, bounds.Height));
            using var path = BackdropControlPaint.RoundedRectangle(bar, s(5));
            graphics.FillPath(front, path);
        }
    }

    private static void DrawChart(Graphics graphics, Func<float, float> s, RectangleF bounds, Color color, Color line, bool withFill)
    {
        bounds = Scale(bounds, s);
        var values = new[] { .62f, .55f, .68f, .43f, .5f, .3f, .38f, .21f, .3f, .14f };
        var points = values.Select((value, index) => new PointF(
            bounds.Left + bounds.Width * index / (values.Length - 1),
            bounds.Top + bounds.Height * value)).ToArray();
        using (var guide = new Pen(line, s(1)) { DashStyle = DashStyle.Dot })
            graphics.DrawLine(guide, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
        if (withFill)
        {
            var area = new List<PointF>(points) { new(bounds.Right, bounds.Bottom), new(bounds.Left, bounds.Bottom) };
            using var fill = new SolidBrush(Color.FromArgb(35, color));
            graphics.FillPolygon(fill, area.ToArray());
        }
        using var pen = new Pen(color, s(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawLines(pen, points);
    }

    private static void DrawSampleText(Graphics graphics, string text, float x, float y, float size, Color color, FontStyle style = FontStyle.Regular, StringAlignment alignment = StringAlignment.Near, float width = 350)
    {
        using var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
        graphics.DrawString(text, font, brush, new RectangleF(x, y, width, size * 1.6f), format);
    }

    private static RectangleF Scale(RectangleF rectangle, Func<float, float> s) => new(
        s(rectangle.X), s(rectangle.Y), s(rectangle.Width), s(rectangle.Height));

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static RectangleF Fit(Size image, float width, float height)
    {
        var scale = Math.Min(width / image.Width, height / image.Height);
        return new RectangleF(0, 0, image.Width * scale, image.Height * scale);
    }

    private static RectangleF Center(RectangleF size, RectangleF bounds) => new(
        bounds.Left + (bounds.Width - size.Width) / 2,
        bounds.Top + (bounds.Height - size.Height) / 2,
        size.Width,
        size.Height);
}
