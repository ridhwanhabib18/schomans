# ADR-001: No mediator pattern

- Status: Accepted
- Date: 2026-10-23

## Context
.NET Clean Architecture templates usually use CQRS + MediatR. Since 2025 MediatR is commercially
licensed, and many teams question whether the mediator layer is needed.

## Decision
SchoMans uses neither the mediator pattern nor the MediatR or Mediator libraries. Endpoints call
handlers directly through dependency injection.

## Reasons
- Every command has exactly one handler, so the mediator layer adds no real value.
- Cross-cutting needs are covered by built-in ASP.NET Core and EF Core mechanisms.
- Code flow is explicit: *go to definition* leads straight from endpoint to handler.
- No commercial licensing risk.

## Pipeline replacements
| Need | Mechanism |
| --- | --- |
| Validation | FluentValidation endpoint filter (`WithValidation<T>()`) |
| Audit log | EF Core `SaveChangesInterceptor` |
| Logging & errors | Serilog middleware + ProblemDetails |

## Consequences
Endpoints depend on concrete handler types, which is acceptable for a single-team, single-deployable
project. Command/Handler naming is kept to organize use cases.

## Rejected alternatives
- MediatR: commercial license.
- Mediator (MIT): adds a layer without matching benefit; reconsider if the project needs many
  handlers per event or a complex pipeline.
