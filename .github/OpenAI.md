````markdown
# OpenAI usage rules (stub)

Purpose: short, actionable guidance for generating code and prompts when using OpenAI models (GPT family).

Core rules (follow canonical secure .NET guidance in `.github/AGENTS.md`):

- Never hardcode API keys or secrets. Use `IConfiguration`/environment variables/secret stores.
- Validate all model outputs before treating them as trusted data (especially any generated code, SQL, or shell commands).
- Apply input validation and output encoding per OWASP guidance when inserting model output into HTML, SQL, or file paths.
- Log interactions at a high level only (do not log secrets or PII). Use request IDs for traceability.

Provider-specific guidance:

- Authentication: prefer short-lived API keys or token-based auth; store keys in secure vaults.
- Rate limits: implement exponential backoff and retry with jitter; surface rate-limit errors to the caller.
- Model selection: choose appropriate model for task (e.g., code generation -> gpt-4/codecushion or similar); keep a fallback to a smaller model for cost control.
- System prompts: use system-level instructions for safety constraints and disallowed actions.
- Response validation: treat the model as untrusted; run static analysis on generated code and compile/test in sandbox before executing.

````
