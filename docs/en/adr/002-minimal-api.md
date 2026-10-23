# ADR-002: Minimal API instead of Controllers

- Status: Accepted
- Date: 2026-10-23

## Context
ASP.NET Core offers two endpoint styles: Controllers (MVC) and Minimal API. Many enterprise
codebases still use Controllers, while Microsoft promotes Minimal API for new projects.

## Decision
SchoMans uses Minimal API with one endpoint file per feature.

## Reasons
- Fits the per-feature layout and direct handler calls (ADR-001); a controller would only be a
  pass-through layer.
- Dependencies are injected per endpoint, not into a whole class.
- Endpoint filters suit validation without a mediator pipeline.
- Lighter, with built-in OpenAPI used to generate the orval TypeScript client.

## Consequences
- Structure must be kept by convention (see `CLAUDE.md` and PRD section 7).
- MVC features such as `ModelState` and content negotiation are unavailable; not needed for a
  JSON-only API.
- Controller-only libraries cannot be used.

## Weaknesses and mitigations
| Weakness | Mitigation |
| --- | --- |
| Easy to get messy | One file per feature + `MapGroup`, nothing in `Program.cs` |
| Lambdas hard to read and debug | Named static methods, no business logic in endpoints |
| Parameter binding can surprise | Explicit sources, `[AsParameters]` for queries |
| `IFormFile` hits antiforgery | `.DisableAntiforgery()` (JWT, not cookies) |
| Fewer MVC features | Libraries must support Minimal API |
| Long endpoint metadata | Group-level config, `.WithValidation<T>()` |
| Hard to unit test endpoints | Unit tests on handlers, integration tests per endpoint |
| Team familiarity | This ADR |

## Rejected alternatives
- Controllers: more boilerplate with no extra benefit for this project.
- FastEndpoints or Carter: an extra dependency and pattern on top of Minimal API.
