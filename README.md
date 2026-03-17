##  The Architectural Philosophy: Immutable State

In this project, I moved beyond standard CRUD. In high-stakes environments, data is never truly deleted, it only evolves. I implemented a **Temporal Table Pattern** to ensure every `UPDATE` is recorded as a historical snapshot, preserving a verifiable audit trail.



Originally, I experimented with manual PL/pgSQL triggers, but evolved the system to use the **`table_version`** PostgreSQL extension. This shifted the complexity from custom application code to a robust, schema-based versioning engine.



---



##  System Architecture: The Multi-Schema Approach



Unlike my initial "In-Table" design, the current system separates **Operational Data** from **Historical Data** using PostgreSQL schemas.



| Component | Responsibility | Database Object |


| **Live State** | Holds only the most recent version for the UI. | `public.todo_item` |

| **Audit Log** | Stores every previous version of a row. | `table_version.public_todo_item_revision` |

| **Revision Journal** | Maps database changes to human-readable notes. | `table_version.revision` |







---



##  Technical Implementation



### 1. Extension Integration

Instead of manual triggers, the table is registered with the versioning engine:

```sql

-- Enable system-wide versioning for the table

SELECT table_version.ver_enable_versioning('public', 'todo_item');

```



### 2. Transactional Revision Tagging

One "Superpower" of this implementation is the ability to group multiple row changes under a single **Revision Note**. In the `.NET` controller, I tag the transaction before saving:



```csharp

// Tags the audit log before the update happens

await _context.Database.ExecuteSqlRawAsync("SELECT table_version.ver_create_revision('System update via API')");

await _context.SaveChangesAsync();

```



### 3. Time-Travel Queries

To retrieve the history, the API queries a specialized **Revision View**. This allows the frontend to "time-travel" through every change made to a specific entity.



```sql

SELECT * FROM table_version.public_todo_item_revision 

WHERE entity_id = @id 

ORDER BY version DESC;

```



---



##  Lessons Learned & Challenges



### The "Mirror Room" Effect (Recursive Triggers)

* **Problem:** Early manual triggers caused infinite loops where an `INSERT` triggered another `INSERT`.

* **Solution:** Adopting the `table_version` extension provided a "Circuit Breaker" architecture, handling row-forking internally without recursive depth errors.



### EF Core Identity Conflict

* **Problem:** When querying history, multiple rows share the same Primary Key (`id`). EF Core's Change Tracker would normally merge these into a single object.

* **Solution:** Used `.AsNoTracking()` in the `TodoController` to force EF Core to treat every historical snapshot as a unique data point.



### PostgreSQL Case Sensitivity (WSL/Linux)

* **Problem:** Developing in a Windows environment but deploying to PostgreSQL on **WSL (Ubuntu)** revealed strict case-sensitivity issues (e.g., `TodoItems` vs `todo_item`).

* **Solution:** Standardized all database objects to lowercase `snake_case` and used explicit `HasColumnName` mappings in `AppDbContext`.



---



##  Summary of Achievement

1. **Automated Audit Trail:** No manual code is required to "copy" rows; the database handles it natively.

2. **Data Integrity:** Used a compound **Unique Constraint** on `(entity_id, version)` to prevent history corruption.

3. **Optimized Performance:** The "Live" table stays small and fast, as historical bloat is moved to a secondary schema.



