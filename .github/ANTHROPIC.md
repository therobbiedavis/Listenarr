````markdown
# Anthropic usage rules (stub)

Purpose: short guidance for using Anthropic/Claude-family APIs consistent with project security rules.

Core rules (see `.github/AGENTS.md` for full secure-.NET guidance):

- Never commit or hardcode API keys. Use secure config sources.
- Validate outputs; do not trust model-provided URIs, code, or commands without sanitization.
- Sanitize any content before writing to file system or executing as code.

Provider specifics:

- Authentication: store API keys in environment variables or secret managers; rotate regularly.
- Rate limits & safety: implement retries with backoff; validate content for safety and prohibited categories before acting on it.
- System prompt usage: prefer explicit safety and output format instructions in system prompts.

````
