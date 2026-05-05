using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace Fiview;

public partial class imgView_Form : Form
{
    #region Constants and Static Readonly
    private const int PreviewCompressionThreshold = 1000;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    private static readonly string[] CropSaveExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    ];
    #endregion

    #region State Fields
    // Navegação e Arquivos
    private string[] imageFiles = [];
    private string? currentDirectory;
    private string? currentImagePath;
    private int currentImageIndex = -1;

    // Estado da Imagem Atual
    private Size currentImageNaturalSize;
    private bool currentImageIsPreview;
    private bool currentImageHasPreview;

    // Cache e Preload
    private readonly object cacheLock = new();
    private readonly Dictionary<string, LoadedImage> preloadedImages = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? preloadCancellation;

    // Estado de Input
    private bool controlKeyPressed;
    private bool controlKeyUsedByShortcut;
    private ContextMenuStrip? contextMenu;
    #endregion

    #region Constructor & Initialization
    public imgView_Form(string? initialImagePath = null)
    {
        InitializeComponent();
        ConfigureForm();
        RegisterEvents();

        if (!string.IsNullOrWhiteSpace(initialImagePath))
            LoadImage(initialImagePath);
        else
            OpenImageDialog();
    }

    private void ConfigureForm()
    {
        Text = "Fast Image View";
        BackColor = Color.Black;
        DoubleBuffered = true;

        SetWindowDarkMode(Handle);
        CreateContextMenu();
    }

    private void RegisterEvents()
    {
        KeyPreview = true;
        KeyDown += imgViewForm_KeyDown;
        KeyUp += imgViewForm_KeyUp;

        img_ref.MouseWheel += imgRef_MouseWheel;
        img_ref.FullQualityRequested += imgRef_FullQualityRequested;
        img_ref.PreviewQualityRequested += imgRef_PreviewQualityRequested;

        AllowDrop = true;
        DragEnter += imgViewForm_DragEnter;
        DragDrop += imgViewForm_DragDrop;
    }
    #endregion

    #region Image Loading Logic
    public void LoadImage(string path)
    {
        if (!TryLeaveCropModeIfNeeded()) return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !IsImageFile(fullPath)) return;

            UpdateNavigationList(fullPath);

            var loadedImage = TakePreloadedImage(fullPath) ?? DecodeDisplayImage(fullPath);

            // Atualiza estado local
            currentImagePath = fullPath;
            currentImageIsPreview = loadedImage.IsPreview;
            currentImageNaturalSize = loadedImage.NaturalSize;
            currentImageHasPreview = ShouldUsePreview(loadedImage.NaturalSize.Width, loadedImage.NaturalSize.Height);

            // Atualiza UI
            img_ref.ExitCropMode(clearSelection: true);
            img_ref.SetImage(loadedImage.Image, loadedImage.IsPreview, loadedImage.NaturalSize);

            UpdateWindowTitle(fullPath);
            RestoreWindow();
            PreloadNeighborImages();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem: {ex.Message}", "Fiview", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateNavigationList(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            LoadImageList(fullPath);
        }
        else
        {
            currentImageIndex = Array.FindIndex(imageFiles, f => string.Equals(f, fullPath, StringComparison.OrdinalIgnoreCase));
            if (currentImageIndex < 0) LoadImageList(fullPath);
        }
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

        currentImageIndex = Array.FindIndex(imageFiles, f => string.Equals(f, initialPath, StringComparison.OrdinalIgnoreCase));
    }
    #endregion

    #region Navigation Methods
    private void LoadPreviousImage()
    {
        if (imageFiles.Length == 0 || currentImageIndex < 0) return;
        var prev = currentImageIndex == 0 ? imageFiles.Length - 1 : currentImageIndex - 1;
        LoadImage(imageFiles[prev]);
    }

    private void LoadNextImage()
    {
        if (imageFiles.Length == 0 || currentImageIndex < 0) return;
        var next = currentImageIndex == imageFiles.Length - 1 ? 0 : currentImageIndex + 1;
        LoadImage(imageFiles[next]);
    }
    #endregion

    #region Crop Workflow
    private void ToggleCropMode()
    {
        if (!img_ref.HasImage) return;

        if (!img_ref.IsCropModeActive)
        {
            img_ref.EnterCropMode();
            UpdateWindowTitle(currentImagePath);
        }
        else
        {
            TryLeaveCropModeIfNeeded(reloadSavedImageAfterSave: true);
        }
    }

    private bool TryLeaveCropModeIfNeeded(bool reloadSavedImageAfterSave = false)
    {
        if (!img_ref.IsCropModeActive) return true;

        if (!img_ref.HasCropSelection)
        {
            img_ref.ExitCropMode(clearSelection: true);
            UpdateWindowTitle(currentImagePath);
            return true;
        }

        var result = MessageBox.Show("Deseja salvar o recorte antes de sair?", "Salvar", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => SaveCurrentCropSelection(reloadSavedImageAfterSave),
            DialogResult.No => ExitCropWithoutSaving(),
            _ => false
        };
    }

    private bool ExitCropWithoutSaving()
    {
        img_ref.ExitCropMode(clearSelection: true);
        UpdateWindowTitle(currentImagePath);
        return true;
    }

    private bool SaveCurrentCropSelection(bool reloadSavedImageAfterSave)
    {
        if (string.IsNullOrWhiteSpace(currentImagePath)) return false;

        var selection = img_ref.GetCropSelection();
        if (selection is null || selection.Value.Width <= 0 || selection.Value.Height <= 0)
            return ExitCropWithoutSaving();

        var defaultExt = GetDefaultCropExtension(currentImagePath);
        using var sfd = new SaveFileDialog
        {
            Title = "Salvar imagem recortada",
            Filter = "PNG image|*.png|JPEG image|*.jpg;*.jpeg|Bitmap image|*.bmp|WEBP image|*.webp",
            DefaultExt = defaultExt.TrimStart('.'),
            FileName = BuildCroppedFileName(currentImagePath, defaultExt)
        };

        if (sfd.ShowDialog() != DialogResult.OK) return false;

        try
        {
            ProcessAndSaveCrop(sfd.FileName, selection.Value);

            img_ref.ExitCropMode(clearSelection: true);
            UpdateWindowTitle(currentImagePath);

            if (reloadSavedImageAfterSave) LoadImage(sfd.FileName);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Fiview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void ProcessAndSaveCrop(string targetPath, Rectangle selection)
    {
        using var sourceImage = ImageSharpImage.Load(currentImagePath!);
        var cropArea = ClampCropSelectionToBounds(selection, sourceImage.Width, sourceImage.Height);

        sourceImage.Mutate(ctx => ctx.Crop(new ImageSharpRectangle(cropArea.X, cropArea.Y, cropArea.Width, cropArea.Height)));

        using var outputStream = File.Create(targetPath);
        sourceImage.Save(outputStream, CreateCropEncoder(targetPath));
    }
    #endregion

    #region Input Event Handlers
    private void imgViewForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ControlKey)
        {
            controlKeyPressed = true;
            controlKeyUsedByShortcut = false;
            e.Handled = true;
            return;
        }

        if (controlKeyPressed) controlKeyUsedByShortcut = true;

        switch (e.KeyCode)
        {
            case Keys.Left: LoadPreviousImage(); e.Handled = true; break;
            case Keys.Right: LoadNextImage(); e.Handled = true; break;
            case Keys.F: img_ref.ResetView(); e.Handled = true; break;
            case Keys.Escape:
                if (img_ref.IsCropModeActive) { TryLeaveCropModeIfNeeded(); e.Handled = true; }
                break;
        }
    }

    private void imgViewForm_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.ControlKey) return;

        var shouldToggle = controlKeyPressed && !controlKeyUsedByShortcut;
        controlKeyPressed = false;
        controlKeyUsedByShortcut = false;

        if (shouldToggle)
        {
            ToggleCropMode();
            e.Handled = true;
        }
    }

    private void imgRef_MouseWheel(object? sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) == Keys.Control) controlKeyUsedByShortcut = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left: LoadPreviousImage(); return true;
            case Keys.Right: LoadNextImage(); return true;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void imgViewForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 } && IsImageFile(files[0])) e.Effect = DragDropEffects.Copy;
        }
    }

    private void imgViewForm_DragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files is { Length: > 0 }) LoadImage(files[0]);
    }
    #endregion

    #region Quality Switching Handlers
    private void imgRef_FullQualityRequested(object? sender, EventArgs e)
    {
        if (!currentImageIsPreview || string.IsNullOrWhiteSpace(currentImagePath)) return;
        try
        {
            var fullImg = DecodeImage(currentImagePath);
            currentImageIsPreview = false;
            img_ref.SetImage(fullImg, naturalSize: fullImg.Size, preserveView: true);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void imgRef_PreviewQualityRequested(object? sender, EventArgs e)
    {
        if (currentImageIsPreview || !currentImageHasPreview || string.IsNullOrWhiteSpace(currentImagePath)) return;
        try
        {
            var info = ImageSharpImage.Identify(currentImagePath);
            if (info == null) return;
            var preview = DecodePreviewImage(currentImagePath, info.Width, info.Height);
            currentImageIsPreview = true;
            img_ref.SetImage(preview, isPreviewImage: true, naturalSize: new Size(info.Width, info.Height), preserveView: true);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    protected override void OnResize(EventArgs e) // muda pra qualidade maxima quando detectar maximização de janela
    {
        base.OnResize(e);

        if (this.WindowState == FormWindowState.Maximized)
        {
            if (currentImageIsPreview && !string.IsNullOrWhiteSpace(currentImagePath))
            {
                imgRef_FullQualityRequested(this, EventArgs.Empty);
            }
        }
    }
    #endregion

    #region Decoding & Image Utilities
    private static LoadedImage DecodeDisplayImage(string path)
    {
        if (IsGifFile(path))
        {
            var gif = DecodeImage(path);
            return new LoadedImage(gif, false, gif.Size);
        }

        var info = ImageSharpImage.Identify(path);
        if (info != null && ShouldUsePreview(info.Width, info.Height))
        {
            return new LoadedImage(DecodePreviewImage(path, info.Width, info.Height), true, new Size(info.Width, info.Height));
        }

        var bmp = DecodeImage(path);
        return new LoadedImage(bmp, false, bmp.Size);
    }

    private static Image DecodeImage(string path)
    {
        if (IsGifFile(path)) return Image.FromFile(path);
        if (string.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase)) return DecodeWebpImage(path);

        using var source = Image.FromFile(path);
        return new Bitmap(source);
    }

    private static Bitmap DecodePreviewImage(string path, int w, int h)
    {
        using var image = ImageSharpImage.Load<Rgba32>(path);
        var scale = Math.Min((float)PreviewCompressionThreshold / w, (float)PreviewCompressionThreshold / h);
        var pw = Math.Max(1, (int)MathF.Round(w * scale));
        var ph = Math.Max(1, (int)MathF.Round(h * scale));

        image.Mutate(x => x.Resize(pw, ph));
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
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            image.ProcessPixelRows(accessor => {
                var rowBuffer = new byte[image.Width * 4];
                for (var y = 0; y < image.Height; y++)
                {
                    var span = accessor.GetRowSpan(y);
                    for (var x = 0; x < span.Length; x++)
                    {
                        var p = span[x]; var o = x * 4;
                        rowBuffer[o] = p.B; rowBuffer[o + 1] = p.G; rowBuffer[o + 2] = p.R; rowBuffer[o + 3] = p.A;
                    }
                    Marshal.Copy(rowBuffer, 0, IntPtr.Add(data.Scan0, y * data.Stride), rowBuffer.Length);
                }
            });
        }
        catch { bitmap.Dispose(); throw; }
        finally { bitmap.UnlockBits(data); }
        return bitmap;
    }
    #endregion

    #region Preload & Cache Management
    private void PreloadNeighborImages()
    {
        preloadCancellation?.Cancel();
        preloadCancellation?.Dispose();

        var targets = GetNeighborImagePaths();
        if (targets.Length == 0) { ClearPreloadedImages(); return; }

        preloadCancellation = new CancellationTokenSource();
        var token = preloadCancellation.Token;

        _ = Task.Run(() =>
        {
            TrimPreloadedImages(targets.ToHashSet(StringComparer.OrdinalIgnoreCase));
            foreach (var target in targets)
            {
                if (token.IsCancellationRequested) break;
                lock (cacheLock) { if (preloadedImages.ContainsKey(target)) continue; }

                try
                {
                    var loaded = DecodeDisplayImage(target);
                    if (token.IsCancellationRequested) { loaded.Image.Dispose(); break; }
                    lock (cacheLock)
                    {
                        if (preloadedImages.ContainsKey(target)) loaded.Image.Dispose();
                        else preloadedImages[target] = loaded;
                    }
                }
                catch { continue; }
            }
        }, token);
    }

    private string[] GetNeighborImagePaths()
    {
        if (imageFiles.Length <= 1 || currentImageIndex < 0) return [];
        var prev = currentImageIndex == 0 ? imageFiles.Length - 1 : currentImageIndex - 1;
        var next = currentImageIndex == imageFiles.Length - 1 ? 0 : currentImageIndex + 1;
        return prev == next ? [imageFiles[next]] : [imageFiles[prev], imageFiles[next]];
    }

    private LoadedImage? TakePreloadedImage(string path)
    {
        lock (cacheLock) { return preloadedImages.Remove(path, out var img) ? img : null; }
    }

    private void TrimPreloadedImages(HashSet<string> keep)
    {
        lock (cacheLock)
        {
            foreach (var path in preloadedImages.Keys.Where(p => !keep.Contains(p)).ToArray())
            {
                preloadedImages[path].Image.Dispose();
                preloadedImages.Remove(path);
            }
        }
    }

    private void ClearPreloadedImages()
    {
        lock (cacheLock)
        {
            foreach (var img in preloadedImages.Values) img.Image.Dispose();
            preloadedImages.Clear();
        }
    }
    #endregion

    #region UI & Helper Methods
    private void UpdateWindowTitle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Text = img_ref.IsCropModeActive ? "Fast Image View [Crop Mode]" : "Fast Image View";
            return;
        }

        var pos = currentImageIndex >= 0 ? currentImageIndex + 1 : 0;
        var res = currentImageNaturalSize.Width > 0 ? $" ({currentImageNaturalSize.Width}x{currentImageNaturalSize.Height})" : "";
        var baseTitle = imageFiles.Length > 0
            ? $"[{pos}/{imageFiles.Length}] - {Path.GetFileName(path)}{res} - Fast Image View"
            : $"{Path.GetFileName(path)}{res} - Fast Image View";

        Text = img_ref.IsCropModeActive ? $"{baseTitle} [Crop Mode]" : baseTitle;
    }

    private void RestoreWindow()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void CreateContextMenu()
    {
        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Open image", null, (_, _) => OpenImageDialog()));
        contextMenu.Items.Add(new ToolStripMenuItem("Fit image", null, (_, _) => img_ref.ResetView()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => Close()));

        img_ref.ContextMenuStrip = contextMenu;
        ContextMenuStrip = contextMenu;
    }

    private void OpenImageDialog()
    {
        if (!TryLeaveCropModeIfNeeded()) return;
        using var ofd = new OpenFileDialog { Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp" };
        if (ofd.ShowDialog() == DialogResult.OK) LoadImage(ofd.FileName);
    }

    private static Rectangle ClampCropSelectionToBounds(Rectangle sel, int imgW, int imgH)
    {
        var l = Math.Clamp(sel.X, 0, Math.Max(0, imgW - 1));
        var t = Math.Clamp(sel.Y, 0, Math.Max(0, imgH - 1));
        return new Rectangle(l, t, Math.Clamp(sel.Width, 1, imgW - l), Math.Clamp(sel.Height, 1, imgH - t));
    }

    private static string BuildCroppedFileName(string path, string ext) => $"{Path.GetFileNameWithoutExtension(path)}_cropped{ext}";
    private static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path));
    private static bool IsGifFile(string path) => string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
    private static bool ShouldUsePreview(int w, int h) => w > PreviewCompressionThreshold || h > PreviewCompressionThreshold;

    private static string GetDefaultCropExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return CropSaveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) ? ext : ".png";
    }

    private static IImageEncoder CreateCropEncoder(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => new JpegEncoder(),
        ".bmp" => new BmpEncoder(),
        ".webp" => new WebpEncoder(),
        _ => new PngEncoder()
    };
    #endregion

    #region Form Lifecycle
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!TryLeaveCropModeIfNeeded()) e.Cancel = true;
        else base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        preloadCancellation?.Cancel();
        preloadCancellation?.Dispose();
        ClearPreloadedImages();
        base.OnFormClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static void SetWindowDarkMode(IntPtr handle)
    {
        if (Environment.OSVersion.Version.Major < 10) return;
        var useImmersiveDarkMode = 1;
        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
    }
    #endregion

    private sealed record LoadedImage(Image Image, bool IsPreview, Size NaturalSize);
}