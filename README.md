# ADO Pipeline Comparator

A web-based tool for comparing Build and Release pipelines across multiple Azure DevOps organizations side-by-side. Connects to any number of ADO organizations, caches pipeline definitions locally, and provides a rich comparison UI — run as a standalone Windows `.exe` or as a Docker container on any platform.

## Features

- **Multi-org support** — Connect to any number of Azure DevOps organizations at once (PAT-based, stored AES-256 encrypted)
- **Build & Release pipelines** — Supports classic build definitions, YAML pipelines, and classic release definitions
- **Side-by-side comparison** — Select 2+ pipelines and compare stages, jobs, steps, variables, and triggers in collapsible sections
- **Pipeline cache** — Pipelines are cached in a local SQLite database; refresh on demand (all orgs, single org, or single pipeline)
- **Advanced table** — Sorting, filtering, grouping, column reordering/visibility, row selection
- **Organizations view** — Manage ADO projects per site (show/hide from comparisons)
- **Export** — Export comparison results to Excel (.xlsx) and PDF
- **Test connection** — Validate each ADO site connection before saving

## Architecture

```
Blazor Server UI  →  Service Layer (Core)  →  Repository (Data / EF Core)
                             ↓
                    Azure DevOps .NET Client Libraries
                             ↓
                     SQLite Database (local file)
```

| Component | Technology |
|-----------|-----------|
| Frontend | Blazor Server + MudBlazor 7.x (Material Design) |
| Backend | ASP.NET Core 8 (.NET 8 LTS) |
| Database | SQLite via Entity Framework Core 8 |
| ADO Client | `Microsoft.TeamFoundationServer.Client` (official SDK) |
| Encryption | AES-256 (PAT tokens stored encrypted) |
| Export | ClosedXML (Excel), QuestPDF |

## Pages

| Page | URL | Description |
|------|-----|-------------|
| **Home** | `/` | Dashboard / welcome page |
| **ADO Sites** | `/sites` | Manage ADO organization connections (URL + PAT) |
| **Organizations** | `/organizations` | Browse and toggle ADO projects per site |
| **Pipelines** | `/pipelines` | Full pipeline tree with cache refresh, search, and selection |
| **Compare** | `/compare` | Side-by-side pipeline comparison with export |

## Quick Start

### Option 1 — Docker (recommended)

```bash
# 1. Create a .env file with your encryption key
echo "ENCRYPTION_KEY=your-strong-32-char-key-here" > .env

# 2. Start the container
docker compose up -d

# 3. Open in browser
http://localhost:5000
```

### Option 2 — Docker run (single command)

```bash
docker run -d --name ado-pipeline-comparator \
  -p 5000:8080 \
  -v ado-data:/app/data \
  -e ENCRYPTION_KEY=your-strong-key-here \
  vglu/ado-pipeline-comparator:latest
```

### Option 3 — Run from source (.NET 8 SDK required)

```bash
git clone https://github.com/vglu/ADOPipelineComparator.git
cd ADOPipelineComparator
dotnet run --project src/ADOPipelineComparator.Web
```

Open `http://localhost:5000`.

## Docker

### Docker Compose

The `docker-compose.yml` in the repository root is ready to use. Create a `.env` file next to it:

```env
ENCRYPTION_KEY=<generated-32-char-base64-key>
```

Generate a secure key (PowerShell):
```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

Generate a secure key (Linux/macOS):
```bash
openssl rand -base64 32
```

Then run:
```bash
docker compose up -d
```

### docker run

```bash
docker run -d \
  --name ado-pipeline-comparator \
  -p 5000:8080 \
  -v ado-data:/app/data \
  -e ENCRYPTION_KEY=<your-key> \
  vglu/ado-pipeline-comparator:latest
```

### Volumes

| Mount | Purpose |
|-------|---------|
| `/app/data` | SQLite database — persist this volume to keep all sites, cache, and settings |

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ENCRYPTION_KEY` | **Yes** | AES-256 key for encrypting PAT tokens (min 16 chars, 32+ recommended) |
| `DB_PATH` | No | Full path to the SQLite file inside the container (default: `/app/data/data.db`) |
| `ASPNETCORE_URLS` | No | Bind address (default: `http://0.0.0.0:8080`) |

### Tags

| Tag | Description |
|-----|-------------|
| `latest` | Latest stable release |
| `v1.x.x` | Specific version (e.g. `v1.0.0.1`) |

## First Steps After Start

1. Go to **ADO Sites** → **Add Site** — enter a name, your Azure DevOps organization URL (e.g. `https://dev.azure.com/myorg`), and a PAT token with **Read** access to Pipelines
2. Click **Test Connection** to verify
3. Go to **Pipelines** → click **Refresh All** to load pipelines from all connected ADO sites
4. Select 2 or more pipelines using the checkboxes → click **Compare**
5. Explore the comparison sections (Stages, Jobs, Steps, Variables, Triggers) in the Compare view
6. Use **Export** to save results as Excel or PDF

## Security

- PAT tokens are stored **encrypted** (AES-256) in the SQLite database — they are never stored in plaintext
- The encryption key must be provided via the `ENCRYPTION_KEY` environment variable (or `appsettings.json` for development)
- Never commit `.env` files or `appsettings.Production.json` to version control
- By default the application binds to all interfaces inside Docker; in production, restrict with a reverse proxy (nginx, Traefik) or adjust port mapping

## Build from Source

```bash
# Restore and build
dotnet build ADOPipelineComparator.sln

# Run tests
dotnet test ADOPipelineComparator.sln

# Publish (self-contained Windows exe)
dotnet publish src/ADOPipelineComparator.Web/ADOPipelineComparator.Web.csproj \
  -c Release -r win-x64 --self-contained true -o ./publish
```

## Releases & Packages

- **GitHub Releases**: [github.com/vglu/ADOPipelineComparator/releases](https://github.com/vglu/ADOPipelineComparator/releases) — Windows self-contained ZIP + release notes
- **Docker Hub**: [hub.docker.com/r/vglu/ado-pipeline-comparator](https://hub.docker.com/r/vglu/ado-pipeline-comparator)
- **GitHub Container Registry**: `ghcr.io/vglu/ado-pipeline-comparator`

## License

MIT — see [LICENSE](LICENSE)
