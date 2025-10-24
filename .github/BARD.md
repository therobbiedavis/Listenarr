````markdown
# Google Bard usage rules (stub)

Purpose: short guidance for interacting with Google Bard / PaLM family.

Core rules (follow `.github/AGENTS.md` for the full-security checklist):

- Do not hardcode keys or service account credentials in repo. Use secure vaults.
- Treat generated content as untrusted; validate before using in code paths, SQL, or files.
- Enforce strict encoding and sanitization of any model output that is inserted into web pages.

Provider specifics:

- Authentication: use OAuth or service accounts where possible, store credentials securely and limit scopes.
- Quotas and rate limiting: implement throttling and graceful degradation.
- Data handling: avoid sending PII unless necessary and consented; redact or pseudonymize when possible.

````
