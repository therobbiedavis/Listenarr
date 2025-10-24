````markdown
# Cohere usage rules (stub)

Purpose: provider-specific guidance for Cohere text/embedding models.

Core rules (see `.github/AGENTS.md`):

- Never store API keys in source control. Use environment variables/secret stores.
- Validate and sanitize model outputs before using them in application logic.

Provider specifics:

- Embeddings: if using embeddings for search, normalize and validate input to avoid prompt injection risks.
- Rate limits and batching: batch embedding requests where possible to reduce cost and rate-limit pressure.
- Data residency: be mindful of data residency policies; do not send sensitive or regulated data without approval.

````
