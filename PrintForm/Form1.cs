using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace PrintForm
{
    public partial class Form1 : Form
    {
        // Menyimpan gambar yang akan di-print via PrintDocument
        private Image? _imageToPrint;
        private static readonly HttpClient Http = CreateHttpClient();
        private const string ServerBaseUrl = "http://127.0.0.1:3000";
        private string? _clientId;
        private System.Windows.Forms.Timer? _heartbeatTimer;
        private System.Windows.Forms.Timer? _pingTimer;
        private bool _registerInProgress;
        private bool _jobProcessing;
        private string? _activeJobId;
        private string? _activeJobTempPath;
        private JobListForm? _jobListForm;

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

            statusLabel.Text = "Siap. Pilih printer lalu buka Print Job.";

            await EnsureRegisteredAsync();
            StartHeartbeat();
            StartPingPolling();
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
            }
            else
            {
                statusLabel.Text = "Konfigurasi halaman dibatalkan.";
            }
        }

        private void btnJobList_Click(object sender, EventArgs e)
        {
            if (_jobListForm == null || _jobListForm.IsDisposed)
            {
                _jobListForm = new JobListForm(Http, ServerBaseUrl, () => _clientId, PrintJobFromListAsync);
            }

            _jobListForm.Show();
            _jobListForm.BringToFront();
        }

        // =========================
        // EVENT PRINTDOCUMENT (PRINT)
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
            var isAutoJob = !string.IsNullOrWhiteSpace(_activeJobId);
            if (isAutoJob)
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

            if (!isAutoJob && e.PrintAction == PrintAction.PrintToPrinter)
            {
                MessageBox.Show("Dokumen berhasil dikirim ke printer.",
                                "Informasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                statusLabel.Text = "Selesai mengirim ke printer.";
            }
            else if (!isAutoJob)
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

        private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
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
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _clientId = null;
                    await RegisterClientAsync();
                    return;
                }

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
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _clientId = null;
                    await RegisterClientAsync();
                    return;
                }

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

        private async System.Threading.Tasks.Task PrintJobFromListAsync(PrintJob job)
        {
            if (_jobProcessing)
            {
                statusLabel.Text = "Masih memproses job lain.";
                return;
            }

            await ProcessJobAsync(job);
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

                // Reset image cache
                _imageToPrint?.Dispose();
                _imageToPrint = null;

                string ext = Path.GetExtension(downloadPath).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                {
                    using var img = Image.FromFile(downloadPath);
                    _imageToPrint = new Bitmap(img);
                }

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

        }
    }
}
