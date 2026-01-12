using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace PrintForm
{
    public partial class Form1 : Form
    {
        // Menyimpan gambar yang akan di-preview / di-print via PrintDocument
        private Image? _imageToPrint;
        private static readonly HttpClient Http = CreateHttpClient();
        private const string ServerBaseUrl = "http://127.0.0.1:3000";
        private string? _clientId;
        private System.Windows.Forms.Timer? _heartbeatTimer;
        private System.Windows.Forms.Timer? _pingTimer;
        private bool _registerInProgress;
        private System.Windows.Forms.Timer? _jobPollTimer;
        private bool _jobProcessing;
        private string? _activeJobId;
        private string? _activeJobTempPath;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private const int JobPollIntervalMs = 5000;

        public Form1()
        {
            InitializeComponent();
            FormClosing += Form1_FormClosing;
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false
            };
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        // =========================
        // EVENT FORM LOAD
        // =========================
        private async void Form1_Load(object sender, EventArgs e)
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

            statusLabel.Text = "Siap. Pilih printer dan dokumen.";

            await EnsureRegisteredAsync();
            StartHeartbeat();
            StartPingPolling();
            StartJobPolling();
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
                    using var img = Image.FromFile(path);
                    _imageToPrint = new Bitmap(img);
                }

                ApplyPreviewZoom();
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
            if (!string.IsNullOrWhiteSpace(_activeJobId))
            {
                var jobId = _activeJobId;
                _activeJobId = null;
                _jobProcessing = false;
                if (!string.IsNullOrWhiteSpace(_activeJobTempPath))
                {
                    TryDeleteTempFile(_activeJobTempPath);
                    _activeJobTempPath = null;
                }
                _ = UpdateJobStatusAsync(jobId, e.PrintAction == PrintAction.PrintToPrinter ? "done" : "failed");
            }

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

        private async System.Threading.Tasks.Task RegisterClientAsync()
        {
            if (_registerInProgress)
            {
                return;
            }

            _registerInProgress = true;
            try
            {
                statusLabel.Text = "Mencoba terhubung ke server...";
                var printers = PrinterSettings.InstalledPrinters.Cast<string>().ToArray();
                var payload = new
                {
                    clientId = _clientId,
                    name = Environment.MachineName,
                    printers
                };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync($"{ServerBaseUrl}/api/clients/register", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    statusLabel.Text = $"Gagal terhubung ke server ({(int)response.StatusCode}).";
                    return;
                }

                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("id", out var id))
                {
                    _clientId = id.GetString();
                }

                statusLabel.Text = "Terhubung ke server.";
            }
            catch
            {
                statusLabel.Text = "Tidak bisa terhubung ke server.";
            }
            finally
            {
                _registerInProgress = false;
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatTimer = new System.Windows.Forms.Timer
            {
                Interval = 30000
            };
            _heartbeatTimer.Tick += async (_, _) => await SendHeartbeatAsync();
            _heartbeatTimer.Start();
        }

        private void StartPingPolling()
        {
            _pingTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000
            };
            _pingTimer.Tick += async (_, _) => await PollPingAsync();
            _pingTimer.Start();
        }

        private void StartJobPolling()
        {
            _jobPollTimer = new System.Windows.Forms.Timer
            {
                Interval = JobPollIntervalMs
            };
            _jobPollTimer.Tick += async (_, _) => await PollJobsAsync();
            _jobPollTimer.Start();
        }

        private async System.Threading.Tasks.Task SendHeartbeatAsync()
        {
            await EnsureRegisteredAsync();
            if (string.IsNullOrWhiteSpace(_clientId))
            {
                return;
            }

            try
            {
                var payload = new { clientId = _clientId };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync($"{ServerBaseUrl}/api/clients/heartbeat", content);
                if (!response.IsSuccessStatusCode)
                {
                    statusLabel.Text = "Koneksi server terputus.";
                }
            }
            catch
            {
                statusLabel.Text = "Koneksi server terputus.";
            }
        }

        private async System.Threading.Tasks.Task PollPingAsync()
        {
            await EnsureRegisteredAsync();
            if (string.IsNullOrWhiteSpace(_clientId))
            {
                return;
            }

            try
            {
                using var response = await Http.GetAsync($"{ServerBaseUrl}/api/clients/{_clientId}/ping");
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("items", out var items))
                {
                    return;
                }

                if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
                {
                    return;
                }

                var count = items.GetArrayLength();
                statusLabel.Text = "Ping diterima dari server.";
                MessageBox.Show($"Ping diterima dari server ({count}).",
                                "Ping", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                // Abaikan jika server tidak bisa dihubungi
            }
        }

        private async System.Threading.Tasks.Task PollJobsAsync()
        {
            await EnsureRegisteredAsync();
            if (string.IsNullOrWhiteSpace(_clientId) || _jobProcessing)
            {
                return;
            }

            try
            {
                using var response = await Http.GetAsync($"{ServerBaseUrl}/api/jobs?clientId={Uri.EscapeDataString(_clientId)}&status=ready");
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                var jobs = JsonSerializer.Deserialize<List<PrintJob>>(body, JsonOptions);
                if (jobs == null || jobs.Count == 0)
                {
                    return;
                }

                await ProcessJobAsync(jobs[0]);
            }
            catch
            {
                // Abaikan jika server tidak bisa dihubungi
            }
        }

        private async System.Threading.Tasks.Task ProcessJobAsync(PrintJob job)
        {
            if (_jobProcessing)
            {
                return;
            }

            _jobProcessing = true;
            _activeJobId = job.Id;
            statusLabel.Text = $"Memproses job {job.Id}...";
            var waitForEndPrint = false;

            try
            {
                await UpdateJobStatusAsync(job.Id, "printing");

                var downloadPath = await DownloadJobFileAsync(job.Id, job.OriginalName);
                if (string.IsNullOrWhiteSpace(downloadPath))
                {
                    await UpdateJobStatusAsync(job.Id, "failed");
                    _jobProcessing = false;
                    _activeJobId = null;
                    return;
                }

                _activeJobTempPath = downloadPath;

                // Reset preview
                _imageToPrint?.Dispose();
                _imageToPrint = null;

                string ext = Path.GetExtension(downloadPath).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                {
                    using var img = Image.FromFile(downloadPath);
                    _imageToPrint = new Bitmap(img);
                }

                txtFilePath.Text = downloadPath;
                ApplyPreviewZoom();
                printPreviewControl1.InvalidatePreview();

                ApplyPrintConfig(job);

                if (_imageToPrint != null)
                {
                    waitForEndPrint = true;
                    printDocument1.Print();
                    return;
                }

                await PrintNonImageAsync(downloadPath);
                TryDeleteTempFile(downloadPath);
                _activeJobTempPath = null;
                await UpdateJobStatusAsync(job.Id, "done");
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(job.Id))
                {
                    await UpdateJobStatusAsync(job.Id, "failed");
                }
            }
            finally
            {
                if (!waitForEndPrint)
                {
                    _jobProcessing = false;
                    _activeJobId = null;
                    if (!string.IsNullOrWhiteSpace(_activeJobTempPath))
                    {
                        TryDeleteTempFile(_activeJobTempPath);
                        _activeJobTempPath = null;
                    }
                }
            }
        }

        private void ApplyPrintConfig(PrintJob job)
        {
            var printerName = comboPrinters.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                var defaultSettings = new PrinterSettings();
                printerName = defaultSettings.PrinterName;
            }

            printDocument1.PrinterSettings.PrinterName = printerName ?? string.Empty;
            if (!printDocument1.PrinterSettings.IsValid)
            {
                statusLabel.Text = "Printer tidak valid.";
                return;
            }

            if (job.PrintConfig != null)
            {
                if (job.PrintConfig.Copies >= 1 && job.PrintConfig.Copies <= 999)
                {
                    printDocument1.PrinterSettings.Copies = (short)job.PrintConfig.Copies;
                }

                if (!string.IsNullOrWhiteSpace(job.PrintConfig.PaperSize))
                {
                    foreach (PaperSize size in printDocument1.PrinterSettings.PaperSizes)
                    {
                        if (string.Equals(size.PaperName, job.PrintConfig.PaperSize, StringComparison.OrdinalIgnoreCase))
                        {
                            printDocument1.DefaultPageSettings.PaperSize = size;
                            break;
                        }
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task<string?> DownloadJobFileAsync(string jobId, string originalName)
        {
            using var response = await Http.GetAsync($"{ServerBaseUrl}/api/jobs/{Uri.EscapeDataString(jobId)}/download");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var safeName = Path.GetFileName(originalName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = jobId;
            }

            var filePath = Path.Combine(Path.GetTempPath(), $"{jobId}_{safeName}");
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
            return filePath;
        }

        private async System.Threading.Tasks.Task PrintNonImageAsync(string filePath)
        {
            var printerName = comboPrinters.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(printerName))
            {
                var defaultSettings = new PrinterSettings();
                printerName = defaultSettings.PrinterName;
            }

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
                p.WaitForExit(10000);
            }

            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void TryDeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Abaikan jika file tidak bisa dihapus
            }
        }

        private async System.Threading.Tasks.Task UpdateJobStatusAsync(string jobId, string status)
        {
            var payload = new { status };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{ServerBaseUrl}/api/jobs/{Uri.EscapeDataString(jobId)}")
            {
                Content = content
            };
            using var response = await Http.SendAsync(request);
        }

        private async System.Threading.Tasks.Task EnsureRegisteredAsync()
        {
            if (!string.IsNullOrWhiteSpace(_clientId))
            {
                return;
            }

            await RegisterClientAsync();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyPreviewZoom();
        }

        private void ApplyPreviewZoom()
        {
            if (printPreviewControl1.ClientSize.Width <= 0 || printPreviewControl1.ClientSize.Height <= 0)
            {
                return;
            }

            printPreviewControl1.AutoZoom = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopTimers();
            if (string.IsNullOrWhiteSpace(_clientId))
            {
                return;
            }

            try
            {
                var payload = new { clientId = _clientId };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                Http.PostAsync($"{ServerBaseUrl}/api/clients/unregister", content)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // Abaikan kegagalan saat shutdown
            }
        }

        private void StopTimers()
        {
            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Stop();
                _heartbeatTimer.Dispose();
            }

            if (_pingTimer != null)
            {
                _pingTimer.Stop();
                _pingTimer.Dispose();
            }

            if (_jobPollTimer != null)
            {
                _jobPollTimer.Stop();
                _jobPollTimer.Dispose();
            }
        }

        private sealed class PrintJob
        {
            public string Id { get; set; } = string.Empty;
            public string OriginalName { get; set; } = string.Empty;
            public PrintConfig? PrintConfig { get; set; }
        }

        private sealed class PrintConfig
        {
            public string? PaperSize { get; set; }
            public int Copies { get; set; }
        }
    }
}
