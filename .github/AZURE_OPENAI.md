````markdown
# Azure OpenAI usage rules (stub)

Purpose: guidance for integrating Microsoft Azure-hosted OpenAI models while following enterprise controls.

Core rules (see `.github/AGENTS.md`):

- Use managed identities or Key Vault for storing keys; avoid long-lived credentials in code.
- Validate and sanitize all model outputs before use.

Provider specifics:

- Authentication: prefer Azure Key Vault + Managed Identity for retrieval of deployment keys.
- Telemetry & compliance: ensure logs do not contain PII or secrets; follow enterprise compliance settings.
- Network: restrict outbound network access from services using the model when possible.

````
