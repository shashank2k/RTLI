CREATE TABLE IF NOT EXISTS logs (
id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
ts timestamptz NOT NULL,
level text NOT NULL,
app text NOT NULL,
message text NOT NULL,
user_id text NULL,
trace_id text NULL,
context jsonb NULL,
ingested_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_logs_ts ON logs (ts);
CREATE INDEX IF NOT EXISTS ix_logs_app_ts ON logs (app, ts);
CREATE INDEX IF NOT EXISTS ix_logs_level_ts ON logs (level, ts);