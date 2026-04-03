<script lang="ts">
    interface Props {
        /** Show only the minimal core stack (postgres + api + watchtower) */
        minimal?: boolean;
    }

    let { minimal = false }: Props = $props();

    const minimalCompose = `services:
  nocturne-postgres-server:
    image: "docker.io/library/postgres:17.6"
    environment:
      POSTGRES_HOST_AUTH_METHOD: "scram-sha-256"
      POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256 --auth-local=scram-sha-256"
      POSTGRES_USER: "\${POSTGRES_USERNAME}"
      POSTGRES_PASSWORD: "\${POSTGRES_PASSWORD}"
    expose:
      - "5432"
    volumes:
      - type: "volume"
        target: "/var/lib/postgresql/data"
        source: "nocturne-postgres-data"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  nocturne-api:
    image: "\${NOCTURNE_API_IMAGE}"
    environment:
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      HTTPS_PORTS: "\${NOCTURNE_API_PORT}"
      ConnectionStrings__nocturne-postgres: "Host=nocturne-postgres-server;Port=5432;Username=\${POSTGRES_USERNAME};Password=\${POSTGRES_PASSWORD};Database=nocturne"
      INSTANCE_KEY: "\${INSTANCE_KEY}"
    ports:
      - "\${NOCTURNE_API_PORT}:\${NOCTURNE_API_PORT}"
    depends_on:
      nocturne-postgres-server:
        condition: "service_started"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  watchtower:
    image: "ghcr.io/nicholas-fedor/watchtower:latest"
    environment:
      WATCHTOWER_CLEANUP: "true"
      WATCHTOWER_POLL_INTERVAL: "86400"
    volumes:
      - "/var/run/docker.sock:/var/run/docker.sock:ro"
    networks:
      - "nocturne"
    restart: "unless-stopped"

networks:
  nocturne:
    driver: "bridge"

volumes:
  nocturne-postgres-data:
    driver: "local"`;

    const fullCompose = `services:
  nocturne-postgres-server:
    image: "docker.io/library/postgres:17.6"
    environment:
      POSTGRES_HOST_AUTH_METHOD: "scram-sha-256"
      POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256 --auth-local=scram-sha-256"
      POSTGRES_USER: "\${POSTGRES_USERNAME}"
      POSTGRES_PASSWORD: "\${POSTGRES_PASSWORD}"
    expose:
      - "5432"
    volumes:
      - type: "volume"
        target: "/var/lib/postgresql/data"
        source: "nocturne-postgres-data"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  nocturne-api:
    image: "\${NOCTURNE_API_IMAGE}"
    environment:
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      HTTPS_PORTS: "\${NOCTURNE_API_PORT}"
      ConnectionStrings__nocturne-postgres: "Host=nocturne-postgres-server;Port=5432;Username=\${POSTGRES_USERNAME};Password=\${POSTGRES_PASSWORD};Database=nocturne"
      INSTANCE_KEY: "\${INSTANCE_KEY}"
      DemoService__Enabled: "false"
    ports:
      - "\${NOCTURNE_API_PORT}:\${NOCTURNE_API_PORT}"
    depends_on:
      nocturne-postgres-server:
        condition: "service_started"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  # Optional: Dexcom connector
  dexcom-connector:
    image: "\${DEXCOM_CONNECTOR_IMAGE}"
    environment:
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      HTTP_PORTS: "\${DEXCOM_CONNECTOR_PORT}"
      NocturneApiUrl: "https://nocturne-api:\${NOCTURNE_API_PORT}"
      INSTANCE_KEY: "\${INSTANCE_KEY}"
      CONNECT_DEXCOM_USERNAME: "\${DEXCOM_USERNAME}"
      CONNECT_DEXCOM_PASSWORD: "\${DEXCOM_PASSWORD}"
      CONNECT_DEXCOM_SERVER: "\${DEXCOM_SERVER}"
      Parameters__Connectors__Dexcom__Enabled: "true"
    expose:
      - "\${DEXCOM_CONNECTOR_PORT}"
    depends_on:
      nocturne-api:
        condition: "service_started"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  # Optional: LibreLinkUp connector
  freestyle-connector:
    image: "\${FREESTYLE_CONNECTOR_IMAGE}"
    environment:
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      HTTP_PORTS: "\${FREESTYLE_CONNECTOR_PORT}"
      NocturneApiUrl: "https://nocturne-api:\${NOCTURNE_API_PORT}"
      INSTANCE_KEY: "\${INSTANCE_KEY}"
      CONNECT_LIBRE_USERNAME: "\${LIBRELINKUP_USERNAME}"
      CONNECT_LIBRE_PASSWORD: "\${LIBRELINKUP_PASSWORD}"
      CONNECT_LIBRE_REGION: "\${LIBRELINKUP_REGION}"
      Parameters__Connectors__LibreLinkUp__Enabled: "true"
    expose:
      - "\${FREESTYLE_CONNECTOR_PORT}"
    depends_on:
      nocturne-api:
        condition: "service_started"
    networks:
      - "nocturne"
    restart: "unless-stopped"

  watchtower:
    image: "ghcr.io/nicholas-fedor/watchtower:latest"
    environment:
      WATCHTOWER_CLEANUP: "true"
      WATCHTOWER_POLL_INTERVAL: "86400"
    volumes:
      - "/var/run/docker.sock:/var/run/docker.sock:ro"
    networks:
      - "nocturne"
    restart: "unless-stopped"

networks:
  nocturne:
    driver: "bridge"

volumes:
  nocturne-postgres-data:
    driver: "local"`;

    const envTemplate = `# Core settings
POSTGRES_USERNAME=nocturne
POSTGRES_PASSWORD=change-me-to-a-secure-password
INSTANCE_KEY=change-me-min-12-characters
NOCTURNE_API_PORT=8443
NOCTURNE_API_IMAGE=ghcr.io/nightscout/nocturne-api:latest

# Dexcom connector (optional)
DEXCOM_CONNECTOR_IMAGE=ghcr.io/nightscout/nocturne-dexcom:latest
DEXCOM_CONNECTOR_PORT=8081
DEXCOM_USERNAME=
DEXCOM_PASSWORD=
DEXCOM_SERVER=us

# LibreLinkUp connector (optional)
FREESTYLE_CONNECTOR_IMAGE=ghcr.io/nightscout/nocturne-freestyle:latest
FREESTYLE_CONNECTOR_PORT=8082
LIBRELINKUP_USERNAME=
LIBRELINKUP_PASSWORD=
LIBRELINKUP_REGION=eu`;

    const compose = minimal ? minimalCompose : fullCompose;
</script>

<div class="space-y-6 mb-8">
    <div>
        <p class="text-sm font-medium text-foreground mb-2">docker-compose.yml</p>
        <pre class="p-4 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto max-h-[500px]"><code>{compose}</code></pre>
    </div>

    <div>
        <p class="text-sm font-medium text-foreground mb-2">.env</p>
        <pre class="p-4 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto"><code>{envTemplate}</code></pre>
    </div>
</div>
