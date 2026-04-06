<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import {
    getUploaderApps,
    getUploaderSetup,
    getActiveDataSources,
  } from "$api/generated/services.generated.remote";
  import type {
    UploaderApp,
    UploaderSetupResponse,
    DataSourceInfo,
  } from "$lib/api/generated/nocturne-api-client";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import {
    AlertCircle,
    CheckCircle,
    ChevronLeft,
    ChevronRight,
    Copy,
    Check,
    ExternalLink,
    Loader2,
    Upload,
    Activity,
    Smartphone,
    Clock,
  } from "lucide-svelte";
  import Apple from "lucide-svelte/icons/apple";

  // ── View state ──────────────────────────────────────────────────────

  type ViewState = "selection" | "instructions";
  let viewState = $state<ViewState>("selection");
  let selectedApp = $state<UploaderApp | null>(null);
  let setupResponse = $state<UploaderSetupResponse | null>(null);

  // ── Data state ────────────────────────────────────────────────────

  let uploaderApps = $state<UploaderApp[]>([]);
  let dataSources = $state<DataSourceInfo[]>([]);
  let isLoading = $state(true);
  let loadError = $state<string | null>(null);
  let setupLoading = $state(false);

  // ── Copy state ────────────────────────────────────────────────────

  let copiedField = $state<string | null>(null);

  // ── Connection polling ────────────────────────────────────────────

  let pollInterval = $state<ReturnType<typeof setInterval> | null>(null);

  // ── Platform filter ──────────────────────────────────────────────

  type PlatformFilter = "all" | "ios" | "android";
  let platformFilter = $state<PlatformFilter>("all");

  // ── Categories ────────────────────────────────────────────────────

  const categoryLabels: Record<string, string> = {
    cgm: "CGM Apps",
    "aid-system": "AID Systems",
    uploader: "General Uploaders",
  };

  const categoryOrder = ["cgm", "aid-system", "uploader"];

  const filteredApps = $derived(
    platformFilter === "all"
      ? uploaderApps
      : uploaderApps.filter((app) => app.platform === platformFilter),
  );

  const groupedApps = $derived.by(() => {
    const groups: Record<string, UploaderApp[]> = {};
    for (const app of filteredApps) {
      const cat = app.category ?? "uploader";
      if (!groups[cat]) groups[cat] = [];
      groups[cat].push(app);
    }
    return categoryOrder
      .filter((cat) => groups[cat]?.length)
      .map((cat) => ({ category: cat, label: categoryLabels[cat] ?? cat, apps: groups[cat] }));
  });

  // ── Detect which uploaders have sent data ─────────────────────────

  function isDetected(appId: string | undefined): boolean {
    if (!appId) return false;
    return dataSources.some(
      (ds) => ds.sourceType?.toLowerCase() === appId.toLowerCase(),
    );
  }

  function getDataSource(appId: string | undefined): DataSourceInfo | undefined {
    if (!appId) return undefined;
    return dataSources.find(
      (ds) => ds.sourceType?.toLowerCase() === appId.toLowerCase(),
    );
  }

  // ── Load data ─────────────────────────────────────────────────────

  onMount(async () => {
    await loadData();
  });

  onDestroy(() => {
    stopPolling();
  });

  async function loadData() {
    isLoading = true;
    loadError = null;

    try {
      const [apps, sources] = await Promise.all([
        getUploaderApps(),
        getActiveDataSources().catch(() => [] as DataSourceInfo[]),
      ]);

      uploaderApps = apps ?? [];
      dataSources = sources ?? [];
    } catch (e) {
      loadError = e instanceof Error ? e.message : "Failed to load uploader apps";
    } finally {
      isLoading = false;
    }
  }

  // ── Select an app ─────────────────────────────────────────────────

  async function selectApp(app: UploaderApp) {
    selectedApp = app;
    setupLoading = true;
    viewState = "instructions";

    try {
      const result = await getUploaderSetup(app.id!);
      setupResponse = result;
    } catch (e) {
      setupResponse = null;
    } finally {
      setupLoading = false;
    }

    // Start polling for connection if not already detected
    if (!isDetected(app.id)) {
      startPolling();
    }
  }

  function handleBackToSelection() {
    stopPolling();
    selectedApp = null;
    setupResponse = null;
    copiedField = null;
    viewState = "selection";
  }

  // ── Polling ───────────────────────────────────────────────────────

  function startPolling() {
    stopPolling();
    pollInterval = setInterval(async () => {
      try {
        const sources = await getActiveDataSources();
        dataSources = sources ?? [];

        // Stop polling if we detect data from the selected app
        if (selectedApp && isDetected(selectedApp.id)) {
          stopPolling();
        }
      } catch {
        // Silently continue polling
      }
    }, 15_000); // Poll every 15 seconds
  }

  function stopPolling() {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
  }

  // ── Copy to clipboard ─────────────────────────────────────────────

  async function copyToClipboard(value: string, fieldName: string) {
    try {
      await navigator.clipboard.writeText(value);
      copiedField = fieldName;
      setTimeout(() => {
        copiedField = null;
      }, 2000);
    } catch {
      // Clipboard API not available
    }
  }

  // ── Platform icon helper ──────────────────────────────────────────

  function getPlatformLabel(platform: string | undefined): string {
    switch (platform) {
      case "ios":
        return "iOS";
      case "android":
        return "Android";
      case "desktop":
        return "Desktop";
      case "web":
        return "Web";
      default:
        return platform ?? "Unknown";
    }
  }

  function getCategoryIcon(category: string | undefined) {
    switch (category) {
      case "cgm":
        return Activity;
      case "aid-system":
        return Activity;
      case "uploader":
        return Upload;
      default:
        return Upload;
    }
  }
</script>

<svelte:head>
  <title>Uploaders - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Configure Uploader App"
  description="Set up a phone app to push glucose and treatment data to Nocturne. You can always add more uploaders later."
  currentStep={6}
  totalSteps={8}
  prevHref="/settings/setup/insulins"
  nextHref="/settings/setup/connectors"
  showSkip={true}
  saveDisabled={false}
  onSave={async () => true}
>
  {#if viewState === "selection"}
    <!-- ── App Selection ──────────────────────────────────────── -->
    {#if isLoading}
      <div class="flex items-center justify-center py-12">
        <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    {:else if loadError}
      <Card class="border-destructive">
        <CardContent class="flex items-center gap-3 pt-6">
          <AlertCircle class="h-5 w-5 text-destructive" />
          <div>
            <p class="font-medium">Failed to load uploader apps</p>
            <p class="text-sm text-muted-foreground">{loadError}</p>
          </div>
        </CardContent>
      </Card>
    {:else if uploaderApps.length === 0}
      <Card>
        <CardContent class="py-8 text-center">
          <Upload class="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
          <p class="font-medium">No uploader apps available</p>
          <p class="text-sm text-muted-foreground mt-1">
            There are no uploader apps available at this time.
          </p>
        </CardContent>
      </Card>
    {:else}
      <div class="flex items-center justify-end gap-1 mb-2">
        <Button
          variant={platformFilter === "all" ? "default" : "outline"}
          size="sm"
          onclick={() => (platformFilter = "all")}
        >
          All
        </Button>
        <Button
          variant={platformFilter === "ios" ? "default" : "outline"}
          size="sm"
          class="gap-1.5"
          onclick={() => (platformFilter = "ios")}
        >
          <Apple class="h-3.5 w-3.5" />
          iOS
        </Button>
        <Button
          variant={platformFilter === "android" ? "default" : "outline"}
          size="sm"
          class="gap-1.5"
          onclick={() => (platformFilter = "android")}
        >
          <Smartphone class="h-3.5 w-3.5" />
          Android
        </Button>
      </div>

      <div class="space-y-6">
        {#each groupedApps as group (group.category)}
          <div class="space-y-3">
            <h3 class="text-sm font-medium text-muted-foreground">{group.label}</h3>
            <div class="grid gap-3 sm:grid-cols-2">
              {#each group.apps as app (app.id)}
                {@const detected = isDetected(app.id)}
                {@const Icon = getCategoryIcon(app.category)}
                <button
                  type="button"
                  class="flex items-center gap-4 p-4 rounded-lg border transition-colors text-left group {detected
                    ? 'border-green-500/30 bg-green-500/5 hover:bg-green-500/10'
                    : 'bg-muted/30 hover:border-primary/50 hover:bg-accent/50'}"
                  onclick={() => selectApp(app)}
                >
                  <div
                    class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg {detected
                      ? 'bg-green-500/10 text-green-600'
                      : 'bg-primary/10 text-primary'}"
                  >
                    {#if detected}
                      <CheckCircle class="h-5 w-5" />
                    {:else}
                      <Icon class="h-5 w-5" />
                    {/if}
                  </div>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 flex-wrap">
                      <span class="font-medium">{app.name}</span>
                      <Badge variant="outline" class="text-xs gap-1">
                        {getPlatformLabel(app.platform)}
                      </Badge>
                      {#if detected}
                        <Badge variant="secondary" class="text-xs text-green-600">
                          Connected
                        </Badge>
                      {/if}
                    </div>
                    {#if app.description}
                      <p class="text-sm text-muted-foreground line-clamp-1">
                        {app.description}
                      </p>
                    {/if}
                  </div>
                  <ChevronRight
                    class="h-4 w-4 text-muted-foreground group-hover:text-foreground transition-colors shrink-0"
                  />
                </button>
              {/each}
            </div>
          </div>
        {/each}
      </div>
    {/if}

  {:else if viewState === "instructions"}
    <!-- ── Setup Instructions ─────────────────────────────────── -->
    <div class="space-y-4">
      <Button
        variant="ghost"
        size="sm"
        class="gap-1 -ml-2"
        onclick={handleBackToSelection}
      >
        <ChevronLeft class="h-4 w-4" />
        Back to uploaders
      </Button>

      {#if setupLoading}
        <div class="flex items-center justify-center py-8">
          <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      {:else if setupResponse}
        <div>
          <h2 class="text-lg font-semibold">{selectedApp?.name}</h2>
          {#if selectedApp?.description}
            <p class="text-sm text-muted-foreground">
              {selectedApp.description}
            </p>
          {/if}
        </div>

        <!-- API URLs to copy -->
        <Card>
          <CardHeader class="pb-3">
            <CardTitle class="text-sm">Connection Details</CardTitle>
            <CardDescription>
              Copy these values into your {selectedApp?.name} app settings.
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-3">
            <!-- Full API URL -->
            <div class="space-y-1">
              <span class="text-xs font-medium text-muted-foreground">API URL</span>
              <div class="flex items-center gap-2">
                <code
                  class="flex-1 rounded-md border bg-muted px-3 py-2 text-sm font-mono break-all"
                >
                  {setupResponse.fullApiUrl}
                </code>
                <Button
                  variant="outline"
                  size="icon"
                  class="shrink-0"
                  onclick={() => copyToClipboard(setupResponse!.fullApiUrl!, "apiUrl")}
                >
                  {#if copiedField === "apiUrl"}
                    <Check class="h-4 w-4 text-green-500" />
                  {:else}
                    <Copy class="h-4 w-4" />
                  {/if}
                </Button>
              </div>
            </div>

            <!-- xDrip-style URL (with embedded secret) -->
            {#if setupResponse.xdripStyleUrl}
              <div class="space-y-1">
                <span class="text-xs font-medium text-muted-foreground">
                  xDrip-style URL (with embedded secret)
                </span>
                <div class="flex items-center gap-2">
                  <code
                    class="flex-1 rounded-md border bg-muted px-3 py-2 text-sm font-mono break-all"
                  >
                    {setupResponse.xdripStyleUrl}
                  </code>
                  <Button
                    variant="outline"
                    size="icon"
                    class="shrink-0"
                    onclick={() => copyToClipboard(setupResponse!.xdripStyleUrl!, "xdripUrl")}
                  >
                    {#if copiedField === "xdripUrl"}
                      <Check class="h-4 w-4 text-green-500" />
                    {:else}
                      <Copy class="h-4 w-4" />
                    {/if}
                  </Button>
                </div>
              </div>
            {/if}

            <!-- API Secret -->
            {#if setupResponse.apiSecretPlaceholder}
              <div class="space-y-1">
                <span class="text-xs font-medium text-muted-foreground">API Secret</span>
                <div class="flex items-center gap-2">
                  <code
                    class="flex-1 rounded-md border bg-muted px-3 py-2 text-sm font-mono break-all"
                  >
                    {setupResponse.apiSecretPlaceholder}
                  </code>
                  <Button
                    variant="outline"
                    size="icon"
                    class="shrink-0"
                    onclick={() =>
                      copyToClipboard(setupResponse!.apiSecretPlaceholder!, "apiSecret")}
                  >
                    {#if copiedField === "apiSecret"}
                      <Check class="h-4 w-4 text-green-500" />
                    {:else}
                      <Copy class="h-4 w-4" />
                    {/if}
                  </Button>
                </div>
              </div>
            {/if}
          </CardContent>
        </Card>

        <!-- App download link -->
        {#if selectedApp?.url}
          <a
            href={selectedApp.url}
            target="_blank"
            rel="noopener noreferrer"
            class="inline-flex items-center gap-2 text-sm text-primary hover:underline"
          >
            <ExternalLink class="h-4 w-4" />
            Download {selectedApp.name}
          </a>
        {/if}

        <!-- Connection status -->
        {@const detected = isDetected(selectedApp?.id)}
        {@const ds = getDataSource(selectedApp?.id)}
        {#if detected && ds}
          <Card class="border-green-500/30">
            <CardContent class="flex items-center gap-3 pt-6">
              <CheckCircle class="h-5 w-5 text-green-500" />
              <div>
                <p class="font-medium text-green-600">Connected</p>
                <p class="text-sm text-muted-foreground">
                  {selectedApp?.name} is sending data. {ds.entriesLast24h ?? 0} entries in the last 24 hours.
                </p>
              </div>
            </CardContent>
          </Card>
        {:else}
          <Card class="border-muted">
            <CardContent class="flex items-center gap-3 pt-6">
              <Clock class="h-5 w-5 text-muted-foreground" />
              <div>
                <p class="font-medium">Waiting for data</p>
                <p class="text-sm text-muted-foreground">
                  Once you've configured {selectedApp?.name}, it can take up to five minutes for the first glucose data to arrive. This page will update automatically.
                </p>
              </div>
            </CardContent>
          </Card>
        {/if}
      {:else}
        <Card class="border-destructive">
          <CardContent class="flex items-center gap-3 pt-6">
            <AlertCircle class="h-5 w-5 text-destructive" />
            <div>
              <p class="font-medium">Failed to load setup instructions</p>
              <p class="text-sm text-muted-foreground">
                Could not load setup details for {selectedApp?.name}. Please try again.
              </p>
            </div>
          </CardContent>
        </Card>
      {/if}
    </div>
  {/if}
</WizardShell>
