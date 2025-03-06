using System.ComponentModel;

namespace Fiview;

partial class init_Form
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(init_Form));
        button1 = new System.Windows.Forms.Button();
        label1 = new System.Windows.Forms.Label();
        button2 = new System.Windows.Forms.Button();
        SuspendLayout();
        // 
        // button1
        // 
        button1.Font = new System.Drawing.Font("Calibri", 15F);
        button1.Location = new System.Drawing.Point(9, 105);
        button1.Name = "button1";
        button1.Size = new System.Drawing.Size(253, 50);
        button1.TabIndex = 0;
        button1.Text = "Open Image";
        button1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        button1.UseVisualStyleBackColor = true;
        button1.Click += button1_Click;
        // 
        // label1
        // 
        label1.Font = new System.Drawing.Font("Consolas", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)0));
        label1.ForeColor = System.Drawing.Color.Lime;
        label1.Location = new System.Drawing.Point(9, 15);
        label1.Name = "label1";
        label1.Size = new System.Drawing.Size(478, 87);
        label1.TabIndex = 1;
        label1.Text = "Fast Image View";
        label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // button2
        // 
        button2.Font = new System.Drawing.Font("Calibri", 15F);
        button2.Location = new System.Drawing.Point(9, 161);
        button2.Name = "button2";
        button2.Size = new System.Drawing.Size(253, 50);
        button2.TabIndex = 0;
        button2.Text = "About";
        button2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        button2.UseVisualStyleBackColor = true;
        button2.Click += button2_Click;
        // 
        // init_Form
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        BackColor = System.Drawing.Color.Black;
        ClientSize = new System.Drawing.Size(496, 473);
        Controls.Add(button2);
        Controls.Add(button1);
        Controls.Add(label1);
        Icon = ((System.Drawing.Icon)resources.GetObject("$this.Icon"));
        Text = "init_Form";
        ResumeLayout(false);
    }

    private System.Windows.Forms.Button button2;

    private System.Windows.Forms.Label label1;

    private System.Windows.Forms.Button button1;

    #endregion
}