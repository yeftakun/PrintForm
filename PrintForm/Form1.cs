using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace PrintForm
{
    public partial class Form1 : Form
    {
        // Menyimpan gambar yang akan di-preview / di-print via PrintDocument
        private Image? _imageToPrint;

        public Form1()
        {
            InitializeComponent();
        }

        // =========================
        // EVENT FORM LOAD
        // =========================
        private void Form1_Load(object sender, EventArgs e)
        {
            // Isi comboPrinters dengan printer yang terpasang di Windows
            comboPrinters.Items.Clear();

            foreach (string printerName in PrinterSettings.InstalledPrinters)
            {
                comboPrinters.Items.Add(printerName);
            }

            if (comboPrinters.Items.Count > 0)
            {
                var defaultSettings = new PrinterSettings();
                comboPrinters.SelectedItem = defaultSettings.PrinterName;
            }

            // Hubungkan PrintDocument dengan PrintPreviewControl
            printPreviewControl1.Document = printDocument1;
            printPreviewControl1.AutoZoom = true;

            statusLabel.Text = "Siap. Pilih printer dan dokumen.";
        }

        // =========================
        // PILIH FILE DOKUMEN
        // =========================
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "Pilih dokumen untuk dicetak";
            openFileDialog1.Filter =
                "Semua dokumen|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.jpg;*.jpeg;*.png;*.bmp|" +
                "Gambar|*.jpg;*.jpeg;*.png;*.bmp|" +
                "Semua file|*.*";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string path = openFileDialog1.FileName;
                txtFilePath.Text = path;
                statusLabel.Text = "Dokumen dipilih: " + openFileDialog1.SafeFileName;

                // Reset image lama
                _imageToPrint?.Dispose();
                _imageToPrint = null;

                // Jika file adalah gambar → load ke _imageToPrint untuk preview
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                {
                    _imageToPrint = Image.FromFile(path);
                }

                // Paksa refresh preview (akan panggil PrintPage)
                printPreviewControl1.InvalidatePreview();
            }
        }

        // =========================
        // KONFIGURASI HALAMAN (H/P, UKURAN KERTAS, MARGIN)
        // =========================
        private void btnConfig_Click(object sender, EventArgs e)
        {
            // Pastikan printer di-set ke printer yang dipilih
            if (comboPrinters.SelectedItem is string selectedPrinter)
            {
                printDocument1.PrinterSettings.PrinterName = selectedPrinter;
            }

            pageSetupDialog1.Document = printDocument1;

            var result = pageSetupDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                statusLabel.Text = "Konfigurasi halaman diperbarui.";
                printPreviewControl1.InvalidatePreview();
            }
            else
            {
                statusLabel.Text = "Konfigurasi halaman dibatalkan.";
            }
        }

        // =========================
        // PRINT DOKUMEN
        //  - GAMBAR → PrintDocument (preview & konfigurasi ikut)
        //  - NON-GAMBAR → lewat aplikasi default (printto)
        // =========================
        private void btnPrint_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboPrinters.SelectedItem == null)
                {
                    MessageBox.Show("Silakan pilih printer terlebih dahulu.",
                                    "Informasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtFilePath.Text))
                {
                    MessageBox.Show("Silakan pilih dokumen yang akan dicetak.",
                                    "Informasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string printerName = comboPrinters.SelectedItem!.ToString()!;
                string filePath = txtFilePath.Text;

                // CABANG 1: FILE GAMBAR (ada preview) → pakai PrintDocument
                if (_imageToPrint != null)
                {
                    printDocument1.PrinterSettings.PrinterName = printerName;

                    if (!printDocument1.PrinterSettings.IsValid)
                    {
                        MessageBox.Show("Printer yang dipilih tidak valid atau tidak tersedia.",
                                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        statusLabel.Text = "Printer tidak valid.";
                        return;
                    }

                    statusLabel.Text = "Mengirim dokumen gambar ke printer...";
                    printDocument1.Print();   // akan memicu PrintPage
                    return;
                }

                // CABANG 2: BUKAN GAMBAR → kirim ke aplikasi default (PDF reader, Word, dll.)
                statusLabel.Text = "Mengirim dokumen ke printer (via aplikasi default)...";

                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "printto",
                    Arguments = $"\"{printerName}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                var p = Process.Start(psi);

                if (p != null)
                {
                    // Tunggu sebentar (banyak aplikasi print synchronously)
                    p.WaitForExit(10000); // maks 10 detik
                }

                statusLabel.Text = "Dokumen dikirim ke printer.";
                MessageBox.Show("Dokumen sudah dikirim ke printer melalui aplikasi bawaan dokumen.",
                                "Informasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Terjadi kesalahan saat mencetak dokumen.\n\n{ex.Message}",
                                "Error Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Gagal mengirim dokumen ke printer.";
            }
        }

        // =========================
        // EVENT PRINTDOCUMENT (PREVIEW & PRINT)
        // =========================
        private void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_imageToPrint == null)
            {
                using Font font = new Font("Segoe UI", 12);
                e.Graphics.DrawString("Tidak ada dokumen gambar yang dipilih.",
                                      font, Brushes.Black,
                                      e.MarginBounds.Location);
                e.HasMorePages = false;
                return;
            }

            Rectangle m = e.MarginBounds;

            // Rasio aspek gambar dan halaman
            float imgRatio = (float)_imageToPrint.Width / _imageToPrint.Height;
            float pageRatio = (float)m.Width / m.Height;

            Rectangle drawRect;

            if (imgRatio > pageRatio)
            {
                // Gambar lebih lebar
                int drawWidth = m.Width;
                int drawHeight = (int)(m.Width / imgRatio);
                int drawY = m.Top + (m.Height - drawHeight) / 2;
                drawRect = new Rectangle(m.Left, drawY, drawWidth, drawHeight);
            }
            else
            {
                // Gambar lebih tinggi
                int drawHeight = m.Height;
                int drawWidth = (int)(m.Height * imgRatio);
                int drawX = m.Left + (m.Width - drawWidth) / 2;
                drawRect = new Rectangle(drawX, m.Top, drawWidth, drawHeight);
            }

            e.Graphics.DrawImage(_imageToPrint, drawRect);

            e.HasMorePages = false;
        }

        private void printDocument1_BeginPrint(object sender, PrintEventArgs e)
        {
            statusLabel.Text = "Proses print dimulai...";
        }

        private void printDocument1_EndPrint(object sender, PrintEventArgs e)
        {
            if (e.PrintAction == PrintAction.PrintToPrinter)
            {
                MessageBox.Show("Dokumen berhasil dikirim ke printer.",
                                "Informasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                statusLabel.Text = "Selesai mengirim ke printer.";
            }
            else
            {
                statusLabel.Text = "Print dibatalkan atau tidak dikirim ke printer.";
            }
        }

        // =========================
        // STUB EVENT LAMA (JIKA MASIH TERIKAT DI DESIGNER)
        // =========================
        private void label1_Click(object sender, EventArgs e)
        {
            // Dibiarkan kosong; hanya agar designer tidak error
        }

        private void labelFile_Click(object sender, EventArgs e)
        {
            // Dibiarkan kosong; hanya agar designer tidak error
        }

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // Dibiarkan kosong; hanya agar designer tidak error
        }

        private void txtFilePath_TextChanged(object sender, EventArgs e)
        {
            // Dibiarkan kosong; hanya agar designer tidak error
        }
    }
}
