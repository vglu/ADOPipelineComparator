# ADO Pipeline Comparator — System Requirements

## 1. Overview

**ADO Pipeline Comparator** — a desktop / server application in C# for comparing Build and Release pipelines across multiple Azure DevOps (ADO) organizations.  
The application runs as a standalone `.exe` file on Windows and as a Docker container on any platform.

---

## 2. Tech Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Platform | .NET 8 (LTS) | Cross-platform, Docker support |
| UI Framework | ASP.NET Core + Blazor Server | Runs as .exe and in Docker; rich interactive browser components |
| UI Component Library | MudBlazor | Free, feature-rich with tables, trees, and dialogs |
| Database | SQLite | File-based DB; mounted as external volume in Docker |
| ORM | Entity Framework Core 8 | Code-First migrations, works with SQLite |
| ADO API Client | Azure DevOps .NET Client Libraries (`Microsoft.TeamFoundationServer.Client`) | Official SDK |
| Containerization | Docker + docker-compose | Volume for DB outside the container |
| Configuration | `appsettings.json` + environment variables | DB path overridden at runtime in Docker |

---

## 3. Run Modes

### 3.1 Run as .exe (Windows)
```
ADOPipelineComparator.exe --urls "http://localhost:5000"
```
- Opens the default browser at `http://localhost:5000`
- Database stored in `%AppData%\ADOPipelineComparator\data.db` (default) or the path from `appsettings.json`

### 3.2 Run in Docker
```yaml
# docker-compose.yml
services:
  app:
    image: ado-pipeline-comparator
    ports:
      - "5000:8080"
    volumes:
      - ./data:/app/data   # external DB - NOT inside the container
    environment:
      - DB_PATH=/app/data/data.db
```
- Database mounted as external volume (`./data/data.db`)
- Data persists when the container is recreated

---

## 4. Database

### 4.1 Location
- File-based SQLite database; path configured via application settings
- In Docker it must be mounted as an external volume

### 4.2 Tables

#### `AdoSites` — ADO site registry
| Column | Type | Description |
|--------|------|-------------|
| `Id` | INTEGER PK | Auto-increment |
| `Name` | TEXT NOT NULL | Display name of the entry |
| `OrganizationUrl` | TEXT NOT NULL | ADO organization URL (e.g. `https://dev.azure.com/myorg`) |
| `Pat` | TEXT NOT NULL | Personal Access Token (stored encrypted) |
| `CreatedAt` | DATETIME | Creation date |
| `UpdatedAt` | DATETIME | Last modified date |

> **Security**: PAT is stored encrypted (AES-256; key from `appsettings.json` or `ENCRYPTION_KEY` environment variable).

#### `PipelineCache` — pipeline cache (optional, for performance)
| Column | Type | Description |
|--------|------|-------------|
| `Id` | INTEGER PK | |
| `AdoSiteId` | INTEGER FK | Reference to `AdoSites` |
| `Project` | TEXT | Project name |
| `PipelineId` | INTEGER | Pipeline ID in ADO |
| `PipelineName` | TEXT | Pipeline display name |
| `PipelineType` | TEXT | `Build` / `Release` |
| `LastRunDate` | DATETIME | Date of the last run |
| `LastRunBy` | TEXT | Who triggered the run |
| `PipelineUrl` | TEXT | Direct link to the pipeline in ADO |
| `PipelineSubtype` | TEXT | `YAML` / `ClassicBuild` / `ClassicRelease` |
| `CachedAt` | DATETIME | When the cache was last updated (shown in the "Updated" column) |

---

## 5. Modules and Screens

### 5.1 ADO Sites (`/sites`)

**Description**: Manage connections to ADO organizations.

**Functionality**:
- Table listing all sites (Name, OrganizationUrl, creation date)
- **Create** — dialog with fields: Name, OrganizationUrl, PAT
- **Edit** — modify an existing record
- **Delete** — with confirmation
- **Test connection** — button that performs a test request to ADO and shows status (OK / error)
- PAT displayed masked (`••••••••`), with a "Show" toggle button

---

### 5.2 Pipelines (`/pipelines`)

**Description**: Main screen — a tree-table of all pipelines from all connected ADO sites.

**Table columns**:
| Field | Description | Applicability |
|-------|-------------|---------------|
| Organization | Name from the site registry | Build + Release |
| Project | ADO project name | Build + Release |
| Pipeline ID | Numeric ID in ADO | Build + Release |
| Pipeline Name | Display name | Build + Release |
| Type | `Build` / `Release` | Build + Release |
| Last Run (UTC) | Date/time UTC | Build + Release |
| Last Run By | Username | Build + Release |
| Task Name | Name of the pipeline job/task | **Build only** |

**Table capabilities**:
- **Tree**: grouping by Organization → Project → Type → Pipeline
- **Sorting** by any column (ASC/DESC)
- **Filtering** by any field (search string + filter by Build/Release type)
- **Show/hide columns** — column visibility menu
- **Column reordering** — drag & drop column headers
- **Grouping** — select a field to group by
- **Row selection** — checkbox to select 2 or more pipelines
- **"Compare" button** — enabled when ≥2 pipelines of the same type (Build or Release) are selected
- **"Refresh all" button** (top level, whole table) — reload pipelines for all organizations
- **"Refresh organization" button** (in the organization group row) — reload all pipelines for a specific organization
- **"↻" button** (in a specific pipeline row) — refresh only that pipeline
- **"Updated" column** — date and time of the last data retrieval from ADO for each pipeline
- **"Open in ADO" button** — for a single selected pipeline, opens the URL in the browser

---

### 5.3 Compare Pipelines (`/compare`)

**Description**: Detailed side-by-side comparison of two or more pipelines.

#### 5.3.1 Header
- List of pipelines being compared (pills/chips): `[Org] Pipeline Name`
- **"+ Add pipeline" button** — opens a side panel to select a pipeline
- **"✕" button** on each pipeline — remove from comparison
- **"Open in ADO ↗" button** on each pipeline — open URL in a new browser tab

#### 5.3.2 Comparison Sections

Each section is a collapsible accordion panel with a table.  
Rows that **match** in all pipelines are highlighted green (`#e8f5e9`).  
Rows that **differ** in at least one pipeline are highlighted yellow (`#fff9c4`).  
Rows that are **missing** in one of the pipelines are highlighted red (`#ffebee`).

##### Section A: Variables
| Column | Description |
|--------|-------------|
| Variable Name | Key |
| Value [Pipeline 1] | Variable value |
| Value [Pipeline 2] | Variable value |
| ... | For each pipeline |
| Status | `=` (match) / `≠` (different) / `—` (missing) |

##### Section B: Triggers
| Column | Description |
|--------|-------------|
| Trigger Type | CI / Scheduled / PR |
| Parameters [Pipeline N] | Branches, paths, schedule |
| Status | |

##### Section C: Variable Groups
| Column | Description |
|--------|-------------|
| Group Name | |
| Linked [Pipeline N] | Yes / No |
| Status | |

##### Section D: Steps (Jobs & Tasks)
Hierarchical view: Stage → Job → Step

| Column | Description |
|--------|-------------|
| Order | Step number |
| Type | task / script / checkout / etc. |
| Step Name | displayName |
| Parameters [Pipeline N] | All inputs/parameters of the step |
| Status | Match / Different / Missing |

- Cell parameters: if a value contains `\n` or `\\n` — displayed as a real line break, not as literal characters

#### 5.3.3 Common Section Requirements
- Each section can be collapsed/expanded
- Each table supports sorting and filtering
- Cells with multiline content (`\n`, `\\n`) display real line breaks (not escape sequences)
- **"Copy as CSV" button** for each section
- **"Export to Excel" button** for each section (`.xlsx` file)
- **"Export all to Excel" button** — single file with all sections on separate sheets
- **"Export to PDF" button** — comparison report in PDF format
- Adding/removing a pipeline updates the tables without a page reload

---

## 6. Non-Functional Requirements

### 6.1 Performance
- Expected scale: 3–5 organizations, 15–20 projects, up to ~100 pipelines total
- Initial pipeline list load: ≤ 5 seconds at the stated scale
- Loading indicators for all asynchronous operations
- Pipeline data cached in SQLite (TTL: 15 minutes, configurable)

### 6.2 Security
- **No application-level authentication required** — accessible only via localhost when running as .exe
- PAT tokens stored encrypted in the database (AES-256)
- Encryption key from environment variable or `appsettings.json`, never hardcoded
- Application listens only on `localhost` when running as .exe (not exposed to the network)
- In Docker — configured via `ASPNETCORE_URLS`

### 6.3 Reliability
- If one ADO site is unavailable — all others continue to work
- ADO API errors are shown to the user, they do not crash the application
- Automatic DB migrations on application startup

### 6.4 Compatibility
- Windows 10/11 x64 (.exe mode)
- Linux x64 (Docker mode, Alpine-based image)
- Browser: Chrome ≥ 100, Edge ≥ 100, Firefox ≥ 100

---

## 7. Project Structure (Preliminary)

```
ADOPipelineComparator/
├── docs/
│   ├── requirements.md          <- this file
│   └── architecture.md          <- architecture and ADR
├── src/
│   ├── ADOPipelineComparator.Web/       <- Blazor Server App (entry point)
│   │   ├── Pages/
│   │   │   ├── Sites.razor              <- ADO site registry
│   │   │   ├── Pipelines.razor          <- pipeline list
│   │   │   └── Compare.razor            <- comparison form
│   │   ├── Components/
│   │   │   ├── PipelineTable.razor
│   │   │   ├── CompareSection.razor
│   │   │   └── SiteDialog.razor
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── ADOPipelineComparator.Core/      <- business logic, models
│   │   ├── Models/
│   │   ├── Services/
│   │   │   ├── AdoService.cs            <- ADO API interaction
│   │   │   └── CompareService.cs        <- comparison logic
│   │   └── Interfaces/
│   └── ADOPipelineComparator.Data/      <- EF Core, SQLite
│       ├── AppDbContext.cs
│       ├── Entities/
│       └── Migrations/
├── docker-compose.yml
├── Dockerfile
└── ADOPipelineComparator.sln
```

---

## 8. ADO API — Used Endpoints

| Data | ADO REST API / SDK |
|------|--------------------|
| Build pipeline list | `GET {org}/{project}/_apis/pipelines` |
| Build pipeline definition | `GET {org}/{project}/_apis/pipelines/{id}` |
| Latest Build runs | `GET {org}/{project}/_apis/build/builds?definitions={id}&$top=1` |
| Release pipeline list | `GET {org}/{project}/_apis/release/definitions` |
| Release pipeline definition | `GET {org}/{project}/_apis/release/definitions/{id}` |
| Latest Release runs | `GET {org}/{project}/_apis/release/releases?definitionId={id}&$top=1` |
| Project list | `GET {org}/_apis/projects` |

---

## 9. Key Use Cases (User Stories)

1. **As a user** I want to add multiple ADO organizations so that I can see pipelines from all of them in one table.
2. **As a user** I want to filter pipelines by organization and type so that I can quickly find what I need.
3. **As a user** I want to select two Build pipelines and compare their variables to find differences.
4. **As a user** I want to add a third pipeline to an already open comparison without starting over.
5. **As a user** I want to click "Open in ADO" and navigate to the pipeline in the browser.
6. **As a user** I want multiline scripts in table cells to render properly, without `\n` shown as literal characters.
7. **As a DevOps engineer** I want to run the application in Docker with the database mounted externally so that data is not lost when the container is recreated.

---

## 10. Export of Comparison Results

### 10.1 Export to Excel (.xlsx)
- Library: **ClosedXML** (free, no Office dependency)
- Each comparison section (Variables, Triggers, Variable Groups, Steps) on a separate sheet
- Row color highlighting preserved in Excel cells (green / yellow / red)
- Export button for individual sections + "Export all" button (all sheets at once)

### 10.2 Export to PDF
- Library: **QuestPDF** (free for open-source projects)
- Single document with all sections
- Document header: names of compared pipelines + export date/time
- Row color highlighting preserved

---

## 11. YAML vs Classic Pipeline Comparison

### 11.1 Pipeline Types in ADO
| Type | Stored as | Data available via API |
|------|-----------|------------------------|
| YAML Build | `.yml` file in repository | Steps, variables, triggers from the YAML file |
| Classic Build | JSON definition in ADO | Steps, variables, triggers from the UI |
| Classic Release | JSON definition in ADO | Stages, environments, tasks, variables |

### 11.2 Decision — Unified Model
Both types (YAML and Classic) are mapped to a single internal model `PipelineDefinition` before comparison.
Therefore the comparison form is the same regardless of pipeline type.

**What is shown when comparing YAML vs Classic:**
- Variables section — available for both
- Triggers section — available for both (different format → normalized)
- Variable Groups section — available for both
- Steps section — for YAML: stages/jobs/steps from `.yml`; for Classic: phases/tasks from JSON
- The "Definition Type" column in the pipeline table shows: `YAML` / `Classic Build` / `Classic Release`
- When comparing YAML and Classic, an info banner is shown: _"Pipelines of different types — some fields may be missing"_

### 11.3 Recommendation
Do not prohibit comparing YAML vs Classic — allow the comparison but inform the user about limitations.

---

## 12. Resolved Design Decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | Application-level authentication? | **No** — localhost only, no login |
| 2 | Export comparison results? | **Yes** — Excel (ClosedXML) + PDF (QuestPDF) |
| 3 | Comparison history (snapshots)? | **No** |
| 4 | YAML vs Classic handled separately? | **No** — unified model, info banner |
| 5 | Scale | 3–5 organizations, 15–20 projects, up to ~100 pipelines |
| 6 | Change notifications? | **No** — manual refresh (buttons at org level / per pipeline) |
