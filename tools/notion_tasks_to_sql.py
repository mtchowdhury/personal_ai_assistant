#!/usr/bin/env python3
"""
Turns a Notion "Task Manager" CSV export into a SQL script that loads the rows into
dtasks.Tasks for one user.

    python3 tools/notion_tasks_to_sql.py \
        --export ~/Downloads/taskmanager_notion_export \
        --email you@example.com \
        -o dtasks_import.sql

The generated script is one transaction: it either loads everything or leaves the
database untouched. It resolves the user by email and seeds the statuses/tags the
app would otherwise create on first use, so it works against a database where the
module has never been opened.

Notion quirks handled here:
  - "Noon" is not one of our time-of-day slots; it maps to Afternoon.
  - Tags come out lowercase ("households"); they are mapped to the app's casing.
  - Parent links carry the parent's Notion page id, which is what subtasks are
    matched on -- titles repeat in the export and would be ambiguous.
  - A blank Status means Done: the export is overwhelmingly finished work.
"""

import argparse
import csv
import glob
import os
import re
import sys
from datetime import datetime

# Notion slot -> our TaskTimeOfDay enum. Noon has no direct equivalent.
TIME_OF_DAY = {
    'morning': 'Morning',
    'noon': 'Afternoon',
    'afternoon': 'Afternoon',
    'evening': 'Evening',
    'night': 'Night',
}

# Our TaskPriority enum, stored as an int.
PRIORITY = {'low': 0, 'medium': 1, 'high': 2}
DEFAULT_PRIORITY = 1  # Medium

# Our TaskCategory enum, stored as an int.
CATEGORY = {'task': 0, 'subtask': 1}

# Notion status -> the status names the app seeds.
STATUS = {
    'to do': 'To do',
    'todo': 'To do',
    'in progress': 'In Progress',
    'on hold': 'On Hold',
    'blocked': 'Blocked',
    'done': 'Done',
}
# The export is overwhelmingly completed work, so an unset status means Done.
DEFAULT_STATUS = 'Done'

# Notion tag -> the tag names the app seeds.
TAGS = {
    'households': 'Households',
    'household': 'Households',
    'personal': 'Personal',
    'learning': 'Learning',
    'health': 'Health',
    'finance': 'Finance',
}

# Property lines in the per-task markdown pages; everything else is body text.
MD_PROPERTIES = (
    'Category:', 'Date:', 'Status:', 'Priority:', 'Tags:', 'Time of day:',
    'Parent task:', 'Sub-tasks:', 'Subtasks:', 'Approax duration', 'Approx duration',
)


def sql_str(value):
    """Quotes a value as a SQL string literal, or NULL when empty."""
    if value is None or value == '':
        return 'NULL'
    return "'" + str(value).replace("'", "''") + "'"


def sql_text_array(values):
    """Renders a Postgres text[] literal."""
    if not values:
        return "ARRAY[]::text[]"
    return "ARRAY[" + ", ".join(sql_str(v) for v in values) + "]"


def notion_page_id(link):
    """
    Pulls the 32-char page id out of a Notion relation link or filename, e.g.
    "make paste (Task%20Manager/make%20paste%203247b7ee....csv)" -> "3247b7ee...".

    The id is anchored to the end (before the .csv/.md extension) rather than
    matched anywhere in the string: a plain 32-hex search starts inside the
    URL-encoded "%20" separator and returns an id shifted by two characters.
    """
    if not link:
        return None
    match = re.search(r'([0-9a-f]{32})(?:\.[a-z]+)?\)?\s*$', link.strip())
    if match:
        return match.group(1)
    # Fall back to the last 32-hex run anywhere in the string.
    matches = re.findall(r'[0-9a-f]{32}', link)
    return matches[-1] if matches else None


def parse_date(value):
    """Notion writes 'March 15, 2026'. Returns an ISO date, or None."""
    value = (value or '').strip()
    if not value:
        return None
    # Some rows carry a time range after the date ("March 15, 2026 9:00 AM").
    value = value.split('→')[0].strip()
    for fmt in ("%B %d, %Y", "%B %d, %Y %I:%M %p"):
        try:
            return datetime.strptime(value, fmt).date().isoformat()
        except ValueError:
            continue
    return None


def parse_duration(value):
    """'90' or '90 mins' -> 90. Returns None when absent or not a number."""
    value = (value or '').strip()
    if not value:
        return None
    match = re.search(r'\d+', value)
    return int(match.group(0)) if match else None


def load_markdown_notes(export_dir):
    """
    Reads each exported page's body text, keyed by Notion page id. The body is
    everything after the title and the property block -- checklists, notes, links.
    """
    notes = {}
    page_dir = os.path.join(export_dir, 'Task Manager')
    if not os.path.isdir(page_dir):
        return notes

    for path in glob.glob(os.path.join(page_dir, '*.md')):
        page_id = notion_page_id(os.path.basename(path))
        if not page_id:
            continue

        body = []
        for line in open(path, encoding='utf-8').read().splitlines():
            stripped = line.strip()
            if not stripped or stripped.startswith('# '):
                continue
            if stripped.startswith(MD_PROPERTIES):
                continue
            body.append(line.rstrip())

        # Trim blank lines that the property block leaves behind.
        while body and not body[0].strip():
            body.pop(0)
        while body and not body[-1].strip():
            body.pop()

        if body:
            notes[page_id] = "\n".join(body)

    return notes


def find_csv(export_dir):
    """Prefers the '_all' export, which includes every row rather than one view."""
    candidates = sorted(glob.glob(os.path.join(export_dir, '*_all.csv')))
    if candidates:
        return candidates[0]
    candidates = sorted(glob.glob(os.path.join(export_dir, '*.csv')))
    if not candidates:
        sys.exit(f"No CSV found in {export_dir}")
    return candidates[0]


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--export', required=True, help='the unzipped Notion export folder')
    ap.add_argument('--email', required=True, help='email of the user to import for')
    ap.add_argument('-o', '--out', default='dtasks_import.sql', help='SQL file to write')
    args = ap.parse_args()

    export_dir = os.path.expanduser(args.export)
    csv_path = find_csv(export_dir)
    notes_by_page = load_markdown_notes(export_dir)

    rows = list(csv.DictReader(open(csv_path, encoding='utf-8-sig')))

    tasks = []
    warnings = []
    seen_ids = set()

    for index, row in enumerate(rows):
        title = (row.get('Task') or '').strip()
        if not title:
            warnings.append(f"row {index + 2}: blank title, skipped")
            continue

        raw_status = (row.get('Status') or '').strip().lower()
        status_name = STATUS.get(raw_status, DEFAULT_STATUS)
        if raw_status and raw_status not in STATUS:
            warnings.append(f"row {index + 2}: unknown status {row['Status']!r} -> {DEFAULT_STATUS}")

        raw_priority = (row.get('Priority') or '').strip().lower()
        priority = PRIORITY.get(raw_priority, DEFAULT_PRIORITY)
        if raw_priority and raw_priority not in PRIORITY:
            warnings.append(f"row {index + 2}: unknown priority {row['Priority']!r} -> Medium")

        raw_category = (row.get('Category') or '').strip().lower()
        parent_link = (row.get('Parent task') or '').strip()
        # A row with a parent is a Subtask even when the column is blank.
        category = CATEGORY.get(raw_category, 1 if parent_link else 0)

        raw_slot = (row.get('Time of day') or '').strip().lower()
        slot = TIME_OF_DAY.get(raw_slot)
        if raw_slot and slot is None:
            warnings.append(f"row {index + 2}: unknown time of day {row['Time of day']!r} -> none")
        slot_index = ['Morning', 'Afternoon', 'Evening', 'Night'].index(slot) if slot else None

        tag_names = []
        for tag in (row.get('Tags') or '').split(','):
            tag = tag.strip()
            if not tag:
                continue
            mapped = TAGS.get(tag.lower(), tag)
            if mapped not in tag_names:
                tag_names.append(mapped)

        tasks.append({
            'title': title,
            'date': parse_date(row.get('Date')),
            'duration': parse_duration(row.get('Approax duration(mins)') or row.get('Approx duration(mins)')),
            'status': status_name,
            'priority': priority,
            'category': category,
            'slot': slot_index,
            'tags': tag_names,
            'parent_page': notion_page_id(parent_link),
            'parent_title': re.sub(r'\s*\(.*\)\s*$', '', parent_link).strip(),
            'row': index,
        })

    # Match each task to its exported markdown page so bodies become Notes, and so
    # subtasks can be linked by page id. Titles repeat, so a title that maps to
    # several pages is left unlinked rather than guessed.
    page_ids_by_title = {}
    page_dir = os.path.join(export_dir, 'Task Manager')
    if os.path.isdir(page_dir):
        for path in glob.glob(os.path.join(page_dir, '*.md')):
            base = os.path.basename(path)
            pid = notion_page_id(base)
            name = re.sub(r'\s+[0-9a-f]{32}\.md$', '', base)
            page_ids_by_title.setdefault(name.strip().lower(), []).append(pid)

    for task in tasks:
        matches = page_ids_by_title.get(task['title'].lower(), [])
        task['page_id'] = matches[0] if len(matches) == 1 else None
        task['notes'] = notes_by_page.get(task['page_id']) if task['page_id'] else None

    # Drop self-referencing parents. Notion allows a task to list itself in the
    # Parent task column; carried over it would be an invalid row.
    for task in tasks:
        if not task['parent_page']:
            continue
        if task['parent_page'] == task['page_id'] or task['parent_title'].lower() == task['title'].lower():
            warnings.append(f"row {task['row'] + 2}: {task['title']!r} lists itself as its parent, link dropped")
            task['parent_page'] = None
            task['category'] = 0

    # Emit the script.
    out = []
    w = out.append

    w("-- Generated by tools/notion_tasks_to_sql.py -- do not edit by hand.")
    w(f"-- Source: {os.path.basename(csv_path)}")
    w(f"-- Tasks:  {len(tasks)}")
    w(f"-- Target: {args.email}")
    w("--")
    w("-- Runs as a single transaction: it either loads every row or changes nothing.")
    w("-- Safe to run against a database where the Task Manager has never been opened;")
    w("-- the statuses and tags the app seeds on first use are created here if missing.")
    w("")
    w("-- ON_ERROR_STOP is a psql meta-command and must precede BEGIN, so that any")
    w("-- failure below aborts the script instead of leaving the transaction open.")
    w("\\set ON_ERROR_STOP on")
    w("")
    w("BEGIN;")
    w("")
    w("-- Resolve the target user, and fail loudly rather than importing nowhere.")
    w("DO $$")
    w("DECLARE")
    w("    v_user uuid;")
    w("BEGIN")
    w(f"    SELECT \"Id\" INTO v_user FROM identity.\"Users\" WHERE lower(\"Email\") = lower({sql_str(args.email)});")
    w("    IF v_user IS NULL THEN")
    w(f"        RAISE EXCEPTION 'No user with email {args.email}';")
    w("    END IF;")
    w("END $$;")
    w("")
    w("-- Refuse to run twice. The script has no dedup of its own, so a second run would")
    w("-- silently double every task; clear the previous import first if that is intended:")
    w("--   DELETE FROM dtasks.\"Tasks\" WHERE \"Source\" = 'notion-import';")
    w("DO $$")
    w("DECLARE")
    w("    v_existing int;")
    w("BEGIN")
    w("    SELECT COUNT(*) INTO v_existing")
    w("    FROM dtasks.\"Tasks\" t")
    w("    JOIN identity.\"Users\" usr ON usr.\"Id\" = t.\"UserId\"")
    w(f"    WHERE lower(usr.\"Email\") = lower({sql_str(args.email)}) AND t.\"Source\" = 'notion-import';")
    w("    IF v_existing > 0 THEN")
    w("        RAISE EXCEPTION 'This user already has % notion-import task(s). Delete them first to re-import.', v_existing;")
    w("    END IF;")
    w("END $$;")
    w("")
    w("-- A temp table keeps the user id out of every statement below.")
    w("CREATE TEMP TABLE _import_user ON COMMIT DROP AS")
    w(f"SELECT \"Id\" AS user_id FROM identity.\"Users\" WHERE lower(\"Email\") = lower({sql_str(args.email)});")
    w("")

    w("-- Seed the statuses the app would create on first use. Existing rows are kept")
    w("-- as they are, so a status the user has already renamed or recoloured is untouched.")
    w("INSERT INTO dtasks.\"TaskStatuses\" (\"Id\", \"UserId\", \"Name\", \"SortOrder\", \"Color\", \"IsDone\", \"IsProtected\", \"CreatedOn\", \"CreatedBy\")")
    w("SELECT gen_random_uuid(), u.user_id, s.name, s.sort_order, s.color, s.is_done, s.is_protected, now(), u.user_id")
    w("FROM _import_user u")
    w("CROSS JOIN (VALUES")
    seeds = [("To do", 0, "#6b7280", "false", "true"),
             ("In Progress", 1, "#2f6fed", "false", "false"),
             ("On Hold", 2, "#c98a2b", "false", "false"),
             ("Blocked", 3, "#c0453b", "false", "false"),
             ("Done", 4, "#3f9c6a", "true", "true")]
    for i, (name, order, color, is_done, prot) in enumerate(seeds):
        comma = "," if i < len(seeds) - 1 else ""
        w(f"    ({sql_str(name)}, {order}, {sql_str(color)}, {is_done}, {prot}){comma}")
    w(") AS s(name, sort_order, color, is_done, is_protected)")
    w("WHERE NOT EXISTS (")
    w("    SELECT 1 FROM dtasks.\"TaskStatuses\" x")
    w("    WHERE x.\"UserId\" = u.user_id AND lower(x.\"Name\") = lower(s.name)")
    w(");")
    w("")

    w("-- Seed the tag pick-list the same way.")
    w("INSERT INTO dtasks.\"TaskTags\" (\"Id\", \"UserId\", \"Name\", \"Color\", \"CreatedOn\", \"CreatedBy\")")
    w("SELECT gen_random_uuid(), u.user_id, t.name, t.color, now(), u.user_id")
    w("FROM _import_user u")
    w("CROSS JOIN (VALUES")
    tag_seeds = [("Households", "#8b5cf6"), ("Personal", "#2f6fed"), ("Learning", "#0d9488"),
                 ("Health", "#c0453b"), ("Finance", "#c98a2b")]
    for i, (name, color) in enumerate(tag_seeds):
        comma = "," if i < len(tag_seeds) - 1 else ""
        w(f"    ({sql_str(name)}, {sql_str(color)}){comma}")
    w(") AS t(name, color)")
    w("WHERE NOT EXISTS (")
    w("    SELECT 1 FROM dtasks.\"TaskTags\" x")
    w("    WHERE x.\"UserId\" = u.user_id AND lower(x.\"Name\") = lower(t.name)")
    w(");")
    w("")

    w("-- Staging table: the export as-is, with the Notion page id kept so parent/child")
    w("-- links can be resolved after every row has an app-side id.")
    w("CREATE TEMP TABLE _import_tasks (")
    w("    new_id        uuid NOT NULL DEFAULT gen_random_uuid(),")
    w("    page_id       text,")
    w("    parent_page   text,")
    w("    title         text NOT NULL,")
    w("    notes         text,")
    w("    category      int  NOT NULL,")
    w("    priority      int  NOT NULL,")
    w("    scheduled_on  date,")
    w("    time_of_day   int,")
    w("    duration      int,")
    w("    status_name   text NOT NULL,")
    w("    tags          text[] NOT NULL,")
    w("    sort_order    int NOT NULL")
    w(") ON COMMIT DROP;")
    w("")

    w("INSERT INTO _import_tasks")
    w("    (page_id, parent_page, title, notes, category, priority, scheduled_on,")
    w("     time_of_day, duration, status_name, tags, sort_order)")
    w("VALUES")

    values = []
    for order, t in enumerate(tasks):
        values.append(
            "    (" + ", ".join([
                sql_str(t['page_id']),
                sql_str(t['parent_page']),
                sql_str(t['title']),
                sql_str(t['notes']),
                str(t['category']),
                str(t['priority']),
                f"DATE {sql_str(t['date'])}" if t['date'] else "NULL",
                str(t['slot']) if t['slot'] is not None else "NULL",
                str(t['duration']) if t['duration'] is not None else "NULL",
                sql_str(t['status']),
                sql_text_array(t['tags']),
                str(order),
            ]) + ")"
        )
    w(",\n".join(values) + ";")
    w("")

    w("-- Load the tasks. Parents land first so a subtask's ParentId always resolves;")
    w("-- ParentId is filled in afterwards from the staged page ids.")
    w("INSERT INTO dtasks.\"Tasks\" (")
    w("    \"Id\", \"UserId\", \"Title\", \"Notes\", \"Category\", \"Priority\", \"ScheduledOn\",")
    w("    \"TimeOfDay\", \"ApproxDurationMinutes\", \"StatusId\", \"ParentId\", \"Tags\",")
    w("    \"CompletedOn\", \"SortOrder\", \"Source\", \"CreatedOn\", \"CreatedBy\"")
    w(")")
    w("SELECT")
    w("    i.new_id,")
    w("    u.user_id,")
    w("    i.title,")
    w("    i.notes,")
    w("    i.category,")
    w("    i.priority,")
    w("    -- Stored as midnight UTC, matching NormalizeDate() in DTaskService: the")
    w("    -- scheduled day is a calendar date and must not shift across a day boundary.")
    w("    -- Note timezone(\'UTC\', <timestamp>), not \'<date> AT TIME ZONE UTC\' -- the")
    w("    -- latter reads midnight as UTC and renders it in the server\'s zone, moving")
    w("    -- every date back a day in any zone east of UTC.")
    w("    timezone('UTC', i.scheduled_on::timestamp),")
    w("    i.time_of_day,")
    w("    i.duration,")
    w("    s.\"Id\",")
    w("    NULL,")
    w("    i.tags,")
    w("    CASE WHEN s.\"IsDone\" THEN now() ELSE NULL END,")
    w("    i.sort_order,")
    w("    'notion-import',")
    w("    now(),")
    w("    u.user_id")
    w("FROM _import_tasks i")
    w("CROSS JOIN _import_user u")
    w("JOIN dtasks.\"TaskStatuses\" s")
    w("  ON s.\"UserId\" = u.user_id AND lower(s.\"Name\") = lower(i.status_name);")
    w("")

    w("-- Link subtasks to their parents now that every row has an id.")
    w("UPDATE dtasks.\"Tasks\" t")
    w("SET \"ParentId\" = parent.new_id")
    w("FROM _import_tasks child")
    w("JOIN _import_tasks parent ON parent.page_id = child.parent_page")
    w("WHERE t.\"Id\" = child.new_id")
    w("  AND child.parent_page IS NOT NULL;")
    w("")

    w("-- Summary, so the run is verifiable before committing.")
    w("SELECT")
    w("    (SELECT COUNT(*) FROM dtasks.\"Tasks\" t JOIN _import_user u ON t.\"UserId\" = u.user_id")
    w("      WHERE t.\"Source\" = 'notion-import') AS imported,")
    w("    (SELECT COUNT(*) FROM dtasks.\"Tasks\" t JOIN _import_user u ON t.\"UserId\" = u.user_id")
    w("      WHERE t.\"Source\" = 'notion-import' AND t.\"ParentId\" IS NOT NULL) AS subtasks_linked,")
    w("    (SELECT COUNT(*) FROM dtasks.\"Tasks\" t JOIN _import_user u ON t.\"UserId\" = u.user_id")
    w("      WHERE t.\"Source\" = 'notion-import' AND t.\"Notes\" IS NOT NULL) AS with_notes;")
    w("")
    w("COMMIT;")
    w("")

    with open(args.out, 'w', encoding='utf-8') as fh:
        fh.write("\n".join(out))

    linked = sum(1 for t in tasks if t['parent_page'])
    with_notes = sum(1 for t in tasks if t['notes'])
    print(f"Wrote {args.out}")
    print(f"  tasks:      {len(tasks)}")
    print(f"  subtasks:   {linked}")
    print(f"  with notes: {with_notes}")
    if warnings:
        print(f"  warnings:   {len(warnings)}")
        for warning in warnings[:12]:
            print(f"    - {warning}")


if __name__ == '__main__':
    main()
