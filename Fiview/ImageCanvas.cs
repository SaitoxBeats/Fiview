// Purpose: Render the current image and own all viewport and crop-selection interaction.
using System.Drawing.Drawing2D;

namespace Fiview;

/// <summary>
/// Custom image surface responsible for painting, zooming, panning, and crop selection.
/// </summary>
internal sealed class ImageCanvas : Control
{
    private const float ZoomStep = 1.15f;
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 32.0f;

    private static readonly Color CropOverlayColor = Color.FromArgb(150, 0, 0, 0);
    private static readonly Color CropBorderColor = Color.FromArgb(255, 0, 255, 140);
    private static readonly Color CropGuideColor = Color.FromArgb(220, 255, 255, 255);

    private Image? _image;
    private Size _naturalSize;
    private float _zoom = 1.0f;
    private PointF _imageOrigin;
    private Point _lastMousePosition;
    private bool _isDragging;
    private bool _fitToViewport = true;
    private bool _isPreviewImage;
    private EventHandler? _animationHandler;

    private bool _isCropModeActive;
    private bool _isSelectingCrop;
    private PointF _cropSelectionAnchor;
    private Rectangle _cropSelection;

    public event EventHandler? FullQualityRequested;
    public event EventHandler? PreviewQualityRequested;

    /// <summary>
    /// Returns whether an image is currently loaded into the canvas.
    /// </summary>
    public bool HasImage => _image is not null;

    /// <summary>
    /// Returns whether the canvas is currently interpreting left-drag as crop selection.
    /// </summary>
    public bool IsCropModeActive => _isCropModeActive;

    /// <summary>
    /// Returns whether a valid crop selection already exists.
    /// </summary>
    public bool HasCropSelection => !_cropSelection.IsEmpty;

    public ImageCanvas()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        BackColor = Color.Black;
        TabStop = true;
        Cursor = Cursors.Default;
    }

    /// <summary>
    /// Replaces the current display image while optionally preserving the current viewport.
    /// </summary>
    public void SetImage(
        Image image,
        bool isPreviewImage = false,
        Size? naturalSize = null,
        bool preserveView = false)
    {
        var oldImage = _image;
        StopAnimation(oldImage);

        _image = image;
        _naturalSize = naturalSize ?? image.Size;
        _isPreviewImage = isPreviewImage;
        oldImage?.Dispose();
        StartAnimation(_image);

        if (preserveView)
        {
            Invalidate();
        }
        else
        {
            ResetViewCore();
        }
    }

    /// <summary>
    /// Enables crop mode without destroying an existing selection.
    /// </summary>
    public void EnterCropMode()
    {
        if (_image is null)
        {
            return;
        }

        _isCropModeActive = true;
        _isDragging = false;
        _isSelectingCrop = false;
        Cursor = Cursors.Cross;
        Invalidate();
    }

    /// <summary>
    /// Leaves crop mode and optionally clears the selection.
    /// </summary>
    public void ExitCropMode(bool clearSelection = false)
    {
        _isCropModeActive = false;
        _isDragging = false;
        _isSelectingCrop = false;

        if (clearSelection)
        {
            _cropSelection = Rectangle.Empty;
        }

        Cursor = Cursors.Default;
        Invalidate();
    }

    /// <summary>
    /// Clears the current crop selection while keeping the current mode unchanged.
    /// </summary>
    public void ClearCropSelection()
    {
        _cropSelection = Rectangle.Empty;
        _isSelectingCrop = false;
        Invalidate();
    }

    /// <summary>
    /// Returns the current crop rectangle in natural image pixels.
    /// </summary>
    public Rectangle? GetCropSelection()
    {
        return HasCropSelection ? _cropSelection : null;
    }

    /// <summary>
    /// Fits the image back to the viewport and restores preview mode if appropriate.
    /// </summary>
    public void ResetView()
    {
        ResetViewCore();
        RequestPreviewIfNeeded();
    }

    private void ResetViewCore()
    {
        if (_image is null)
        {
            _zoom = 1.0f;
            _naturalSize = Size.Empty;
            _imageOrigin = PointF.Empty;
            Invalidate();
            return;
        }

        _fitToViewport = true;
        _zoom = GetFitZoom();
        CenterImage();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAnimation(_image);
            _image?.Dispose();
            _image = null;
        }

        base.Dispose(disposing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_image is null)
        {
            return;
        }

        if (_fitToViewport)
        {
            _zoom = GetFitZoom();
        }

        CenterImage();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_image is null || _isSelectingCrop)
        {
            return;
        }

        if (_isPreviewImage)
        {
            FullQualityRequested?.Invoke(this, EventArgs.Empty);
        }

        ZoomAt(e.Location, e.Delta > 0 ? ZoomStep : 1.0f / ZoomStep);
        RequestPreviewIfNeeded();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_image is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_isCropModeActive)
        {
            if (!TryTranslateClientPointToImagePoint(e.Location, clampToBounds: false, out var anchor))
            {
                return;
            }

            _isSelectingCrop = true;
            _cropSelectionAnchor = anchor;
            _cropSelection = Rectangle.Empty;
            Cursor = Cursors.Cross;
            Invalidate();
            return;
        }

        _isDragging = true;
        _lastMousePosition = e.Location;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isCropModeActive)
        {
            if (!_isSelectingCrop)
            {
                return;
            }

            if (!TryTranslateClientPointToImagePoint(e.Location, clampToBounds: true, out var currentPoint))
            {
                return;
            }

            _cropSelection = BuildCropSelection(_cropSelectionAnchor, currentPoint);
            Invalidate();
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        _fitToViewport = false;
        _imageOrigin = new PointF(
            _imageOrigin.X + e.X - _lastMousePosition.X,
            _imageOrigin.Y + e.Y - _lastMousePosition.Y);

        _lastMousePosition = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_isCropModeActive)
        {
            _isSelectingCrop = false;
            if (_cropSelection.Width < 1 || _cropSelection.Height < 1)
            {
                _cropSelection = Rectangle.Empty;
            }

            Cursor = Cursors.Cross;
            Invalidate();
            return;
        }

        _isDragging = false;
        Cursor = Cursors.Default;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_image is null)
        {
            return;
        }

        e.Graphics.Clear(Color.Black);
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.InterpolationMode = _zoom >= 1.0f
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBilinear;

        if (ImageAnimator.CanAnimate(_image))
        {
            ImageAnimator.UpdateFrames(_image);
        }

        var destination = GetImageDisplayBounds();
        e.Graphics.DrawImage(_image, destination);

        if (_isCropModeActive)
        {
            DrawCropOverlay(e.Graphics, destination);
        }
    }

    /// <summary>
    /// Returns the rectangle currently occupied by the image in client coordinates.
    /// </summary>
    private RectangleF GetImageDisplayBounds()
    {
        return new RectangleF(
            _imageOrigin.X,
            _imageOrigin.Y,
            _naturalSize.Width * _zoom,
            _naturalSize.Height * _zoom);
    }

    /// <summary>
    /// Converts a mouse position into natural image coordinates and optionally clamps it to the image bounds.
    /// </summary>
    private bool TryTranslateClientPointToImagePoint(Point clientPoint, bool clampToBounds, out PointF imagePoint)
    {
        imagePoint = PointF.Empty;

        if (_image is null || _zoom <= 0.0f)
        {
            return false;
        }

        var imageBounds = GetImageDisplayBounds();
        if (!clampToBounds &&
            (clientPoint.X < imageBounds.Left ||
             clientPoint.X > imageBounds.Right ||
             clientPoint.Y < imageBounds.Top ||
             clientPoint.Y > imageBounds.Bottom))
        {
            return false;
        }

        var translatedX = clampToBounds
            ? Math.Clamp(clientPoint.X, imageBounds.Left, imageBounds.Right)
            : clientPoint.X;
        var translatedY = clampToBounds
            ? Math.Clamp(clientPoint.Y, imageBounds.Top, imageBounds.Bottom)
            : clientPoint.Y;

        imagePoint = new PointF(
            Math.Clamp((translatedX - _imageOrigin.X) / _zoom, 0.0f, _naturalSize.Width),
            Math.Clamp((translatedY - _imageOrigin.Y) / _zoom, 0.0f, _naturalSize.Height));

        return true;
    }

    /// <summary>
    /// Builds a freeform rectangular selection anchored at the initial drag point.
    /// </summary>
    private Rectangle BuildCropSelection(PointF anchor, PointF currentPoint)
    {
        var left = Math.Min(anchor.X, currentPoint.X);
        var top = Math.Min(anchor.Y, currentPoint.Y);
        var right = Math.Max(anchor.X, currentPoint.X);
        var bottom = Math.Max(anchor.Y, currentPoint.Y);

        var selectionWidth = right - left;
        var selectionHeight = bottom - top;
        if (selectionWidth < 1.0f || selectionHeight < 1.0f)
        {
            return Rectangle.Empty;
        }

        var leftPixels = Math.Clamp((int)MathF.Floor(left), 0, Math.Max(0, _naturalSize.Width - 1));
        var topPixels = Math.Clamp((int)MathF.Floor(top), 0, Math.Max(0, _naturalSize.Height - 1));
        var rightPixels = Math.Clamp((int)MathF.Ceiling(right), leftPixels + 1, _naturalSize.Width);
        var bottomPixels = Math.Clamp((int)MathF.Ceiling(bottom), topPixels + 1, _naturalSize.Height);

        return new Rectangle(
            leftPixels,
            topPixels,
            rightPixels - leftPixels,
            bottomPixels - topPixels);
    }

    /// <summary>
    /// Draws the crop overlay without mutating the underlying bitmap.
    /// </summary>
    private void DrawCropOverlay(Graphics graphics, RectangleF imageBounds)
    {
        using var imageBorderPen = new Pen(CropGuideColor, 1.0f) { DashStyle = DashStyle.Dash };
        graphics.DrawRectangle(
            imageBorderPen,
            imageBounds.X,
            imageBounds.Y,
            imageBounds.Width,
            imageBounds.Height);

        if (HasCropSelection)
        {
            var selectionBounds = new RectangleF(
                _imageOrigin.X + _cropSelection.X * _zoom,
                _imageOrigin.Y + _cropSelection.Y * _zoom,
                _cropSelection.Width * _zoom,
                _cropSelection.Height * _zoom);

            using var overlayBrush = new SolidBrush(CropOverlayColor);
            FillOverlayRectangle(graphics, overlayBrush, 0.0f, 0.0f, ClientSize.Width, selectionBounds.Top);
            FillOverlayRectangle(graphics, overlayBrush, 0.0f, selectionBounds.Bottom, ClientSize.Width, ClientSize.Height - selectionBounds.Bottom);
            FillOverlayRectangle(graphics, overlayBrush, 0.0f, selectionBounds.Top, selectionBounds.Left, selectionBounds.Height);
            FillOverlayRectangle(graphics, overlayBrush, selectionBounds.Right, selectionBounds.Top, ClientSize.Width - selectionBounds.Right, selectionBounds.Height);

            using var selectionPen = new Pen(CropBorderColor, 2.0f);
            graphics.DrawRectangle(
                selectionPen,
                selectionBounds.X,
                selectionBounds.Y,
                selectionBounds.Width,
                selectionBounds.Height);
        }

        var statusText = HasCropSelection
            ? "Crop mode: press Ctrl to save or discard the selection."
            : "Crop mode: drag to draw a crop rectangle, then press Ctrl again.";

        TextRenderer.DrawText(
            graphics,
            statusText,
            Font,
            new Point(12, 12),
            Color.White,
            Color.FromArgb(90, 0, 0, 0));
    }

    private static void FillOverlayRectangle(Graphics graphics, Brush brush, float x, float y, float width, float height)
    {
        if (width <= 0.0f || height <= 0.0f)
        {
            return;
        }

        graphics.FillRectangle(brush, x, y, width, height);
    }

    private void RequestPreviewIfNeeded()
    {
        if (_image is null || _isPreviewImage || _zoom > GetFitZoom() + 0.0001f)
        {
            return;
        }

        PreviewQualityRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ZoomAt(Point location, float factor)
    {
        if (_image is null)
        {
            return;
        }

        var oldZoom = _zoom;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            return;
        }

        var imagePointX = (location.X - _imageOrigin.X) / oldZoom;
        var imagePointY = (location.Y - _imageOrigin.Y) / oldZoom;

        _zoom = newZoom;
        _fitToViewport = false;
        _imageOrigin = new PointF(
            location.X - imagePointX * newZoom,
            location.Y - imagePointY * newZoom);

        Invalidate();
    }

    private float GetFitZoom()
    {
        if (_image is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return 1.0f;
        }

        var widthScale = (float)ClientSize.Width / _naturalSize.Width;
        var heightScale = (float)ClientSize.Height / _naturalSize.Height;
        return Math.Min(1.0f, Math.Min(widthScale, heightScale));
    }

    private void CenterImage()
    {
        if (_image is null)
        {
            return;
        }

        var imageWidth = _naturalSize.Width * _zoom;
        var imageHeight = _naturalSize.Height * _zoom;

        if (imageWidth <= ClientSize.Width)
        {
            _imageOrigin.X = (ClientSize.Width - imageWidth) / 2.0f;
        }

        if (imageHeight <= ClientSize.Height)
        {
            _imageOrigin.Y = (ClientSize.Height - imageHeight) / 2.0f;
        }
    }

    private void StartAnimation(Image? image)
    {
        if (image is null || !ImageAnimator.CanAnimate(image))
        {
            return;
        }

        _animationHandler = (_, _) =>
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke((Action)Invalidate);
            }
            else
            {
                Invalidate();
            }
        };

        ImageAnimator.Animate(image, _animationHandler);
    }

    private void StopAnimation(Image? image)
    {
        if (image is null || _animationHandler is null)
        {
            return;
        }

        if (ImageAnimator.CanAnimate(image))
        {
            ImageAnimator.StopAnimate(image, _animationHandler);
        }

        _animationHandler = null;
    }
}
