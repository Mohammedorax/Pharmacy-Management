"""Minimal read-only MCP server for the plan SQLite index.

Stdlib only: no packages, no network, no account. Speaks newline-delimited JSON-RPC 2.0 on stdio.

Tools
    schema  - lists tables and columns
    query   - runs one read-only SELECT/WITH/PRAGMA table_info statement, capped at max rows
"""

import argparse
import json
import sqlite3
import sys

READ_ONLY_PREFIXES = ("select", "with", "pragma table_info", "pragma table_list")
FORBIDDEN = (
    "insert", "update", "delete", "drop", "alter", "create", "attach", "detach",
    "replace", "vacuum", "reindex", "truncate",
)


def _open(db_path):
    return sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, timeout=10)


def _schema(db_path):
    connection = _open(db_path)
    try:
        lines = []
        for name, sql in connection.execute(
            "SELECT name, sql FROM sqlite_master WHERE type='table' ORDER BY name"
        ):
            lines.append(f"TABLE {name}")
            if sql:
                lines.append(sql)
        for name, in connection.execute(
            "SELECT name FROM sqlite_master WHERE type='index' AND sql IS NOT NULL ORDER BY name"
        ):
            lines.append(f"INDEX {name}")
        return "\n".join(lines)
    finally:
        connection.close()


def _validate(sql):
    text = sql.strip()
    if not text:
        raise ValueError("empty statement")
    body = text.rstrip(";").strip()
    if ";" in body:
        raise ValueError("only one statement is allowed")
    lowered = body.lower()
    if not lowered.startswith(READ_ONLY_PREFIXES):
        raise ValueError("only SELECT, WITH and PRAGMA table_info are allowed")
    for word in FORBIDDEN:
        if word in lowered:
            raise ValueError(f"forbidden keyword: {word}")
    return body


def _query(db_path, sql, max_rows):
    statement = _validate(sql)
    connection = _open(db_path)
    try:
        cursor = connection.execute(statement)
        columns = [d[0] for d in cursor.description] if cursor.description else []
        rows = cursor.fetchmany(max_rows + 1)
        truncated = len(rows) > max_rows
        rows = rows[:max_rows]
        parts = []
        if columns:
            parts.append(" | ".join(columns))
            for row in rows:
                parts.append(" | ".join("NULL" if v is None else str(v) for v in row))
            parts.append(f"({len(rows)} rows{' , truncated' if truncated else ''})")
        else:
            parts.append("(statement returned no result set)")
        return "\n".join(parts)
    finally:
        connection.close()


def _tools():
    return [
        {
            "name": "query",
            "description": "Run one read-only SQL statement against the plan index "
            "(table items: id, source, line, section, state, text, evidence, has_evidence).",
            "inputSchema": {
                "type": "object",
                "properties": {
                    "sql": {"type": "string", "description": "SELECT/WITH/PRAGMA table_info only."},
                    "maxRows": {"type": "integer", "description": "Default 100, max 500."},
                },
                "required": ["sql"],
            },
        },
        {
            "name": "schema",
            "description": "Describe the plan index schema (tables, columns, indexes).",
            "inputSchema": {"type": "object", "properties": {}},
        },
    ]


def _respond(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", required=True)
    parser.add_argument("--max-rows", type=int, default=100)
    args = parser.parse_args()

    for raw in sys.stdin.buffer:
        line = raw.decode("utf-8", errors="replace").strip()
        if not line:
            continue
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            _respond({"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "parse error"}})
            continue

        if "id" not in message:
            continue

        message_id = message.get("id")
        method = message.get("method", "")
        params = message.get("params") or {}

        try:
            if method == "initialize":
                _respond({
                    "jsonrpc": "2.0",
                    "id": message_id,
                    "result": {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {"tools": {}},
                        "serverInfo": {"name": "pharmacy-plan", "version": "1.0.0"},
                    },
                })
            elif method == "ping":
                _respond({"jsonrpc": "2.0", "id": message_id, "result": {}})
            elif method == "tools/list":
                _respond({"jsonrpc": "2.0", "id": message_id, "result": {"tools": _tools()}})
            elif method == "tools/call":
                name = params.get("name", "")
                arguments = params.get("arguments") or {}
                if name == "schema":
                    text = _schema(args.db)
                elif name == "query":
                    sql = arguments.get("sql")
                    if not isinstance(sql, str):
                        raise ValueError("sql is required")
                    requested = int(arguments.get("maxRows") or args.max_rows)
                    text = _query(args.db, sql, max(1, min(requested, 500)))
                else:
                    _respond({
                        "jsonrpc": "2.0",
                        "id": message_id,
                        "error": {"code": -32602, "message": f"unknown tool: {name}"},
                    })
                    continue
                _respond({
                    "jsonrpc": "2.0",
                    "id": message_id,
                    "result": {"content": [{"type": "text", "text": text}], "isError": False},
                })
            else:
                _respond({
                    "jsonrpc": "2.0",
                    "id": message_id,
                    "error": {"code": -32601, "message": f"method not found: {method}"},
                })
        except Exception as error:
            _respond({
                "jsonrpc": "2.0",
                "id": message_id,
                "result": {"content": [{"type": "text", "text": f"ERROR: {error}"}], "isError": True},
            })

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
