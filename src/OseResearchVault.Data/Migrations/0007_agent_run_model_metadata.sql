ALTER TABLE agent_run ADD COLUMN model_provider TEXT;
ALTER TABLE agent_run ADD COLUMN model_name TEXT;
ALTER TABLE agent_run ADD COLUMN model_parameters_json TEXT NOT NULL DEFAULT '{}';
