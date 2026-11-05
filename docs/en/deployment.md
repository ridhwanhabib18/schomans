# Deployment guide

How to put SchoMans on one VPS with HTTPS. Everything is in `infra/`: `docker-compose.prod.yml`, `Caddyfile`, `.env.production.example`, `make-production-realm.sh`, `sqlserver/init-keycloak.sql`, and the Dockerfiles of the api (`src/SchoMans.Api/Dockerfile`) and the web app (`web/Dockerfile`). The design behind it is section 3 of the [system design](system-design.md).

## What you need
- A VPS with about **4 GB of RAM** (SQL Server Express takes up to 1.5 GB, Keycloak about 1 GB, the api up to 0.5 GB), Docker Engine with the compose plugin, and ports **80 and 443** open to the internet.
- **Two DNS records** (A or AAAA) pointing at the server: one for the app (for example `school.example.com`) and one for Keycloak (`auth.school.example.com`). Caddy gets the HTTPS certificates when it first sees each name, so the records must exist before the first start.
- A **Cloudflare R2** account for the files: the buckets `schomans-private` and `schomans-public`, an API token with read and write on both, and a CORS rule on both buckets that allows `GET` and `PUT` from `https://school.example.com` (the browser uploads straight to R2 with presigned URLs). Use the S3 endpoint `https://<account-id>.r2.cloudflarestorage.com` and the public address of `schomans-public` for `R2_PUBLIC_BASE_URL`.
- Optional: an SMTP account (without one the emails wait in the queue), and a Claude API key (without one the AI features use the mock provider, which costs nothing, see ADR-004).

## Steps
```bash
git clone https://github.com/ridhwanhabib18/schomans.git && cd schomans
cp infra/.env.production.example infra/.env.production
nano infra/.env.production            # fill in every value; choose long random passwords
sh infra/make-production-realm.sh     # writes infra/keycloak/realm-schomans.production.json
docker compose -f infra/docker-compose.prod.yml --env-file infra/.env.production up -d --build
```
The first start builds two images and takes several minutes; Keycloak needs one to two minutes more to import the realm. Then check:
- `https://school.example.com` shows the landing page, and `https://school.example.com/api/v1/news/public` answers `200`.
- `https://auth.school.example.com/realms/schomans/.well-known/openid-configuration` lists the issuer `https://auth.school.example.com/realms/schomans`.
- With `DEMO_MODE=true` the login page has the one-click demo buttons; the api migrates the database and seeds the fictional school on its first start (`SEED_DEMO_DATA=true`).

To update: `git pull`, run `make-production-realm.sh` again if the realm changed, and run the same `up -d --build`. The realm is only imported when Keycloak starts with an empty database, so a change to `realm-schomans.json` needs the Keycloak admin console or a new database to take effect.

## What is, and is not, reachable from the internet
- Public: the app, `/api/*`, `/hubs/*`, `/sitemap.xml`, and Keycloak on its own address.
- **Not routed**: the jobs dashboard (`/hangfire`), the API reference (`/scalar` and the OpenAPI document are on only in the Development and Demo environments, and the proxy does not send them to the api), the admin console of Keycloak, SQL Server, and the health endpoints. Reach the admin console with an SSH tunnel to the Keycloak container instead of opening it.
- The api believes `X-Forwarded-For` and `X-Forwarded-Proto` only from the Docker network (`ForwardedHeaders` settings), so the rate limits per client address (6 chatbot questions a minute per guest) see each visitor and a forged header buys nothing.
- **The demo accounts share a public password** (`DEMO_PASSWORD`, which is also in the web bundle by design). Use them only with the fictional data. A deployment with real people must set `DEMO_MODE=false` and `SEED_DEMO_DATA=false`, remove the `schomans-web-demo` client and the demo users from the realm, and use the Keycloak sign-in instead.

## Pictures of the running demo
After the deploy, from `web/`: `CAPTURE_URL=https://school.example.com npm run capture:screenshots` visits every page of every role at desktop and phone size (into `docs/assets/screenshots/`), and `npm run capture:flows` records the main flows into GIFs (`docs/assets/flows/`, needs Python and Pillow). Run them again after a UI change.

## What was tested, and what was not
The files were tested on a development machine with the domains `localhost` and `auth.localhost`: both images build, the stack starts healthy, Caddy serves the web app (deep links, long cache for hashed files, no cache for the service worker) and routes the api, a demo sign-in through the real Keycloak works end to end, every page of the teacher role loads, the jobs dashboard and the Keycloak admin console are not reachable, and a forged `X-Forwarded-For` does not give a guest a new allowance. The testing found two defects that are fixed: the api refused the plain-HTTP metadata address on the internal network (`Keycloak__RequireHttpsMetadata`), and Keycloak's realm import failed on SQL Server because a client description was longer than 255 characters.

**Not tested** (they need things that do not exist on a development machine): real DNS and Let's Encrypt certificates, Cloudflare R2 (uploads from the browser and the CORS rule), a real SMTP server, a Claude key, and a 4 GB server under load. The R2 and e-mail settings follow the application's own configuration (`Storage:*`, `Email:*`), which the integration tests cover against SeaweedFS and a fake sender.

**Not built**: automatic backups. The database lives in the volume `sqlserver-data`; copy it with a `BACKUP DATABASE` run through `sqlcmd` in the `sqlserver` container before you rely on the demo for anything you cannot recreate (the seeded demo data can always be recreated).
