## The Architectural Philosophy: Immutable State

In this project, I moved beyond standard CRUD. In high-stakes environments, data is never truly deleted — it only evolves. I implemented a **Temporal Table Pattern** to ensure every `UPDATE` is recorded as a historical snapshot, preserving a verifiable audit trail.

I built and compared two separate implementations of the same idea:

- **PostgreSQL** — using the `table_version` extension (schema-based, extension-driven versioning)
- **MSSQL Server** — using native System Versioning (engine-level, declarative temporal tables)

---

## Project Structure

```
database_versioning/
├── postgres_database_versioning/   # PostgreSQL + table_version extension
│   ├── DatabaseVersion.Api/        # .NET 8 API (EF Core + Npgsql)
│   ├── Learnings.MD                # Initial edge case notes
│   └── Learnings2.MD               # Deep write cost & growth analysis
├── mssql_database_versioning/      # MSSQL Server System Versioning
│   └── Controllers / Data / Models # .NET API with EF Core .IsTemporal()
└── README.md
```

---

## PostgreSQL Implementation: `table_version` Extension

### System Architecture: The Multi-Schema Approach

Unlike my initial "in-table" design, the current system separates **Operational Data** from **Historical Data** using PostgreSQL schemas.

| Component | Responsibility | Database Object |
|---|---|---|
| **Live State** | Holds only the most recent version for the UI | `public.todo_item` |
| **Audit Log** | Stores every previous version of a row | `table_version.public_todo_item_revision` |
| **Revision Journal** | Maps database changes to human-readable notes | `table_version.revision` |

Originally, I experimented with manual PL/pgSQL triggers, but evolved the system to use the **`table_version`** extension. This shifted the complexity from custom application code to a robust, schema-based versioning engine.

### 1. Extension Integration

Instead of manual triggers, the table is registered with the versioning engine:

```sql
-- Enable system-wide versioning for the table
SELECT table_version.ver_enable_versioning('public', 'todo_item');
```

### 2. Transactional Revision Tagging

One edge  of this implementation is the ability to group multiple row changes under a single **Revision Note**. In the `.NET` controller, I tag the transaction before saving:

```csharp
// Tags the audit log before the update happens
await _context.Database.ExecuteSqlRawAsync("SELECT table_version.ver_create_revision('System update via API')");
await _context.SaveChangesAsync();
```

### 3. Time-Travel Queries

To retrieve history, the API queries a specialised **Revision View**. This lets the frontend "time-travel" through every change made to a specific entity.

```sql
SELECT * FROM table_version.public_todo_item_revision
WHERE entity_id = @id
ORDER BY version DESC;
```

---

## MSSQL Implementation: Native System Versioning

### How It Works

MSSQL implements temporal tables at the engine level. When a row is updated, the engine atomically moves the previous row state to the history table — no triggers, no application code, no middleware.

I configured it through EF Core's `.IsTemporal()` fluent API:

```csharp
modelBuilder.Entity<TodoItem>()
    .ToTable("TodoItems", t => t.IsTemporal());
```

This generates hidden `SysStartTime` / `SysEndTime` period columns and a corresponding `TodoItemHistory` table. EF Core exposes first-class LINQ for time-travel queries:

```csharp
// All historical versions
var history = await _context.TodoItems.TemporalAll().AsNoTracking().ToListAsync();

// State at a specific point in time
var snapshot = await _context.TodoItems.TemporalAsOf(pointInTime).AsNoTracking().ToListAsync();
```

### What MSSQL Does Better

- The audit trail **cannot have gaps by design** — the engine enforces it
- No raw SQL needed for history queries — LINQ works natively
- Write overhead is lower: ~10–20% vs PostgreSQL's ~3–6× multiplier
- The query planner has native awareness of `FOR SYSTEM_TIME` intervals

### What It Costs

- The history table schema must mirror the live table exactly — no extra metadata columns
- Schema migrations require disabling system versioning, altering both tables, and re-enabling — this takes an exclusive lock. Running this during business hours will stall the whole application
- Every `UPDATE` writes a history row, even if no values actually changed (no-op updates)

---

## Lessons Learned & Challenges

### The "Mirror Room" Effect (Recursive Triggers)

- **Problem:** Early manual trigger iterations caused infinite loops where an `INSERT` triggered another `INSERT`.
- **Solution:** Adopting the `table_version` extension provided a built-in circuit breaker — it handles row-forking internally without recursive depth errors.

### EF Core Identity Conflict

- **Problem:** When querying history, multiple rows share the same Primary Key (`id`). EF Core's Change Tracker would merge these into a single object.
- **Solution:** Used `.AsNoTracking()` in the controller to force EF Core to treat every historical snapshot as a unique data point.

### PostgreSQL Case Sensitivity (WSL/Linux)

- **Problem:** Developing on Windows but running PostgreSQL on WSL (Ubuntu) revealed strict case-sensitivity issues (`TodoItems` vs `todo_item`).
- **Solution:** Standardised all database objects to lowercase `snake_case` and used explicit `HasColumnName` mappings in `AppDbContext`.

### MSSQL Schema Lock During Migration

- **Problem:** EF Core generates a four-step SQL sequence for adding a column to a temporal table (`SET SYSTEM_VERSIONING = OFF` → `ALTER` → `ALTER` → `SET SYSTEM_VERSIONING = ON`). Under active load, this blocks everything.
- **Solution:** Temporal table migrations must run during a scheduled maintenance window, never as part of an automated deployment pipeline.

---

## The Real Cost: Write Amplification

Every logical `UPDATE` in the PostgreSQL implementation is not a single write. It becomes:

```
1× UPDATE  → live table
1× INSERT  → revision table (full row copy)
1× INSERT  → revision metadata
  + index updates on all revision table indexes
  + WAL entries for every one of the above
```

**Real multiplier: ~3×–6× actual write cost per logical operation.**

At 100,000 updates/day with an 800-byte row and 5 indexes on the revision table:

- Daily growth: **~250–400 MB/day**
- Monthly growth: **~7–12 GB/month**

The hidden driver is the **WAL (Write-Ahead Log)**. PostgreSQL writes every change twice — once to WAL, once to the actual table. Versioning multiplies this. What breaks first in production is not disk space or CPU — it is **I/O throughput and WAL pressure**.

MSSQL is lower cost here: it logs both the live table update and the history insert within a single atomic transaction log record, which is why the overhead stays at ~10–20%.

---

## Growth Curve Reality

Both systems grow non-linearly without lifecycle management:

| Timeline | PostgreSQL | MSSQL |
|---|---|---|
| Month 1 | Everything fast | Negligible overhead |
| Month 3 | Index maintenance noticeable | Backup window starts growing |
| Month 6 | VACUUM starts falling behind; bloat begins | Temporal queries slow as history table grows |
| Month 12 | Query latency degrades | VLF fragmentation appears |
| Month 18+ | Ops team in emergency mode | Index maintenance spills into business hours |

---

## Edge Cases That Bit Me (or Will Bite You)

**Hot Row Explosion** — One entity updated at very high frequency accumulates 100k+ versions, causing index skew and slow history queries for that record. Fix: cap history per entity or archive aggressively.

**Update Storms** — A batch job updating 10,000+ rows fires one revision write per row, keeping the transaction open for the full duration. In PostgreSQL this blocks VACUUM and spikes WAL. In MSSQL it crosses the 5,000-lock-per-statement threshold and escalates to a table-level lock. Fix: chunk batch operations into small transactions.

**Silent Storage Explosion** — A typo correction, a spacing fix, a status toggle — each creates a full row copy in the revision table. Three meaningless edits = three complete row snapshots. Fix: diff-check before writing where practical.

**History Queries Becoming Useless** — `ORDER BY version DESC` on a large, unpartitioned revision table gets progressively slower and more memory-heavy. Fix: always `LIMIT` and paginate history queries; partition the revision table by date.

**VACUUM Can't Keep Up** — Even though revision tables are append-only, the live table still generates dead tuples on every `UPDATE`. Under heavy `table_version` write loads, autovacuum with default settings cannot keep up. Bloat accumulates silently until queries degrade. Fix: tune `autovacuum_vacuum_scale_factor` downward for any versioned table before going live.

**GDPR / Right to Be Forgotten** — The system is built on "data is never deleted." But GDPR requires that it is. Deleting a user from the live table leaves their PII in every historical snapshot in the `table_version` schema. Fix: a custom purge script is required — there is no built-in mechanism.

---

## Production Rules (The Non-Negotiables)

1. **Not everything should be versioned.** Only version business-critical state — financial records, access control, consent data. Session tokens, notification preferences, and counters should not be versioned.

2. **Lifecycle management is mandatory.** A typical retention policy: 30–90 days full history, 6–12 months compressed, older → archived to cold storage (S3 / Azure Blob) or deleted. Without this, the system chokes on its own output.

3. **Partition early, not later.** Use `PARTITION BY RANGE (revision_timestamp)`. Dropping an old partition is near-instantaneous. Deleting millions of rows from a monolithic table is not.

4. **Separate read contexts.** Long-running audit queries must not share the same `DbContext` or connection pool as operational writes. Use a dedicated `AuditDbContext` on a read replica. Five concurrent 90-day audit reports will exhaust a default connection pool of 100.

5. **Monitor these metrics.** WAL size per second, table size growth rate, index size growth rate, vacuum lag, and replication delay. If you are not tracking these before go-live, the first warning will come from users.

---

## Summary of Achievement

1. **Automated Audit Trail:** No manual code copies rows — the database handles it via the `table_version` extension (PostgreSQL) or the storage engine itself (MSSQL).
2. **Data Integrity:** A compound unique constraint on `(entity_id, version)` prevents history corruption.
3. **Optimised Live Performance:** The live table stays small and fast; historical data accumulates in a separate schema or table, independently manageable.
4. **Honest Cost Model:** Both implementations have been benchmarked for write amplification, storage growth curves, and failure modes under load — not just happy-path functionality.
