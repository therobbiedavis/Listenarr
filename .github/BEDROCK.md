````markdown
# Amazon Bedrock usage rules (stub)

Purpose: guidance for using Amazon Bedrock and managed foundation models.

Core rules (see `.github/AGENTS.md`):

- Do not commit AWS credentials. Use IAM roles and temporary credentials.
- Treat model outputs as untrusted; validate before use.

Provider specifics:

- Authentication: use IAM roles (EC2/ECS/Lambda) or temporary STS credentials; avoid embedding keys.
- Resource & cost: watch usage and cost; implement safeguards for runaway usage.
- Data handling & compliance: ensure you follow AWS data protection and regional compliance rules.

````
