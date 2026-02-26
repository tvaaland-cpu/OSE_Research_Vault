CREATE INDEX IF NOT EXISTS idx_document_workspace_company_imported
    ON document(workspace_id, company_id, COALESCE(imported_at, created_at) DESC);

CREATE INDEX IF NOT EXISTS idx_note_workspace_company_created
    ON note(workspace_id, company_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_agent_run_workspace_started
    ON agent_run(workspace_id, started_at DESC);
