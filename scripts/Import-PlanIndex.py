#!/usr/bin/env python3
"""Loads the plan index JSON into SQLite for the local `plan` MCP server.

Usage:
    python scripts/Import-PlanIndex.py <items.json> <database.sqlite>

Only stdlib is used (no extra packages, no account).
"""
import json
import os
import sqlite3
import sys


SCHEMA = """
DROP TABLE IF EXISTS items;
CREATE TABLE items (
    id            TEXT PRIMARY KEY,
    source        TEXT NOT NULL,
    line          INTEGER NOT NULL,
    section       TEXT,
    section_level INTEGER,
    state         TEXT NOT NULL CHECK (state IN ('open', 'done')),
    text          TEXT NOT NULL,
    evidence      TEXT NOT NULL DEFAULT '',
    has_evidence  INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX ix_items_state ON items(state);
CREATE INDEX ix_items_section ON items(section);
CREATE INDEX ix_items_evidence ON items(has_evidence);
"""


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: Import-PlanIndex.py <items.json> <database.sqlite>")
        return 2

    items_path, database_path = sys.argv[1], sys.argv[2]
    with open(items_path, encoding="utf-8") as handle:
        items = json.load(handle)
    if not isinstance(items, list):
        raise SystemExit("items.json must contain a list")

    directory = os.path.dirname(os.path.abspath(database_path))
    if directory:
        os.makedirs(directory, exist_ok=True)

    if not items:
        raise SystemExit("items.json is empty; refusing to replace the plan index")
    ids = [item["id"] for item in items]
    if len(ids) != len(set(ids)):
        raise SystemExit("items.json contains duplicate ids; refusing to replace the plan index")
    temp_path = database_path + ".tmp"
    if os.path.exists(temp_path):
        os.remove(temp_path)
    connection = sqlite3.connect(temp_path)
    try:
        connection.executescript(SCHEMA)
        connection.executemany(
            """INSERT INTO items
               (id, source, line, section, section_level, state, text, evidence, has_evidence)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            [
                (
                    item["id"],
                    item["source"],
                    int(item["line"]),
                    item.get("section", ""),
                    int(item.get("section_level", 0)),
                    item["state"],
                    item["text"],
                    "|".join(item.get("evidence") or []),
                    1 if item.get("has_evidence") else 0,
                )
                for item in items
            ],
        )
        connection.commit()
        total = connection.execute("SELECT COUNT(*) FROM items").fetchone()[0]
        open_count = connection.execute(
            "SELECT COUNT(*) FROM items WHERE state='open'"
        ).fetchone()[0]
        open_no_evidence = connection.execute(
            "SELECT COUNT(*) FROM items WHERE state='open' AND has_evidence=0"
        ).fetchone()[0]
    finally:
        connection.close()
    os.replace(temp_path, database_path)

    print(
        f"PASS plan sqlite: {database_path}; items={total}; open={open_count}; "
        f"open-without-evidence={open_no_evidence}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
