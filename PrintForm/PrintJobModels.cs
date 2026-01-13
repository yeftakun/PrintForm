namespace PrintForm
{
    internal sealed class PrintJob
    {
        public string Id { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public PrintConfig? PrintConfig { get; set; }
    }

    internal sealed class PrintConfig
    {
        public string? PaperSize { get; set; }
        public int Copies { get; set; }
    }
}
