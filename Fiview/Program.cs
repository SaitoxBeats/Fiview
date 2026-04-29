namespace Fiview;

static class Program
{
    private static imgView_Form? _imageForm;
    private static init_Form? _initForm;

    [STAThread]
    private static void Main(string[] args)
    {
        var initialImagePath = args.Length > 0 ? args[0] : null;

        using var mutex = new Mutex(true, SingleInstanceServer.MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SingleInstanceServer.SendImagePath(string.IsNullOrWhiteSpace(initialImagePath)
                ? string.Empty
                : Path.GetFullPath(initialImagePath));
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var server = new SingleInstanceServer(OpenImageOnUiThread);

        if (!string.IsNullOrWhiteSpace(initialImagePath))
        {
            Application.Run(ShowImageForm(initialImagePath));
        }
        else
        {
            Application.Run(ShowInitForm());
        }
    }

    private static void OpenImageOnUiThread(string imagePath)
    {
        var targetForm = Application.OpenForms.Cast<Form>().FirstOrDefault();
        if (targetForm is null || targetForm.IsDisposed)
        {
            return;
        }

        targetForm.BeginInvoke((Action)(() =>
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                RestoreForm(_imageForm ?? (Form?)_initForm);
                return;
            }

            ShowImageForm(imagePath);
        }));
    }

    private static init_Form ShowInitForm()
    {
        _initForm = new init_Form();
        _initForm.OpenImageRequested += (_, _) => ShowImageForm();
        return _initForm;
    }

    private static imgView_Form ShowImageForm(string? imagePath = null)
    {
        if (_imageForm is { IsDisposed: false })
        {
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                _imageForm.LoadImage(imagePath);
            }

            RestoreForm(_imageForm);
            return _imageForm;
        }

        _imageForm = new imgView_Form(imagePath);
        _imageForm.FormClosed += (_, _) =>
        {
            _imageForm = null;
            _initForm?.Close();
        };

        _initForm?.Hide();
        _imageForm.Show();
        return _imageForm;
    }

    private static void RestoreForm(Form? form)
    {
        if (form is null || form.IsDisposed)
        {
            return;
        }

        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();
        form.Activate();
        form.BringToFront();
    }
}
