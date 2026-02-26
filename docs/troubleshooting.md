# Troubleshooting

This page covers common day-to-day issues for local development and analyst use.

## Database lock issues (SQLite)

Symptoms:

- Save/import intermittently fails with a "database is locked" style message.
- Operations succeed after retry.

What to do:

1. Close any extra app instances pointed at the same DB file.
2. Pause external tools that may hold the file (backup sync, indexing, antivirus scan).
3. Retry the operation after a few seconds.
4. If needed, restart the app to release stale handles.

Prevention:

- Prefer one active writer session per workspace.
- Avoid placing the live DB in aggressively synchronized folders.

## Import issues

Symptoms:

- Document import completes but content is missing/partial.
- Import fails for a specific file repeatedly.

What to do:

1. Re-open the source file manually and verify it is not corrupted/password-protected.
2. Rename file to a simple path/name (remove unusual characters) and retry.
3. Re-import from a local disk path (not network share/cloud temp path).
4. Check app logs for extraction or parsing errors.

Quality checks:

- Confirm the imported document appears in lists.
- Confirm snippets/search can reference extracted content.

## Merge and CI tips

When contributing changes:

1. Sync your branch with `main` early and often to reduce conflict size.
2. Keep docs/content changes in focused commits.
3. Run the same quality gates as CI before pushing:
   - `dotnet restore`
   - `dotnet build -c Release`
   - `dotnet test -c Release`

If CI fails:

- Re-run locally in Release with a clean restore.
- Read the first failing test/build error and fix from there (later failures are often cascading).
- If failure is environment-specific, document it clearly in PR notes with reproduction steps.

## Last-resort recovery

If workspace behavior looks inconsistent and normal retries fail:

1. Export a workspace backup (if app can still open).
2. Close the app.
3. Back up the DB file externally.
4. Reopen and validate with a minimal action (open company, read note, run search).

Escalate with:

- Exact error text
- Time window
- File involved
- Recent actions performed right before failure
