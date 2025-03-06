using System.Runtime.InteropServices;

namespace Fiview;

public partial class init_Form : Form
{
    public init_Form()
    {
        InitializeComponent();
        ConfigureForm();
    }

    private void button1_Click(object sender, EventArgs e)
    {
        imgView_Form imgViewForm = new imgView_Form();
        imgViewForm.Show();
        this.Hide();
        imgViewForm.FormClosed += onForm1Closed;
    }

    private void onForm1Closed(object sender, FormClosedEventArgs e)
    {
        this.Close();
    }
    
    private void ConfigureForm()
    {
        this.Text = "Fast Image View";
        this.BackColor = Color.Black;
        this.DoubleBuffered = true;
        
        SetWindowDarkMode(this.Handle);
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

    private void button2_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Hi my name is Saitox :) - https://saitoxbeats.github.io/", "Hey!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
    }
}