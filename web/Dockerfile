# The web image: the built app served by Caddy. Build from the web folder; the VITE_* values are baked into the build:
#   docker build -t schomans-web --build-arg VITE_API_URL=https://school.example.com web
FROM node:24-alpine AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci
COPY . .

ARG VITE_API_URL
ARG VITE_KEYCLOAK_URL
ARG VITE_KEYCLOAK_REALM=schomans
ARG VITE_KEYCLOAK_CLIENT_ID=schomans-web
ARG VITE_KEYCLOAK_DEMO_CLIENT_ID=schomans-web-demo
ARG VITE_DEMO_MODE=false
# The password of the public demo accounts. It ends up in the bundle on purpose (the demo buttons use it), so it is no secret.
ARG VITE_DEMO_PASSWORD=
ENV VITE_API_URL=$VITE_API_URL \
    VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL \
    VITE_KEYCLOAK_REALM=$VITE_KEYCLOAK_REALM \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID \
    VITE_KEYCLOAK_DEMO_CLIENT_ID=$VITE_KEYCLOAK_DEMO_CLIENT_ID \
    VITE_DEMO_MODE=$VITE_DEMO_MODE \
    VITE_DEMO_PASSWORD=$VITE_DEMO_PASSWORD
RUN npm run build

FROM caddy:2-alpine AS runtime
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /web/dist /srv
EXPOSE 80
