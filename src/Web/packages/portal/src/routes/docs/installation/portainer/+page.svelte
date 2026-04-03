<script lang="ts">
    import DockerComposeYaml from "$lib/components/docs/DockerComposeYaml.svelte";
    import EnvVarReference from "$lib/components/docs/EnvVarReference.svelte";
    import VerificationSteps from "$lib/components/docs/VerificationSteps.svelte";
    import NextSteps from "$lib/components/docs/NextSteps.svelte";
    import { Info } from "@lucide/svelte";
</script>

<div class="max-w-3xl">
    <h1 class="text-4xl font-bold tracking-tight mb-4">Portainer</h1>
    <p class="text-lg text-muted-foreground mb-8">
        Deploy Nocturne using the Portainer web interface. No SSH or command-line access required.
    </p>

    <h2 class="text-2xl font-bold mt-8 mb-4">Prerequisites</h2>
    <ul class="list-disc list-inside space-y-2 text-muted-foreground mb-8">
        <li>A running Portainer instance (Community or Business Edition)</li>
        <li>Access to a Docker environment in Portainer</li>
        <li>A domain name (recommended) or static IP address</li>
    </ul>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 1: Create a new stack</h2>
    <ol class="list-decimal list-inside space-y-3 text-muted-foreground mb-8">
        <li>
            <strong class="text-foreground">Navigate to Stacks</strong>
            <p class="ml-6 mt-1">
                In the Portainer sidebar, click <strong class="text-foreground">Stacks</strong>,
                then click <strong class="text-foreground">Add stack</strong>.
            </p>
        </li>
        <li>
            <strong class="text-foreground">Name your stack</strong>
            <p class="ml-6 mt-1">
                Enter <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded">nocturne</code>
                as the stack name.
            </p>
        </li>
        <li>
            <strong class="text-foreground">Select "Web editor"</strong>
            <p class="ml-6 mt-1">
                Choose the Web editor option to paste the compose file directly.
            </p>
        </li>
    </ol>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 2: Paste the compose configuration</h2>
    <p class="text-muted-foreground mb-4">
        Copy the following Docker Compose configuration and paste it into the Portainer web editor.
        The minimal configuration includes PostgreSQL, the Nocturne API, and Watchtower for
        automatic updates.
    </p>

    <div class="p-4 rounded-lg border border-blue-500/30 bg-blue-500/5 mb-4 not-prose">
        <div class="flex items-start gap-3">
            <Info class="w-5 h-5 text-blue-500 mt-0.5 shrink-0" />
            <p class="text-sm text-muted-foreground">
                <strong class="text-blue-700 dark:text-blue-400">Only paste the docker-compose.yml content.</strong>
                The .env variables will be configured separately in Portainer's environment
                variables section below.
            </p>
        </div>
    </div>

    <DockerComposeYaml minimal />

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 3: Configure environment variables</h2>
    <p class="text-muted-foreground mb-4">
        Scroll down to the <strong class="text-foreground">Environment variables</strong> section
        in Portainer. Click <strong class="text-foreground">Advanced mode</strong> and add the
        following variables, one per line in <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded">KEY=VALUE</code> format:
    </p>

    <pre class="p-4 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-4"><code>POSTGRES_USERNAME=nocturne
POSTGRES_PASSWORD=change-me-to-a-secure-password
INSTANCE_KEY=change-me-min-12-characters
NOCTURNE_API_PORT=8443
NOCTURNE_API_IMAGE=ghcr.io/nightscout/nocturne-api:latest</code></pre>

    <p class="text-muted-foreground mb-4">
        Make sure to replace the placeholder values with your own secure passwords and secrets.
    </p>

    <EnvVarReference coreOnly />

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 4: Deploy the stack</h2>
    <ol class="list-decimal list-inside space-y-3 text-muted-foreground mb-8">
        <li>
            <strong class="text-foreground">Review your configuration</strong>
            <p class="ml-6 mt-1">
                Double-check the compose content and environment variables are correct.
            </p>
        </li>
        <li>
            <strong class="text-foreground">Click "Deploy the stack"</strong>
            <p class="ml-6 mt-1">
                Portainer will pull the Docker images and start all services. This may take
                a few minutes on first deployment.
            </p>
        </li>
        <li>
            <strong class="text-foreground">Monitor the deployment</strong>
            <p class="ml-6 mt-1">
                The stack details page will show the status of each container. Wait until all
                containers show as <strong class="text-foreground">Running</strong>.
            </p>
        </li>
    </ol>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 5: Verify the installation</h2>
    <p class="text-muted-foreground mb-4">
        You can check the container logs directly in Portainer by clicking on any container
        in your stack, or from the command line:
    </p>
    <VerificationSteps />

    <h2 class="text-2xl font-bold mt-8 mb-4">Updating</h2>
    <p class="text-muted-foreground mb-4">
        Watchtower is included in the stack and will automatically check for image updates
        once per day. To update manually through Portainer:
    </p>
    <ol class="list-decimal list-inside space-y-2 text-muted-foreground mb-8">
        <li>Navigate to your <strong class="text-foreground">nocturne</strong> stack</li>
        <li>Click <strong class="text-foreground">Editor</strong></li>
        <li>Click <strong class="text-foreground">Update the stack</strong></li>
        <li>Check <strong class="text-foreground">Re-pull image and redeploy</strong></li>
        <li>Click <strong class="text-foreground">Update</strong></li>
    </ol>

    <h2 class="text-2xl font-bold mt-8 mb-4">Adding Connectors</h2>
    <p class="text-muted-foreground mb-4">
        To add data source connectors (Dexcom, LibreLinkUp, etc.), edit your stack in Portainer
        and add the connector services to the compose configuration. You will also need to add the
        connector-specific environment variables. See the
        <a href="/docs/configuration" class="text-primary hover:underline">Configuration Guide</a>
        for details.
    </p>

    <h2 class="text-2xl font-bold mt-8 mb-4">Next Steps</h2>
    <NextSteps />
</div>
