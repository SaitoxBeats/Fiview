using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Fiview;

public partial class imgView_Form : Form
{
    //BUGS: certas imgs ficam fixas no topo da janela e corta fica tudo estranho
    
    //PARA FAZER:
    //zoom,
    //menu do botão direito,
    //abrir nova img usando o menu do botão direito,
    //drag and drop para abrir nova img,

    private ContextMenuStrip contextMenu;
    
    private float zoomFactor = 1.0f;
    private const float ZOOM_INCREMENT = 0.2f;
    private const float MIN_ZOOM = 0.1f;
    private const float MAX_ZOOM = 4.0f;
    private System.Windows.Forms.Timer zoomTimer;
    private Image originalImage;
    private Point lastMousePosition;
    
    private int currentImageIndex = -1;
    private string[] imageFiles;
    private string currentDirectory;

    private FormWindowState _lastWindowState;
    private bool isMaximized;
    
    
    public imgView_Form(string initialImagePath = null)
    {
        InitializeComponent();
        ConfigureForm();
        
        if(!string.IsNullOrEmpty(initialImagePath))
            LoadImage(initialImagePath);
        else
            LoadCommandLineArgs();
        
        //zoom config
        zoomTimer = new System.Windows.Forms.Timer();
        zoomTimer.Interval = 500; //is ms
        zoomTimer.Tick += ZoomTimer_Tick;
        img_ref.MouseWheel += ImgRef_MouseWheel;
        
        _lastWindowState = this.WindowState;
        this.Resize += imgViewForm_Resize; //tá pegando toda vez que um imbecil redimenciona a janela
        img_ref.Anchor = AnchorStyles.None;
        this.KeyPreview = true;
        this.KeyDown += imgViewForm_KeyDown;

        this.AllowDrop = true;
        this.DragEnter += imgViewForm_DragEnter;
        this.DragDrop += imgViewForm_DragDrop;
    }

    #region Detections

    private void imgViewForm_Resize(object sender, EventArgs e)
    {
        if (this.WindowState != _lastWindowState)
        {
            _lastWindowState = this.WindowState;
            if (this.WindowState == FormWindowState.Maximized)
            {
                isMaximized = true;
                LoadImage(imageFiles[currentImageIndex]);
            }
            else if (this.WindowState == FormWindowState.Normal)
            {
                isMaximized = false;
                LoadImage(imageFiles[currentImageIndex]);
            }
        }
        
        CentralizePictureBox();
    }
    
    #endregion
    
    #region FormConfig
    
    private void CentralizePictureBox()
    {
        if (img_ref.Image != null && this.ClientSize.Width > 0 && this.ClientSize.Height > 0)
        {
            this.Update();
            Application.DoEvents();
            
            //calcula o centro do caralho da janelakk
            int x = (this.ClientSize.Width - img_ref.Width) / 2;
            int y = (this.ClientSize.Height - img_ref.Height) / 2;

            //ae mete a porra lá dentro do cu
            img_ref.Location = new Point(x, y);
        }
    }
    
    private void ConfigureForm()
    {
        this.Text = "Fast Image View";
        this.BackColor = Color.Black;
        this.DoubleBuffered = true;
        
        SetWindowDarkMode(this.Handle);
        CreateContextMenu();
    }
    
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    private void SetWindowDarkMode(IntPtr handle)
    {
        if (Environment.OSVersion.Version.Major >= 10)
        {
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
        }
    }
    #endregion

    #region StartLoading
    private void LoadCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            LoadImage(args[1]);
        }else if (imageFiles == null)
        {
            OpenToolTripMenu();
        }
    }

    private void OpenToolTripMenu()
    {
        using (var openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                LoadImage(openFileDialog.FileName);
            }
        }
    }
    #endregion
    
    #region InputSystem
    private void imgViewForm_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left:
                LoadPreviousImage();
                break;
            case Keys.Right:
                LoadNextImage();
                break;
        }
    }
    #endregion
    
    #region ImagesLoadSystem
    private void LoadPreviousImage()
    {
        if (imageFiles == null || currentImageIndex < 0) return;

        currentImageIndex--;
        if (currentImageIndex < 0)
            currentImageIndex = imageFiles.Length - 1;
        
        LoadImage(imageFiles[currentImageIndex]);
    }

    private void LoadNextImage()
    {
        if (imageFiles == null || currentImageIndex < 0) return;

        currentImageIndex++;
        if (currentImageIndex >= imageFiles.Length)
            currentImageIndex = 0;
        
        LoadImage(imageFiles[currentImageIndex]);
    }

    private void LoadImageList(string initialPath)
    {
        currentDirectory = Path.GetDirectoryName(initialPath);
        imageFiles = Directory.GetFiles(currentDirectory)
            .Where(f => f.ToLower().EndsWith(".jpg") ||
                        f.ToLower().EndsWith(".jpeg") ||
                        f.ToLower().EndsWith(".png") ||
                        f.ToLower().EndsWith(".bmp") ||
                        f.ToLower().EndsWith(".gif"))
            .OrderBy(f => f)
            .ToArray();

        currentImageIndex = Array.IndexOf(imageFiles, initialPath);
    }
    
    public void LoadImage(string path)
    {
        try
        {
            if (imageFiles == null || Path.GetDirectoryName(path) != currentDirectory)
            {
                LoadImageList(path);
            }
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                originalImage = Image.FromStream(stream);
                imgConfigOnLoad(originalImage, path);
                zoomFactor = 1.0f;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error in load - {ex.Message} file", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    #endregion
    
    #region imgConfig
    private void imgConfigOnLoad(Image img, string showPath)
    {
        int screenWidth = Screen.PrimaryScreen.Bounds.Width;
        int screenHeight = Screen.PrimaryScreen.Bounds.Height;
        originalImage = img;
        zoomFactor = 1.0f;

        if (isMaximized)
        {
            float aspectRatio = (float)img.Width / img.Height;
            int newWidth = this.ClientSize.Width;
            int newHeight = (int)(newWidth / aspectRatio);

            if (newWidth > this.ClientSize.Height)
            {
                newHeight = this.ClientSize.Height;
                newWidth = (int)(newHeight * aspectRatio);
            }
            
            img_ref.Image = ResizeImage(img, newWidth, newHeight);
            img_ref.Size = new Size(newWidth, newHeight);
        }
        else
        {
            if (img.Width > screenWidth || img.Height > screenHeight)
            {
                float aspectRatio = (float)img.Width / img.Height;
                int newImgWidth = screenWidth / 2;
                int newImgHeight = (int)(newImgWidth / aspectRatio);

                if (newImgHeight > screenHeight / 2)
                {
                    newImgHeight = screenHeight / 2;
                    newImgWidth = (int)(newImgHeight * aspectRatio);
                }

                img_ref.Image = ResizeImage(img, newImgWidth, newImgHeight);
                //img_ref.Image = img;
                img_ref.Size = new Size(newImgWidth, newImgHeight);
                //this.Size = new Size(newImgWidth, newImgHeight);
            }
            else
            {
                img_ref.Image = img;
                img_ref.Size = img.Size;
                //this.Size = img.Size;
            }
        }
        this.Update();
        //this.CenterToScreen();
        CentralizePictureBox();
        this.Text = $"[{currentImageIndex + 1}/{imageFiles.Length}] - {Path.GetFileName(showPath)} - Fast Image View";
    }
    
    private Image ResizeImage(Image image, int width, int height, bool highQuality = true)
    {
        var destImage = new Bitmap(width, height);

        using (var graphics = Graphics.FromImage(destImage))
        {
            if (highQuality)
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            }
            else
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            }
            
            graphics.DrawImage(image, 0, 0, width, height);
        }

        return destImage;
    }
    #endregion

    #region Zoom System

    private void ImgRef_MouseWheel(object sender, MouseEventArgs e)
    {
        if (ModifierKeys == Keys.Control)
        {
            float oldZoom = zoomFactor;

            if (e.Delta > 0)
            {
                zoomFactor += ZOOM_INCREMENT;
                if (zoomFactor > MAX_ZOOM) zoomFactor = MAX_ZOOM;
            }
            else //Zoom out
            {
                zoomFactor -= ZOOM_INCREMENT;
                if (zoomFactor < MIN_ZOOM) zoomFactor = MIN_ZOOM;
            }

            lastMousePosition = e.Location;
            ApplyZoom();
            zoomTimer.Stop();
            zoomTimer.Start();
        }
    }

    private void ApplyZoom(bool highQuality = false)
    {
        if (originalImage == null || img_ref.Image == null)return;
        
        int newWidth = (int)(originalImage.Width * zoomFactor);
        int newHeight = (int)(originalImage.Height * zoomFactor);

        img_ref.Image = ResizeImage(originalImage, newWidth, newHeight, highQuality);
        img_ref.Size = new Size(newWidth, newHeight);
        
        //ajust
        AdjustScrollPosition();
        CentralizePictureBox();
    }

    private void AdjustScrollPosition()
    {
        if (img_ref.Parent is ScrollableControl panel)
        {
            int newX = (int)(lastMousePosition.X * zoomFactor) - (panel.ClientSize.Width / 2);
            int newY = (int)(lastMousePosition.Y * zoomFactor) - (panel.ClientSize.Height / 2);
            
            panel.AutoScrollPosition = new Point(
                Math.Max(0, newX),
                Math.Max(0, newY)
            );
        }
    }
    
    private void ZoomTimer_Tick(object sender, EventArgs e)
    {
        zoomTimer.Stop();
        // Atualiza qualidade após parar de zoom
        if (zoomFactor > 1.0f)
        {
            ApplyZoom(true); // Alta qualidade para zoom in
        }
    }
    #endregion

    #region ContextMenu System

    private void CreateContextMenu()
    {
        contextMenu = new ContextMenuStrip();

        var openImgMenu = new ToolStripMenuItem("Open image");
        openImgMenu.Click += (s, e) => OpenToolTripMenu();

        var quitBtMenu = new ToolStripMenuItem("Quit");
        quitBtMenu.Click += (s, e) => this.Close();

        contextMenu.Items.Add(openImgMenu);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitBtMenu);

        img_ref.ContextMenuStrip = contextMenu;
        this.ContextMenuStrip = contextMenu;
    }

    private void img_ref_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            contextMenu.Show(Cursor.Position);
        }
    }
    #endregion

    #region DragAndDrop System

    private void imgViewForm_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1 && IsImageFile(files[0]))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }
    }
    
    private void imgViewForm_DragDrop(object sender, DragEventArgs e)
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
        {
            LoadImage(files[0]); // load first image
        }
    }
    
    private bool IsImageFile(string path)
    {
        string[] validExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        string ext = Path.GetExtension(path).ToLower();
        return validExtensions.Contains(ext);
    }
    #endregion
}