using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace Fiview;

public partial class imgView_Form : Form
{
    private const int PreviewCompressionThreshold = 1000;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".webp"
    };

    private ContextMenuStrip? contextMenu;
    private readonly object cacheLock = new();
    private readonly Dictionary<string, LoadedImage> preloadedImages = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? preloadCancellation;
    private int currentImageIndex = -1;
    private string[] imageFiles = [];
    private string? currentDirectory;
    private string? currentImagePath;
    private bool currentImageIsPreview;
    private bool currentImageHasPreview;

    public imgView_Form(string? initialImagePath = null)
    {
        InitializeComponent();
        ConfigureForm();

        KeyPreview = true;
        KeyDown += imgViewForm_KeyDown;
        img_ref.FullQualityRequested += imgRef_FullQualityRequested;
        img_ref.PreviewQualityRequested += imgRef_PreviewQualityRequested;

        AllowDrop = true;
        DragEnter += imgViewForm_DragEnter;
        DragDrop += imgViewForm_DragDrop;

        if (!string.IsNullOrWhiteSpace(initialImagePath))
        {
            LoadImage(initialImagePath);
        }
        else
        {
            OpenImageDialog();
        }
    }

    public void LoadImage(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !IsImageFile(fullPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                LoadImageList(fullPath);
            }
            else
            {
                currentImageIndex = Array.FindIndex(
                    imageFiles,
                    file => string.Equals(file, fullPath, StringComparison.OrdinalIgnoreCase));
            }

            var loadedImage = TakePreloadedImage(fullPath) ?? DecodeDisplayImage(fullPath);

            currentImagePath = fullPath;
            currentImageIsPreview = loadedImage.IsPreview;
            currentImageHasPreview = ShouldUsePreview(loadedImage.NaturalSize.Width, loadedImage.NaturalSize.Height);

            img_ref.SetImage(loadedImage.Bitmap, loadedImage.IsPreview, loadedImage.NaturalSize);
            UpdateWindowTitle(fullPath);
            RestoreWindow();
            PreloadNeighborImages();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem: {ex.Message}", "Fiview", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureForm()
    {
        Text = "Fast Image View";
        BackColor = Color.Black;
        DoubleBuffered = true;

        SetWindowDarkMode(Handle);
        CreateContextMenu();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private static void SetWindowDarkMode(IntPtr handle)
    {
        if (Environment.OSVersion.Version.Major < 10)
        {
            return;
        }

        var useImmersiveDarkMode = 1;
        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
    }

    private void OpenImageDialog()
    {
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            LoadImage(openFileDialog.FileName);
        }
    }

    private void imgViewForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left:
                LoadPreviousImage();
                e.Handled = true;
                break;
            case Keys.Right:
                LoadNextImage();
                e.Handled = true;
                break;
            case Keys.F:
                img_ref.ResetView();
                e.Handled = true;
                break;
        }
    }

    private void imgRef_FullQualityRequested(object? sender, EventArgs e)
    {
        if (!currentImageIsPreview || string.IsNullOrWhiteSpace(currentImagePath))
        {
            return;
        }

        try
        {
            var fullQualityBitmap = DecodeImage(currentImagePath);
            currentImageIsPreview = false;
            img_ref.SetImage(fullQualityBitmap, naturalSize: fullQualityBitmap.Size, preserveView: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem original: {ex.Message}", "Fiview", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void imgRef_PreviewQualityRequested(object? sender, EventArgs e)
    {
        if (currentImageIsPreview || !currentImageHasPreview || string.IsNullOrWhiteSpace(currentImagePath))
        {
            return;
        }

        try
        {
            var info = ImageSharpImage.Identify(currentImagePath);
            if (info is null)
            {
                return;
            }

            var previewBitmap = DecodePreviewImage(currentImagePath, info.Width, info.Height);
            currentImageIsPreview = true;
            img_ref.SetImage(previewBitmap, isPreviewImage: true, naturalSize: new Size(info.Width, info.Height), preserveView: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao restaurar preview da imagem: {ex.Message}", "Fiview", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left:
                LoadPreviousImage();
                return true;
            case Keys.Right:
                LoadNextImage();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void LoadPreviousImage()
    {
        if (imageFiles.Length == 0 || currentImageIndex < 0)
        {
            return;
        }

        currentImageIndex = currentImageIndex == 0
            ? imageFiles.Length - 1
            : currentImageIndex - 1;

        LoadImage(imageFiles[currentImageIndex]);
    }

    private void LoadNextImage()
    {
        if (imageFiles.Length == 0 || currentImageIndex < 0)
        {
            return;
        }

        currentImageIndex = currentImageIndex == imageFiles.Length - 1
            ? 0
            : currentImageIndex + 1;

        LoadImage(imageFiles[currentImageIndex]);
    }

    private void LoadImageList(string initialPath)
    {
        currentDirectory = Path.GetDirectoryName(initialPath);
        if (currentDirectory is null)
        {
            imageFiles = [];
            currentImageIndex = -1;
            return;
        }

        imageFiles = Directory.EnumerateFiles(currentDirectory)
            .Where(IsImageFile)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        currentImageIndex = Array.FindIndex(
            imageFiles,
            file => string.Equals(file, initialPath, StringComparison.OrdinalIgnoreCase));
    }

    private LoadedImage? TakePreloadedImage(string path)
    {
        lock (cacheLock)
        {
            if (!preloadedImages.Remove(path, out var loadedImage))
            {
                return null;
            }

            return loadedImage;
        }
    }

    private static LoadedImage DecodeDisplayImage(string path)
    {
        var info = ImageSharpImage.Identify(path);
        if (info is not null && ShouldUsePreview(info.Width, info.Height))
        {
            return new LoadedImage(DecodePreviewImage(path, info.Width, info.Height), true, new Size(info.Width, info.Height));
        }

        var bitmap = DecodeImage(path);
        return new LoadedImage(bitmap, false, bitmap.Size);
    }

    private static Bitmap DecodeImage(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeWebpImage(path);
        }

        using var source = Image.FromFile(path);
        return new Bitmap(source);
    }

    private static bool ShouldUsePreview(int width, int height)
    {
        return width > PreviewCompressionThreshold || height > PreviewCompressionThreshold;
    }

    private static Bitmap DecodePreviewImage(string path, int sourceWidth, int sourceHeight)
    {
        using var image = ImageSharpImage.Load<Rgba32>(path);
        var scale = Math.Min(
            (float)PreviewCompressionThreshold / sourceWidth,
            (float)PreviewCompressionThreshold / sourceHeight);

        var previewWidth = Math.Max(1, (int)MathF.Round(sourceWidth * scale));
        var previewHeight = Math.Max(1, (int)MathF.Round(sourceHeight * scale));

        image.Mutate(context => context.Resize(previewWidth, previewHeight));
        return ToBitmap(image);
    }

    private static Bitmap DecodeWebpImage(string path)
    {
        using var image = ImageSharpImage.Load<Rgba32>(path);
        return ToBitmap(image);
    }

    private static Bitmap ToBitmap(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            image.ProcessPixelRows(accessor =>
            {
                var rowBuffer = new byte[image.Width * 4];

                for (var y = 0; y < image.Height; y++)
                {
                    var sourceRow = accessor.GetRowSpan(y);

                    for (var x = 0; x < sourceRow.Length; x++)
                    {
                        var pixel = sourceRow[x];
                        var offset = x * 4;

                        rowBuffer[offset] = pixel.B;
                        rowBuffer[offset + 1] = pixel.G;
                        rowBuffer[offset + 2] = pixel.R;
                        rowBuffer[offset + 3] = pixel.A;
                    }

                    Marshal.Copy(rowBuffer, 0, IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), rowBuffer.Length);
                }
            });
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    private void PreloadNeighborImages()
    {
        preloadCancellation?.Cancel();
        preloadCancellation?.Dispose();
        preloadCancellation = null;

        var preloadTargets = GetNeighborImagePaths();
        if (preloadTargets.Length == 0)
        {
            ClearPreloadedImages();
            return;
        }

        preloadCancellation = new CancellationTokenSource();
        var token = preloadCancellation.Token;

        _ = Task.Run(() =>
        {
            var keep = preloadTargets.ToHashSet(StringComparer.OrdinalIgnoreCase);
            TrimPreloadedImages(keep);

            foreach (var target in preloadTargets)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                lock (cacheLock)
                {
                    if (preloadedImages.ContainsKey(target))
                    {
                        continue;
                    }
                }

                LoadedImage? loadedImage = null;
                try
                {
                    loadedImage = DecodeDisplayImage(target);
                }
                catch
                {
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    loadedImage.Bitmap.Dispose();
                    break;
                }

                lock (cacheLock)
                {
                    if (preloadedImages.ContainsKey(target))
                    {
                        loadedImage.Bitmap.Dispose();
                    }
                    else
                    {
                        preloadedImages[target] = loadedImage;
                    }
                }
            }
        }, token);
    }

    private string[] GetNeighborImagePaths()
    {
        if (imageFiles.Length <= 1 || currentImageIndex < 0)
        {
            return [];
        }

        var previousIndex = currentImageIndex == 0 ? imageFiles.Length - 1 : currentImageIndex - 1;
        var nextIndex = currentImageIndex == imageFiles.Length - 1 ? 0 : currentImageIndex + 1;

        return previousIndex == nextIndex
            ? [imageFiles[nextIndex]]
            : [imageFiles[previousIndex], imageFiles[nextIndex]];
    }

    private void TrimPreloadedImages(HashSet<string> keep)
    {
        lock (cacheLock)
        {
            foreach (var path in preloadedImages.Keys.Where(path => !keep.Contains(path)).ToArray())
            {
                preloadedImages[path].Bitmap.Dispose();
                preloadedImages.Remove(path);
            }
        }
    }

    private void ClearPreloadedImages()
    {
        lock (cacheLock)
        {
            foreach (var loadedImage in preloadedImages.Values)
            {
                loadedImage.Bitmap.Dispose();
            }

            preloadedImages.Clear();
        }
    }

    private void UpdateWindowTitle(string path)
    {
        var position = currentImageIndex >= 0 ? currentImageIndex + 1 : 0;
        var total = imageFiles.Length;
        Text = total > 0
            ? $"[{position}/{total}] - {Path.GetFileName(path)} - Fast Image View"
            : $"{Path.GetFileName(path)} - Fast Image View";
    }

    private void RestoreWindow()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
        BringToFront();
    }

    private void CreateContextMenu()
    {
        contextMenu = new ContextMenuStrip();

        var openImgMenu = new ToolStripMenuItem("Open image");
        openImgMenu.Click += (_, _) => OpenImageDialog();

        var resetZoomMenu = new ToolStripMenuItem("Fit image");
        resetZoomMenu.Click += (_, _) => img_ref.ResetView();

        var quitBtMenu = new ToolStripMenuItem("Quit");
        quitBtMenu.Click += (_, _) => Close();

        contextMenu.Items.Add(openImgMenu);
        contextMenu.Items.Add(resetZoomMenu);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitBtMenu);

        img_ref.ContextMenuStrip = contextMenu;
        ContextMenuStrip = contextMenu;
    }

    private void imgViewForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is { Length: > 0 } && IsImageFile(files[0]))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void imgViewForm_DragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files is { Length: > 0 })
        {
            LoadImage(files[0]);
        }
    }

    private static bool IsImageFile(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        preloadCancellation?.Cancel();
        preloadCancellation?.Dispose();
        ClearPreloadedImages();
        base.OnFormClosed(e);
    }

    private sealed record LoadedImage(Bitmap Bitmap, bool IsPreview, Size NaturalSize);
}
