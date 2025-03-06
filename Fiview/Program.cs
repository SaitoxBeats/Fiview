using Microsoft.VisualBasic;
using System.Runtime.InteropServices;

namespace Fiview;

static class Program
{
    private static Mutex _mutex = new Mutex(true, "{FASTIMAGEVIEW-UNIQUE-MUTEX}");
    
    [STAThread]
    static void Main(string[] args)
    {
        if (_mutex.WaitOne(TimeSpan.Zero, true))
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string imagePath = args.Length > 0 ? args[0] : null;

            if (!string.IsNullOrEmpty(imagePath))
            {
                Application.Run(new imgView_Form(imagePath));
            }
            else
            {
                Application.Run(new init_Form());
            }
            
            _mutex.ReleaseMutex();
        }
        else
        {
            NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
        }
    }
}

internal class NativeMethods
{
    public const int HWND_BROADCAST = 0xffff;
    public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME");
    
    [DllImport("user32")]
    public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    
    [DllImport("user32")]
    public static extern int RegisterWindowMessage(string message);
}