# Phase 1 Summary — Data Ingestion

**Date:** 2026-05-16  
**Branch:** main  
**Commit:** a68f071 (latest)

---

## What Was Built

Phase 1 implements the full XML sync pipeline: download → parse → diff → upsert. On startup (and every 24 hours after), the worker pulls ~50,000 food establishment records from Fødevarestyrelsen and stores them in SQL Server.

---

## Files Changed

### New Files

| File | Purpose |
|---|---|
| `src/SmileyApi.Api/Workers/EstablishmentSyncRow.cs` | DTO record mapping one parsed XML row to a C# type |
| `src/SmileyApi.Infrastructure/Services/EstablishmentSyncService.cs` | All database write logic — bulk upserts for establishments and inspections |

### Modified Files

| File | Change |
|---|---|
| `src/SmileyApi.Api/Workers/FodevareXmlParser.cs` | Fully implemented — downloads XML, streams and parses ~57k rows with `XmlReader` + `XNode.ReadFrom` + LINQ to XML |
| `src/SmileyApi.Api/Workers/XmlSyncWorker.cs` | Implemented `RunSyncAsync` — calls parser, maps DTOs, delegates DB writes via scoped service |
| `src/SmileyApi.Api/Program.cs` | Registered `AddHttpClient()`, `FodevareXmlParser`, `EstablishmentSyncService`, `XmlSyncWorker` |
| `src/SmileyApi.Infrastructure/SmileyApi.Infrastructure.csproj` | Removed broken `EFCore.BulkExtensions` v10 reference |
| `CLAUDE.md` | Updated bulk insert rule to reflect `SqlBulkCopy` + `MERGE` approach |

---

## Architecture

```
XmlSyncWorker (BackgroundService, singleton)
  │
  ├── FodevareXmlParser (singleton)
  │     └── HttpClient → GET XML → XmlReader → List<EstablishmentSyncRow>
  │
  └── IServiceScopeFactory → scope
        └── EstablishmentSyncService (scoped)
              ├── SqlBulkCopy → #estab_staging
              ├── MERGE Establishments (insert new / update changed)
              ├── SqlBulkCopy → #inspection_staging
              └── MERGE Inspections (insert-if-not-exists)
```

---

## Key Decisions

**XML source is plain XML, not a ZIP.**  
The planning doc described the source as a ZIP file. The actual URL (`Smiley_xml.xml`) serves raw XML directly. The `ZipArchive` unwrapping was removed after the first run failed with `InvalidDataException: End of Central Directory record could not be found`.

**`EFCore.BulkExtensions` replaced with `SqlBulkCopy` + raw `MERGE`.**  
The csproj referenced `EFCore.BulkExtensions` v10, which turned out to be a .NET 10-only meta-package with no DLLs. Rather than downgrading to an older version with EF Core 9 compatibility concerns, bulk operations were implemented using `SqlBulkCopy` (available via the existing `Microsoft.Data.SqlClient` transitive dependency) and raw SQL `MERGE` statements with temp tables. This approach is explicitly allowed by CLAUDE.md and has no additional package dependencies.

**`EstablishmentSyncService` lives in Infrastructure, not the worker.**  
Keeping `SqlBulkCopy` and EF Core usage inside Infrastructure maintains the clean project boundaries: Api stays thin, Infrastructure owns all DB concerns.

**`XNode.ReadFrom` instead of `ReadElementContentAsStringAsync` for row parsing.**  
The Fødevarestyrelsen XML is compact — no whitespace between sibling child elements within `<row>`. The original async element-by-element reader loop called `ReadElementContentAsStringAsync()`, which advances past the element's end tag and lands on the next sibling's start tag. The outer `while (ReadAsync())` then skips that sibling. Every other field was silently dropped, causing the null check on `navn1` to fail and all rows to be discarded (0 rows parsed). Fixed by replacing the loop with `XNode.ReadFrom(reader)`, which atomically consumes the entire `<row>` subtree, then parsing with LINQ to XML (`XElement`). Also corrected field names (`navn1`, `naestseneste_kontrol`, `tredjeseneste_kontrol`, `fjerdeseneste_kontrol`) and the date format (`dd-MM-yyyy HH:mm:ss`).

**Inspection MERGE deduplicates source via `ROW_NUMBER()`.**  
The XML contains multiple `<row>` elements with the same `navnelbnr`. When those rows share an inspection date, `#inspection_staging` ends up with duplicate `(EstablishmentId, InspectedOn)` pairs. SQL Server's MERGE evaluates all source rows simultaneously — both duplicates show "NOT MATCHED" before either is inserted — and attempts to INSERT both, violating the unique index. Fixed by wrapping the staging table in a `ROW_NUMBER() OVER (PARTITION BY EstablishmentId, InspectedOn)` subquery in the USING clause, ensuring only one source row per pair reaches the merge.

---

## Sync Behaviour

- **First run:** inserts all ~50,000 establishments and ~150,000–200,000 inspection rows.
- **Subsequent runs:** updates only establishments where fields changed; skips inspection rows already present (enforced by unique index on `(EstablishmentId, InspectedOn)`).
- **Cadence:** runs once at startup, then every 24 hours.
- **Failure handling:** exceptions are caught and logged; the worker continues to the next 24-hour cycle without crashing the process.

---

## Milestone Status

> **Phase 1 milestone: Full DB populated from one XML sync run** ✅
