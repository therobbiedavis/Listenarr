# Listenarr Rules Index

This folder contains canonical rule files and provider-specific guidance for AI-assisted code generation and usage.

Files:

- `AGENTS.md` — Canonical secure .NET guidance and Vue.js rules (moved from repository root).
- `CLAUDE.md` — Canonical Claude/secure guidance (moved from repository root).
- `OpenAI.md` — OpenAI provider stub (auth, rate limits, validation guidance).
- `ANTHROPIC.md` — Anthropic/Claude provider stub.
- `BARD.md` — Google Bard / PaLM provider stub.
- `COHERE.md` — Cohere provider stub (text/embeddings guidance).
- `HUGGINGFACE.md` — Hugging Face provider stub (self-hosting and API guidance).
- `AZURE_OPENAI.md` — Azure OpenAI provider stub (enterprise guidance).
- `BEDROCK.md` — Amazon Bedrock provider stub.

How to use:

- Keep the canonical security guidance in `AGENTS.md` updated. Provider stubs are intentionally short and reference `AGENTS.md` for full rules.
- When adding a new AI provider, add a small stub here and reference relevant corporate compliance practices.

If you prefer these files to be in a different directory (e.g., `docs/`), tell me and I can move them.
