using System.Drawing.Drawing2D;

namespace Fiview;

internal sealed class ImageCanvas : Control
{
    private const float ZoomStep = 1.15f;
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 32.0f;

    private Image? _image;
    private float _zoom = 1.0f;
    private PointF _imageOrigin;
    private Point _lastMousePosition;
    private bool _isDragging;
    private bool _fitToViewport = true;

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

    public void SetImage(Image image)
    {
        var oldImage = _image;
        _image = image;
        oldImage?.Dispose();

        ResetView();
    }

    public void ResetView()
    {
        if (_image is null)
        {
            _zoom = 1.0f;
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

        ZoomAt(e.Location, e.Delta > 0 ? ZoomStep : 1.0f / ZoomStep);
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

        var destination = new RectangleF(
            _imageOrigin.X,
            _imageOrigin.Y,
            _image.Width * _zoom,
            _image.Height * _zoom);

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

        var widthScale = (float)ClientSize.Width / _image.Width;
        var heightScale = (float)ClientSize.Height / _image.Height;
        return Math.Min(1.0f, Math.Min(widthScale, heightScale));
    }

    private void CenterImage()
    {
        if (_image is null)
        {
            return;
        }

        var imageWidth = _image.Width * _zoom;
        var imageHeight = _image.Height * _zoom;

        if (imageWidth <= ClientSize.Width)
        {
            _imageOrigin.X = (ClientSize.Width - imageWidth) / 2.0f;
        }

        if (imageHeight <= ClientSize.Height)
        {
            _imageOrigin.Y = (ClientSize.Height - imageHeight) / 2.0f;
        }
    }
}
