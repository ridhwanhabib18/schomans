#!/bin/sh
# Makes infra/keycloak/realm-schomans.production.json from the development realm: the address of the app and the secret of the admin
# client are the only things that differ. Run it again after changing SITE_DOMAIN or KEYCLOAK_ADMIN_CLIENT_SECRET in infra/.env.production.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
env_file="${1:-$here/.env.production}"

if [ ! -f "$env_file" ]; then
  echo "Missing $env_file. Copy infra/.env.production.example to it and fill it in." >&2
  exit 1
fi

# Reads KEY=value lines; quotes around a value are removed.
value() {
  grep -E "^$1=" "$env_file" | tail -n 1 | cut -d= -f2- | sed -e "s/^['\"]//" -e "s/['\"]$//"
}

site_domain=$(value SITE_DOMAIN)
secret=$(value KEYCLOAK_ADMIN_CLIENT_SECRET)
if [ -z "$site_domain" ] || [ -z "$secret" ]; then
  echo "SITE_DOMAIN and KEYCLOAK_ADMIN_CLIENT_SECRET must be set in $env_file." >&2
  exit 1
fi
if [ "$secret" = "dev-only-change-me" ]; then
  echo "KEYCLOAK_ADMIN_CLIENT_SECRET is still the development value; choose your own." >&2
  exit 1
fi

sed \
  -e "s#http://localhost:5173#https://$site_domain#g" \
  -e "s#dev-only-change-me#$secret#g" \
  "$here/keycloak/realm-schomans.json" > "$here/keycloak/realm-schomans.production.json"

echo "Wrote infra/keycloak/realm-schomans.production.json for https://$site_domain"
