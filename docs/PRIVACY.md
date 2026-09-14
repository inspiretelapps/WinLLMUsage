# Privacy

WinLLMUsage collects usage locally. There is no account system and no traffic to the upstream author's telemetry project.

- Credentials, cookies, and API keys never appear in CLI stdout, local HTTP responses, logs (redacted), or history sync documents.
- The local API binds `127.0.0.1:6736` only. CORS is permissive (`*`) for GET/OPTIONS so browser pages on this machine can read usage numbers — the same compatibility default as OpenUsage. Turn the API off with `winllmusage.localApi.enabled = false` in settings.
- Privacy Mode hides values in the tray tooltip, dashboard, floating strip, and notifications. It does not detect every capture application.
- Folder sync (when enabled) writes machine-local normalized history only. No raw prompts, credentials, or quota snapshots.
- Companion databases are opened read-only.
