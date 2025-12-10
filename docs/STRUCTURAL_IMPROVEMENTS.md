# Structural improvements — high level review

This document lists prioritized, actionable structural improvements for the Listenarr codebase (backend C# / frontend Vue) to improve organization, maintainability and testability.

## Top priorities (short, high-impact)
- Enforce layer boundaries: keep Controllers → Application services → Domain → Infrastructure. Controllers must be thin adapters.
- Reduce "God" services: split large services (notably DownloadService) into focused services (adapter routing, download orchestration, finalization, persistence).
- Centralize adapter resolution: use a well-defined factory/interface (already added) and avoid ad-hoc GetServices/Func usage across code.
- Strict DI lifetime rules: ensure adapters that consume scoped resources are registered as scoped; singletons only for truly stateless services.
- Add module-level unit tests and DI registration tests for critical services.

## Backend (C#) recommendations
- Project layering and boundaries
  - Clearly separate projects by responsibility (Domain, Application, Infrastructure, Api). Keep no circular references.
  - Move shared DTOs/contracts into a versioned Api.Contracts package if API evolves.
- Service decomposition
  - Break DownloadService into smaller collaborators:
    - IDownloadOrchestrator (orchestration + state machine)
    - IDownloadClientGateway (adapter factory consumer)
    - IFileFinalizer (moves/renames, permission logic)
    - IDownloadRepository / IUnitOfWork (persistence)
  - Favor composition over single-class branching logic.
- Adapters and plugins
  - Keep adapter implementations small and testable; surface adapter metadata via an interface.
  - Use a single adapter factory (IDownloadClientAdapterFactory) and remove any Func<string, ...> ambiguity.
- Persistence and EF
  - Avoid EF logic bleeding into services; use repositories or application services to encapsulate EF specifics.
  - Prefer IDbContextFactory for background/hosted services (already used) and keep short-lived DbContexts.
- Configuration and options
  - Continue to validate options at startup. Group adapter config under a typed options model and surface errors early.
- Background processing
  - Decouple background queue/channel consumers from processing logic via handlers implementing IProcessingHandler.
  - Make background services resilient with retry/circuit policies and cancellation support.
- Logging, errors, and telemetry
  - Centralize structured logging and redact sensitive data (LogRedaction exists). Add correlation IDs for long-running operations.
- Tests and CI
  - Add unit tests for all service boundaries; add integration tests for download flows.
  - Add a CI step to run layering-check and unit tests; fail build on layering violations.
- API design
  - Thin controllers, explicit DTOs, consistent status codes, and API versioning.
  - Add health checks and metrics endpoints.

## Frontend (Vue + TS) recommendations
- Folder structure & boundaries
  - Keep components/ small and view-focused. Use `views/` for route pages and `components/` for reusable parts.
  - Use `services/api` for HTTP client code and centralize error handling and auth token injection.
- State management
  - Use Pinia (or typed stores) with clear module boundaries and typed store interfaces in `src/stores/`.
- Composition API & composables
  - Prefer Composition API and composables for shared logic (use `fe/src/composables/`).
- Types & contracts
  - Keep TypeScript interfaces mirroring backend DTOs in `src/types/` and generate/update them from API contracts where possible.
- Testing and quality
  - Use Vitest for unit tests and Cypress for e2e (already present). Add CI linting, typecheck, and unit tests.
- Performance
  - Lazy-load routes, split large components, and keep large resources out of bundle.
- Tooling
  - Enforce lint/prettier rules and add pre-commit hooks for formatting.

## Quick wins
- Add a short architecture overview (diagram + boundaries) in docs/ARCHITECTURE.md.
- Introduce small module refactors: extract adapter factory usage, split DownloadService into two classes.
- Add DI registration unit tests to catch mismatches early.
- Run layering-check in CI and fail builds when layering rules are violated.

## Migration plan (concrete steps)
- Phase 1 — low friction
  - Add architecture doc + CI layering-check step.
  - Add DI registration tests for adapters and DownloadService dependencies.
  - Centralize adapter factory usage (replace Func usage).
- Phase 2 — refactor
  - Split DownloadService into orchestrator + client gateway + finalizer + repository.
  - Introduce repository / application service boundary; move EF logic.
  - Add unit tests for each new service.
- Phase 3 — polish
  - Add API contracts package, generate frontend types, tighten telemetry & health checks, expand integration tests.

## Notes on maintainability
- Prefer small, single-responsibility classes and explicit interfaces.
- Keep DI graph shallow and explicit (avoid service locator pattern).
- Add tests for behavioral contracts, not implementation details.
