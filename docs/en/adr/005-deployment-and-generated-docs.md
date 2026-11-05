# ADR-005: One VPS behind Caddy, and documents generated from the code

- Status: Accepted
- Date: 2026-11-05

## Context
The PRD asks for Docker Compose on one VPS with Caddy and automatic HTTPS, files in Cloudflare R2, and documents that "never fall
behind the code". Building and testing the stack on a development machine showed what the plain picture leaves out: the rate
limits per client address see only the proxy, Keycloak needs a database that lives beyond its container, and a realm that imports
into the development H2 database did not import into SQL Server.

## Decision
- **Caddy** ends TLS for two addresses: the app (`/` to the web container, `/api`, `/hubs` and `/sitemap.xml` to the api) and
  Keycloak on its own address, so its cookies stay apart from the app. The web container is also Caddy, serving the build with a
  single-page fallback. The jobs dashboard, the health endpoints and the Keycloak admin console are not routed to the internet.
- **Keycloak keeps its data in a second database of the same SQL Server Express** (created once by a `keycloak-db-init` service),
  not in a container-local file. The api asks Keycloak for its keys over the internal network (`MetadataAddress`), while tokens are
  issued for the public address.
- **The api trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from configured networks** (`ForwardedHeaders` settings; the Docker
  network in the deployment), so the rate limits per address see each visitor and a forged header buys nothing.
- **The production compose file has its own project name**, so running it never touches the volumes of the development stack.
- **The documents derived from the code are written by `tools/SchoMans.DocGen`**, which builds the api the way `Program.cs` does
  (with a shared `EndpointRegistration`) and needs no database. It writes the ERD (from the EF Core model), the API reference and
  the authorization matrix (from the registered endpoints and policies), the jobs (by running the real registration against a
  stand-in job manager) and the SignalR events, and fills marked regions of the system design. `--check` fails when a file is out of
  date or when a hand-written document names an API route that no longer exists; `docs.yml` runs it on pull requests (drift only)
  and after a merge (regenerate, then open a pull request).
- **Screenshots and flows are captured by Playwright** against any address (`CAPTURE_URL`), into `docs/assets/`; the flow GIFs are
  assembled from step pictures with a small Python script, because ffmpeg is not a dependency of the project.

## Reasons
- A solo developer can run, update and explain one compose file; Caddy removes certificate work.
- Generating documents from the code is the only way a claim like "the ERD is current" can be checked in CI.
- Each decision above was forced by a defect found while testing the stack, not by preference.

## Consequences
- The deployment was **tested on a development machine** with `localhost` names; real DNS and certificates, Cloudflare R2, SMTP and a
  4 GB server under load are not tested (the [deployment guide](../deployment.md) says so).
- A change to the realm needs the Keycloak console or a new database after the first start, because `--import-realm` skips a realm
  that exists.
- Backups are not automated.
- The generated reference lists the endpoints and their types, not field-level schemas; the OpenAPI document of the running api has those.
