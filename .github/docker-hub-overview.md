# ADO Pipeline Comparator

Web-based tool for comparing **Build and Release pipelines across multiple Azure DevOps organizations** side-by-side. Connect as many ADO organizations as needed, cache pipeline definitions locally, and compare stages, jobs, steps, variables, and triggers in a rich browser UI.

## What it does

- **Multi-org connections** — Connect to any number of Azure DevOps organizations simultaneously via PAT (stored AES-256 encrypted)
- **Build & Release pipelines** — Supports YAML pipelines, classic build definitions, and classic release definitions
- **Side-by-side comparison** — Select 2+ pipelines and compare stages, jobs, steps, variables, and triggers
- **Pipeline cache** — Pipelines are cached locally in SQLite; refresh on demand (all, per org, or per pipeline)
- **Advanced table** — Sort, filter, group, reorder columns, select rows
- **Export** — Export comparison results to Excel (.xlsx) and PDF

## How to run

### Docker Compose (recommended)

```bash
# 1. Create a .env file with your encryption key
echo "ENCRYPTION_KEY=$(openssl rand -base64 32)" > .env

# 2. Start
docker compose up -d

# 3. Open http://localhost:5000
```

### docker run

```bash
docker run -d --name ado-pipeline-comparator \
  -p 5000:8080 \
  -v ado-data:/app/data \
  -e ENCRYPTION_KEY=your-strong-key-here \
  vglu/ado-pipeline-comparator:latest
```

Open **http://localhost:5000**.

### Volumes

| Mount | Purpose |
|-------|---------|
| `/app/data` | SQLite database — persist this volume to keep all sites and cached pipeline data |

### Environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ENCRYPTION_KEY` | **Yes** | AES-256 key for PAT token encryption (32+ chars recommended) |
| `DB_PATH` | No | Path to SQLite file inside container (default: `/app/data/data.db`) |
| `ASPNETCORE_URLS` | No | Bind address (default: `http://0.0.0.0:8080`) |

## Generate a secure encryption key

**Linux/macOS:**
```bash
openssl rand -base64 32
```

**PowerShell (Windows):**
```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32; $rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

## Tags

- `latest` — latest release
- `v1.x.x` — specific version (e.g. `v1.0.0.1`)

## Links

- **Source & docs:** [GitHub — vglu/ADOPipelineComparator](https://github.com/vglu/ADOPipelineComparator)
- **Releases (Windows ZIP):** [GitHub Releases](https://github.com/vglu/ADOPipelineComparator/releases)
- **GHCR image:** `ghcr.io/vglu/ado-pipeline-comparator`
