# ADR-004: AI providers behind one interface, mock by default

- Status: Accepted
- Date: 2026-11-03

## Context
The PRD calls the Claude API from the backend, which costs money per token. The project owner wants to run and demo the AI
features without paying, and the demo must also work without any key. The AI features send school data, so as little of it as
possible may leave the machine.

## Decision
The Application layer knows one interface, `IAiService`. Three implementations sit in Infrastructure and one is chosen by
`Ai:Provider`:

- `Mock` (default): fixed, deterministic answers; used by tests and the public demo.
- `Ollama`: a local model in Docker (`infra/docker-compose.yml`), free, and no data leaves the machine.
- `Claude`: the Messages API through a typed `HttpClient` (tool use, vision, prompt caching); the cheapest model by default.

All three use `HttpClient`; no AI SDK or other package is added. Names of students are replaced by IDs before any call and
mapped back in the backend, whatever the provider. Daily limits per user, the monthly budget cap and the usage log
(`AiUsageLog`) apply to every provider; a free provider costs 0 but its calls are still counted.

## Reasons
- A free path for development and demos, and a paid one for quality, without changing a use case.
- Local Ollama keeps pupil data in the owner's hands, unlike a free hosted tier that may train on it.
- A thin client avoids a dependency whose licence and size we would have to review.

## Consequences
- The PRD said "Claude API" only; the PRD, the stack table and the open questions are updated.
- Small local models follow JSON schemas and tool calls less well. Every answer is schema-validated before use and a failed
  validation is reported as an error, never saved. The test set of each feature (at least 10 cases) runs against the mock in the
  test suite; running it against Ollama or Claude is a manual step whenever a prompt changes.
- The public demo (Cloudflare) cannot reach a local Ollama, so it runs on Mock unless a Claude key is configured.
- Vision for AI-03 needs a vision-capable model in Ollama; the mock returns fixed fields.
