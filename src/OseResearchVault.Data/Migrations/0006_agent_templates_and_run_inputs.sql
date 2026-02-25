ALTER TABLE agent ADD COLUMN goal TEXT;
ALTER TABLE agent ADD COLUMN company_id TEXT;
ALTER TABLE agent ADD COLUMN allowed_tools_json TEXT NOT NULL DEFAULT '[]';
ALTER TABLE agent ADD COLUMN output_schema TEXT;
ALTER TABLE agent ADD COLUMN evidence_policy TEXT;

ALTER TABLE agent_run ADD COLUMN company_id TEXT;
ALTER TABLE agent_run ADD COLUMN query_text TEXT;
ALTER TABLE agent_run ADD COLUMN selected_document_ids_json TEXT NOT NULL DEFAULT '[]';

CREATE INDEX IF NOT EXISTS idx_agent_run_company_id ON agent_run(company_id);
