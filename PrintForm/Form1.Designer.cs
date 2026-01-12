namespace PrintForm
{
    partial class Form1
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            labelPrinter = new Label();
            comboPrinters = new ComboBox();
            labelFile = new Label();
            txtFilePath = new TextBox();
            btnBrowse = new Button();
            btnConfig = new Button();
            btnPrint = new Button();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            printPreviewControl1 = new SafePrintPreviewControl();
            printDocument1 = new System.Drawing.Printing.PrintDocument();
            openFileDialog1 = new OpenFileDialog();
            pageSetupDialog1 = new PageSetupDialog();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // labelPrinter
            // 
            labelPrinter.AutoSize = true;
            labelPrinter.Location = new Point(308, 27);
            labelPrinter.Name = "labelPrinter";
            labelPrinter.Size = new Size(55, 20);
            labelPrinter.TabIndex = 0;
            labelPrinter.Text = "Printer:";
            labelPrinter.Click += label1_Click;
            // 
            // comboPrinters
            // 
            comboPrinters.DropDownStyle = ComboBoxStyle.DropDownList;
            comboPrinters.FormattingEnabled = true;
            comboPrinters.Location = new Point(398, 23);
            comboPrinters.Name = "comboPrinters";
            comboPrinters.Size = new Size(231, 28);
            comboPrinters.TabIndex = 1;
            // 
            // labelFile
            // 
            labelFile.AutoSize = true;
            labelFile.Location = new Point(308, 64);
            labelFile.Name = "labelFile";
            labelFile.Size = new Size(76, 20);
            labelFile.TabIndex = 2;
            labelFile.Text = "Dokumen:";
            labelFile.Click += labelFile_Click;
            // 
            // txtFilePath
            // 
            txtFilePath.Location = new Point(398, 61);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.ReadOnly = true;
            txtFilePath.Size = new Size(231, 27);
            txtFilePath.TabIndex = 3;
            txtFilePath.TextChanged += txtFilePath_TextChanged;
            // 
            // btnBrowse
            // 
            btnBrowse.Location = new Point(535, 104);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(94, 29);
            btnBrowse.TabIndex = 4;
            btnBrowse.Text = "Pilih File...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += btnBrowse_Click;
            // 
            // btnConfig
            // 
            btnConfig.Location = new Point(308, 370);
            btnConfig.Name = "btnConfig";
            btnConfig.Size = new Size(140, 29);
            btnConfig.TabIndex = 5;
            btnConfig.Text = "Konfigurasi Cetak";
            btnConfig.UseVisualStyleBackColor = true;
            btnConfig.Click += btnConfig_Click;
            // 
            // btnPrint
            // 
            btnPrint.Location = new Point(535, 370);
            btnPrint.Name = "btnPrint";
            btnPrint.Size = new Size(94, 29);
            btnPrint.TabIndex = 6;
            btnPrint.Text = "Print";
            btnPrint.UseVisualStyleBackColor = true;
            btnPrint.Click += btnPrint_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip1.Location = new Point(0, 424);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(657, 26);
            statusStrip1.TabIndex = 7;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(38, 20);
            statusLabel.Text = "Siap";
            // 
            // printPreviewControl1
            // 
            printPreviewControl1.Dock = DockStyle.Left;
            printPreviewControl1.Document = printDocument1;
            printPreviewControl1.Location = new Point(0, 0);
            printPreviewControl1.Name = "printPreviewControl1";
            printPreviewControl1.Size = new Size(296, 424);
            printPreviewControl1.TabIndex = 8;
            printPreviewControl1.Zoom = 0.2635294117647059D;
            // 
            // printDocument1
            // 
            printDocument1.BeginPrint += printDocument1_BeginPrint;
            printDocument1.EndPrint += printDocument1_EndPrint;
            printDocument1.PrintPage += printDocument1_PrintPage;
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(657, 450);
            Controls.Add(printPreviewControl1);
            Controls.Add(statusStrip1);
            Controls.Add(btnPrint);
            Controls.Add(btnConfig);
            Controls.Add(btnBrowse);
            Controls.Add(txtFilePath);
            Controls.Add(labelFile);
            Controls.Add(comboPrinters);
            Controls.Add(labelPrinter);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelPrinter;
        private ComboBox comboPrinters;
        private Label labelFile;
        private TextBox txtFilePath;
        private Button btnBrowse;
        private Button btnConfig;
        private Button btnPrint;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private SafePrintPreviewControl printPreviewControl1;
        private System.Drawing.Printing.PrintDocument printDocument1;
        private OpenFileDialog openFileDialog1;
        private PageSetupDialog pageSetupDialog1;
    }
}
