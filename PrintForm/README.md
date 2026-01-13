# PrintForm Client (.NET)

WinForms client for receiving and printing jobs from the PrintForm server.

## Requirements

- Windows 10/11
- .NET 8 SDK
- A local printer installed

Optional (for better PDF printing):
- SumatraPDF (recommended). If not installed, the client falls back to Microsoft Edge.

## Run

```bash
dotnet run --project .\PrintForm\PrintForm.csproj
```

The server URL is configured in `PrintForm/Form1.cs` as `ServerBaseUrl`.

## What it does

- Registers to the server and sends heartbeat updates.
- Polls ping messages from the server.
- Shows a Job List window:
  - `Print` for jobs with status `ready`
  - `Retry` for jobs with status `pending`
  - `Reject` for jobs with status `ready`
- Re-checks job status from the server before Print/Reject to avoid stale actions.

## Print behavior

- Jobs are downloaded from the server to a temp file, printed locally, then the temp file is deleted.
- Images (JPG/PNG/BMP) are printed via `PrintDocument`.
- PDF and other file types are printed via SumatraPDF (preferred) or Edge.
- If the selected printer is offline, the job is set to `pending` (not sent to the spooler).

## Job status notes

- `ready`: waiting to be printed.
- `printing`: client started processing.
- `done`: sent to the printer spooler (UI label shows "sent").
- `pending`: printer offline at the moment of print.
- `failed`: download/print failure.
- `rejected`: rejected by the client.
- `canceled`: canceled by the web UI.

## Troubleshooting

- If PDF printing fails, install SumatraPDF and try again.
- If the client does not appear online, make sure the server is running and `ServerBaseUrl` is correct.
