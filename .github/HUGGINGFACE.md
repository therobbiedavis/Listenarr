````markdown
# Hugging Face usage rules (stub)

Purpose: guidance for self-hosted or API-based Hugging Face models.

Core rules (see `.github/AGENTS.md`):

- Don't commit model tokens or credentials. Use secure configuration sources.
- Validate outputs and apply content filters before using generated text in UI or executing generated code.

Provider specifics:

- Self-hosting: when running models in-house, enforce network isolation, resource limits, and request-size validation to avoid DoS.
- Model provenance: record model id and version used for reproducibility and auditing.
- Licensing: verify model license compliance before using and distributing model outputs.

````
