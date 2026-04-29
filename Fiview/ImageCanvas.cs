using System.Drawing.Drawing2D;

namespace Fiview;

internal sealed class ImageCanvas : Control
{
    private const float ZoomStep = 1.15f;
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 32.0f;

    private Image? _image;
    private Size _naturalSize;
    private float _zoom = 1.0f;
    private PointF _imageOrigin;
    private Point _lastMousePosition;
    private bool _isDragging;
    private bool _fitToViewport = true;
    private bool _isPreviewImage;
    private EventHandler? _animationHandler;

    public event EventHandler? FullQualityRequested;
    public event EventHandler? PreviewQualityRequested;

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

    public bool HasImage => _image is not null;

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

        if (_image is null || ModifierKeys != Keys.Control)
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

        _isDragging = true;
        _lastMousePosition = e.Location;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

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

    private void RequestPreviewIfNeeded()
    {
        if (_image is null || _isPreviewImage || _zoom > GetFitZoom() + 0.0001f)
        {
            return;
        }

        PreviewQualityRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
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

        var destination = new RectangleF(
            _imageOrigin.X,
            _imageOrigin.Y,
            _naturalSize.Width * _zoom,
            _naturalSize.Height * _zoom);

        e.Graphics.DrawImage(_image, destination);
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
