namespace Fiview;

partial class imgView_Form
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(imgView_Form));
        img_ref = new System.Windows.Forms.PictureBox();
        contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
        ((System.ComponentModel.ISupportInitialize)img_ref).BeginInit();
        SuspendLayout();
        // 
        // img_ref
        // 
        img_ref.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
        img_ref.Dock = System.Windows.Forms.DockStyle.Fill;
        img_ref.Location = new System.Drawing.Point(0, 0);
        img_ref.Margin = new System.Windows.Forms.Padding(0);
        img_ref.Name = "img_ref";
        img_ref.Size = new System.Drawing.Size(784, 561);
        img_ref.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
        img_ref.TabIndex = 0;
        img_ref.TabStop = false;
        img_ref.WaitOnLoad = true;
        // 
        // contextMenuStrip1
        // 
        contextMenuStrip1.Name = "contextMenuStrip1";
        contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
        // 
        // imgView_Form
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        BackColor = System.Drawing.Color.Black;
        ClientSize = new System.Drawing.Size(784, 561);
        Controls.Add(img_ref);
        Icon = ((System.Drawing.Icon)resources.GetObject("$this.Icon"));
        Text = "imgView_Form";
        ((System.ComponentModel.ISupportInitialize)img_ref).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;

    private System.Windows.Forms.PictureBox img_ref;

    #endregion
}