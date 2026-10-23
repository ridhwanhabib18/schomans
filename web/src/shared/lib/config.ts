/** Runtime configuration from Vite env variables (see .env.development and .env.example). */
export const config = {
  apiUrl: import.meta.env.VITE_API_URL ?? 'http://localhost:5080',
  keycloak: {
    url: import.meta.env.VITE_KEYCLOAK_URL ?? 'http://localhost:8080',
    realm: import.meta.env.VITE_KEYCLOAK_REALM ?? 'schomans',
    clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID ?? 'schomans-web',
    /** Client used by the one-click demo buttons (a password grant). Exists only in demo and development realms. */
    demoClientId: import.meta.env.VITE_KEYCLOAK_DEMO_CLIENT_ID ?? 'schomans-web-demo',
  },
  /** Shows the demo login buttons. Never on for a real deployment. */
  demoMode: import.meta.env.VITE_DEMO_MODE === 'true',
  demoPassword: import.meta.env.VITE_DEMO_PASSWORD ?? 'Demo123!',
} as const

export const keycloakIssuer = `${config.keycloak.url}/realms/${config.keycloak.realm}`
