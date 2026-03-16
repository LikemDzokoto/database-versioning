
### 1. The Architectural Philosophy

In high-stakes systems—specifically **data is never deleted.** Standard CRUD (Create, Read, Update, Delete) is insufficient because it destroys the audit trail. I implemented a **Temporal Table Pattern** using PostgreSQL triggers to ensure that every `UPDATE` is actually a "fork" of state, preserving the past while advancing the present.

PostgreSQL lacks built-in SQL:2011-style temporal tables, but its MVCC architecture inherently keeps historical row versions until vacuumed. To explicitly track history, I relied on patterns like audit/history tables, trigger-based versioning, or community extensions. In contrast, DBMS like SQL Server, Oracle, and MariaDB have native system-period (transaction-time) support. SQL:2011 defines SYSTEM_TIME tables, but I have emulated this in PostgreSQL to fit my specific project needs.

This section is now specifically tailored to the **TodoItems** schema I built. It contrasts the "native" way other databases handle it with the "custom-engineered" trigger approach I implemented for my system.

---

### B. System-Versioned Temporal Tables

In this model, the database handles history automatically without the application manually managing versions. While some databases support this natively, PostgreSQL requires the specialized **Trigger + IsCurrent** pattern I implemented.

#### 1. Comparison of Native vs. My PostgreSQL Implementation

| Database | Feature Status | Implementation Method |
| --- | --- | --- |
| **SQL Server** | Native | `SYSTEM_VERSIONING = ON` |
| **MariaDB** | Native | `PERIOD FOR SYSTEM_TIME` |
| **PostgreSQL** | **Custom/Extensible** | **PL/pgSQL Triggers (My Current System)** |

---

#### 2. How it would look in SQL Server (Native Example)

If I were using SQL Server, the `TodoItems` table would look like this. Note that it manages a hidden history table automatically.

```sql
CREATE TABLE "TodoItems" (
    "Id" INT PRIMARY KEY,
    "EntityId" UNIQUEIDENTIFIER,
    "Title" NVARCHAR(100),
    "ValidFrom" DATETIME2 GENERATED ALWAYS AS ROW START,
    "ValidTo" DATETIME2 GENERATED ALWAYS AS ROW END,
    PERIOD FOR SYSTEM_TIME ("ValidFrom", "ValidTo")
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.TodoItems_History));

```

---

#### 3. How I implemented it in PostgreSQL (My System)

Since PostgreSQL lacks that specific `WITH SYSTEM_VERSIONING` syntax, I engineered the **"In-Table Versioning"** pattern.

**My Schema:**

* **`EntityId`**: The permanent identity of the task.
* **`Version`**: The incremental counter.
* **`IsCurrent`**: The flag that distinguishes "Live" from "History."

**My Logic Flow:**
When I run a standard `.NET` update:

```csharp
// Standard EF Core Update
todo.Title = "Updated Title";
await _context.SaveChangesAsync();

```

**The Database performs the "Fork":**

1. **Old Row:** Updated to `IsCurrent = false` (becomes history).
2. **New Row:** Created with `Version + 1` and `IsCurrent = true` (becomes live).

---

#### 4. The Real Superpower: Time-Travel Queries

Because of my versioning schema, I can perform "Time Travel" to see exactly what a Todo item looked like at any point in its history.

**To see the current state:**

```sql
SELECT * FROM "TodoItems" 
WHERE "EntityId" = '8bf2...' AND "IsCurrent" = true;

```

**To see the full Audit Trail (History):**

```sql
SELECT "Version", "Title", "IsCompleted", "ValidFrom" 
FROM "TodoItems" 
WHERE "EntityId" = '8bf2...' 
ORDER BY "Version" DESC;

```

---

## Technical Report: Immutable Versioning in PostgreSQL

### 1. The Core Implementation Logic

My system uses an **Append-Only** pattern. Instead of updating a record, the system "forks" it.

* **The "Current" Row:** The row I am actually editing. It always has the latest data and `IsCurrent = true`.
* **The "History" Row:** A frozen snapshot of the data *before* the change, with `IsCurrent = false`.

### 2. Mistakes I Made & Lessons I Learned

During this experiment, I hit three classic "Architectural Walls":

* **The Infinite Loop (Recursion):**
* *Mistake:* Attaching a trigger that performs an `INSERT` to a table that listens for `INSERT/UPDATE`.
* *Fix:* The **Circuit Breaker**. I added logic to check if `IsCurrent` is already false. If it is, the trigger stops.


* **The Stack Depth Error:**
* *Lesson:* PostgreSQL has a hard limit on nested function calls. Multiple active triggers (which I had) created a "Mirror Room" effect where the database ran out of memory trying to track the changes.


* **The Version Collision:**
* *Mistake:* Trying to have two rows with the same `Version` number at the same time.
* *Fix:* I moved from an `AFTER` trigger to a **`BEFORE`** trigger and implemented **Deferred Constraints**.



---

### 3. Constraints & Indexes: The Fine Print

This is the most technical part of my implementation. Standard indexes break versioning.

#### The "EntityId + Version" Compound Key

A standard Primary Key on `Id` isn't enough. To ensure data integrity for my thesis, I needed a **Unique Constraint** on the pair of `(EntityId, Version)`. This ensures I can't have two "Version 2s" for the same item.

#### Deferrable Constraints

Because the trigger creates a new row *while* the old one is being updated, PostgreSQL temporarily sees a conflict.

* **Postgres Requirement:** I had to mark the constraint as `DEFERRABLE INITIALLY DEFERRED`. This tells Postgres: "Don't panic about duplicates until the very end of the save."

---

### 4. Limitations of My PostgreSQL Approach

While powerful, this manual implementation has trade-offs compared to native "Temporal" support:

1. **Storage Bloat:** Every single change (even a typo fix) creates a full new row. In a massive system, this can grow the database size 10x faster than normal.
2. **Query Complexity:** I can no longer just `SELECT * FROM TodoItems`. I must always remember to add `WHERE "IsCurrent" = true`, or I will get multiple versions of every item in my UI.
3. **Schema Migrations:** If I add a new column (e.g., `Priority`), I must update the Trigger function as well. If I forget, the history inserts will fail.

---

### 5. Critical "To-Knows" (The Lessons)

#### A. The Recursive Trigger Trap

Postgres triggers are powerful but "dumb." If a trigger on `UPDATE` performs an `INSERT` back into the same table, the database may see that insert as a new event and fire the trigger again.

* **Lesson:** Always implement a **Circuit Breaker**. My `IF (OLD."IsCurrent" = false)` check is the logical equivalent of a `return` statement in a recursive function to prevent stack overflow.

#### B. The "In-Flight" Collision

Relational databases enforce unique constraints (like `EntityId` + `Version`) the moment a row is touched.

* **The Problem:** During a version bump, for a few microseconds, both the "old" row being archived and the "new" row being updated share the same version number.
* **The Fix:** **Deferred Constraints**. By marking the unique index as `DEFERRABLE INITIALLY DEFERRED`, I tell Postgres to wait until the end of the transaction to check the rules. This allows the "swap" to happen in memory first.

---

### 6. Indexing & Constraints Strategy

For this experiment to be robust enough for my thesis, the indexing strategy had to move beyond simple Primary Keys:

1. **Identity Index:** A non-unique index on `EntityId` for fast lookups.
2. **Versioning Constraint:** A unique compound index on `("EntityId", "Version")`. This is the "Immutable Law" of my system.
3. **Performance Filter:** A partial index `CREATE INDEX ... WHERE "IsCurrent" = true`. This makes my application's most frequent queries (getting the "now" state) extremely fast.

---

To get my system stable and functioning for my thesis, I ended up with a **single, unified trigger** and a **state-aware function**.

Here is the "Final Cut" of the code that successfully solved the infinite recursion and the unique constraint collisions:

### 1. The Final Trigger Function

```sql
CREATE OR REPLACE FUNCTION handle_todo_versioning()
RETURNS TRIGGER AS $$
BEGIN
    -- 1. THE CIRCUIT BREAKER
    IF (OLD."IsCurrent" = false) THEN
        RETURN NEW;
    END IF;

    -- 2. CREATE THE HISTORICAL SNAPSHOT
    INSERT INTO "TodoItems" (
        "EntityId", "Version", "Title", "Description", 
        "IsCompleted", "ValidFrom", "IsCurrent"
    )
    VALUES (
        OLD."EntityId", OLD."Version", OLD."Title", OLD."Description", 
        OLD."IsCompleted", OLD."ValidFrom", false
    );

    -- 3. UPDATE THE CURRENT RECORD
    NEW."Version" := OLD."Version" + 1;
    NEW."ValidFrom" := NOW();
    NEW."IsCurrent" := true;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

```

### 2. The Final Trigger Definition

```sql
DROP TRIGGER IF EXISTS trg_todo_version_control ON "TodoItems";

CREATE TRIGGER trg_todo_version_control
BEFORE UPDATE ON "TodoItems"
FOR EACH ROW
EXECUTE FUNCTION handle_todo_versioning();

```

### 3. The Final Constraint (The "Magic Trick")

```sql
DROP INDEX IF EXISTS "IX_TodoItems_EntityId_Version";
ALTER TABLE "TodoItems" DROP CONSTRAINT IF EXISTS "IX_TodoItems_EntityId_Version";

ALTER TABLE "TodoItems" 
ADD CONSTRAINT "IX_TodoItems_EntityId_Version" 
UNIQUE ("EntityId", "Version") 
DEFERRABLE INITIALLY DEFERRED;

```

### Summary of what I achieved:

1. **Identity Persistence:** My `EntityId` stays the same for the life of the task.
2. **Automatic Versioning:** I just send a standard `UPDATE` from C#, and the database automatically increments the version.
3. **Immutable History:** Every time I change a title, a "ghost" of the old version is saved with `IsCurrent = false`.
4. **Collision Avoidance:** The deferred constraint prevents the `23505: duplicate key` error during the split-second "swap" of version numbers.

---