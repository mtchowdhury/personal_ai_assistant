# tools

## notion_tasks_to_sql.py

Turns a Notion "Task Manager" CSV export into a SQL script that loads the rows into
`dtasks.Tasks` for one user.

```bash
python3 tools/notion_tasks_to_sql.py \
    --export ~/Downloads/taskmanager_notion_export \
    --email you@example.com \
    -o dtasks_import.sql
```

Then run the script against the database:

```bash
psql -h localhost -U <user> -d cmdnext -f dtasks_import.sql
```

The generated script is a single transaction — it loads every row or changes nothing —
and refuses to run twice, since it has no row-level dedup. To re-import, clear the
previous one first:

```sql
DELETE FROM dtasks."Tasks" WHERE "Source" = 'notion-import';
```

Imported tasks carry `Source = 'notion-import'`, which is what distinguishes them from
rows created in the app (`manual`) or by the AI (`ai`).

### How the export maps

| Notion            | App                                                       |
|-------------------|-----------------------------------------------------------|
| Task              | `Title`                                                   |
| page body (`.md`) | `Notes` — everything after the title and property block   |
| Category          | `Category` (Task/Subtask; a row with a parent is a Subtask)|
| Date              | `ScheduledOn`, stored midnight UTC                        |
| Priority          | `Priority`, blank → Medium                                |
| Status            | resolved by name against the user's statuses, blank → Done |
| Tags              | `Tags text[]`, recased to the user's tag list             |
| Time of day       | `TimeOfDay`; **Noon → Afternoon**, which has no equivalent |
| Parent task       | `ParentId`, matched on the Notion page id, not the title  |
| Approax duration  | `ApproxDurationMinutes`                                   |

Statuses and tags the app would seed on first use are created by the script if absent,
so it works against a database where the Task Manager has never been opened. Existing
rows are left alone — a status you have already renamed or recoloured is not touched.

Anything the importer cannot map is reported as a warning rather than dropped silently
(an unknown status, a task listing itself as its own parent, a blank title).
