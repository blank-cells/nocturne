<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import {
    Fingerprint,
    Shield,
    HeartPulse,
    Smartphone,
    Syringe,
    Upload,
    Plug,
    Activity,
    CheckCircle2,
    ChevronRight,
    ArrowRightLeft,
  } from "lucide-svelte";
  import { Button } from "$lib/components/ui/button";
  import { goto } from "$app/navigation";
  import * as patientRemote from "$lib/api/generated/patientRecords.generated.remote";
  import * as servicesRemote from "$lib/api/generated/services.generated.remote";
  import * as profileRemote from "$lib/api/generated/profiles.generated.remote";
  import { markSetupComplete } from "./setup.remote";
  import { getPublicAccessConfig } from "./permissions/permissions.remote";

  // ── Data loading ──────────────────────────────────────────────────

  const patientRecord = patientRemote.getPatientRecord();
  const devices = patientRemote.getDevices();
  const insulins = patientRemote.getInsulins();
  const servicesOverview = servicesRemote.getServicesOverview();
  const activeDataSources = servicesRemote.getActiveDataSources();
  const profileSummary = profileRemote.getProfileSummary(undefined);
  const publicAccessConfig = getPublicAccessConfig();

  // Known uploader app source types for completion detection
  const uploaderSourceTypes = new Set([
    "xdrip", "xdrip4ios", "spike", "loop", "aaps", "openaps",
    "trio", "iaps", "juggluco", "glucotracker", "nightscout-uploader",
  ]);

  // ── Step definitions ──────────────────────────────────────────────

  type SetupStep = {
    title: string;
    description: string;
    icon: typeof HeartPulse;
    href: string;
    required: boolean;
  };

  const steps: SetupStep[] = [
    {
      title: "Passkey",
      description: "Set up passwordless authentication with a passkey",
      icon: Fingerprint,
      href: "/settings/setup/passkey",
      required: true,
    },
    {
      title: "Sharing & Privacy",
      description: "Choose who can see your data",
      icon: Shield,
      href: "/settings/setup/permissions",
      required: true,
    },
    {
      title: "Patient Record",
      description: "Set your diabetes type and clinical information",
      icon: HeartPulse,
      href: "/settings/setup/patient",
      required: true,
    },
    {
      title: "Devices",
      description: "Add the devices you currently use",
      icon: Smartphone,
      href: "/settings/setup/devices",
      required: true,
    },
    {
      title: "Insulins",
      description: "Add the insulins you currently use",
      icon: Syringe,
      href: "/settings/setup/insulins",
      required: true,
    },
    {
      title: "Uploaders",
      description: "Configure a phone app to push data to Nocturne",
      icon: Upload,
      href: "/settings/setup/uploaders",
      required: false,
    },
    {
      title: "Connectors",
      description: "Connect external data sources",
      icon: Plug,
      href: "/settings/setup/connectors",
      required: false,
    },
    {
      title: "Therapy Profile",
      description: "Configure your basal rates and therapy settings",
      icon: Activity,
      href: "/settings/setup/profile",
      required: true,
    },
  ];

  // ── Completion inference ──────────────────────────────────────────

  const completionStatus = $derived.by(() => {
    // Passkey: considered complete if the user is authenticated
    // (they must have a passkey or OIDC session to reach this page)
    const passkeyComplete = true;

    const permissionsComplete = !!publicAccessConfig.current?.configured;

    const patientComplete = !!patientRecord.current?.diabetesType;

    const devicesComplete =
      (devices.current ?? []).some((d) => d.isCurrent) ?? false;

    const insulinsComplete =
      (insulins.current ?? []).some((i) => i.isCurrent) ?? false;

    const uploadersComplete =
      (activeDataSources.current ?? []).some(
        (ds) => ds.sourceType && uploaderSourceTypes.has(ds.sourceType.toLowerCase()),
      ) ?? false;

    const connectorsComplete =
      (servicesOverview.current?.availableConnectors ?? []).some(
        (c) => c.isConfigured,
      ) ?? false;

    const profileComplete =
      (profileSummary.current?.basalSchedules ?? []).length > 0;

    return [
      passkeyComplete,
      permissionsComplete,
      patientComplete,
      devicesComplete,
      insulinsComplete,
      uploadersComplete,
      connectorsComplete,
      profileComplete,
    ];
  });

  const requiredComplete = $derived(
    completionStatus.filter((complete, i) => steps[i].required && complete)
      .length,
  );

  let isFinishing = $state(false);

  async function handleFinishSetup() {
    isFinishing = true;
    try {
      await markSetupComplete();
      await goto("/", { invalidateAll: true });
    } finally {
      isFinishing = false;
    }
  }
</script>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Setup</h1>
    <p class="text-muted-foreground">
      {requiredComplete} of 6 required steps complete
    </p>
  </div>

  <a href="/settings/setup/migrate" class="block">
    <Card.Root class="border-dashed border-blue-500/30 bg-blue-500/5 hover:bg-blue-500/10 transition-colors">
      <Card.Header class="flex flex-row items-center gap-4 space-y-0 p-4">
        <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600">
          <ArrowRightLeft class="h-5 w-5" />
        </div>
        <div class="flex-1 min-w-0">
          <Card.Title class="text-sm font-medium">Coming from Nightscout?</Card.Title>
          <Card.Description class="text-xs">Migrate your data and keep both systems in sync during transition</Card.Description>
        </div>
        <ChevronRight class="h-4 w-4 shrink-0 text-muted-foreground" />
      </Card.Header>
    </Card.Root>
  </a>

  <div class="space-y-3">
    {#each steps as step, i}
      {@const isComplete = completionStatus[i]}
      <a href={step.href} class="block">
        <Card.Root
          class="transition-colors hover:bg-muted/50 {isComplete
            ? 'border-green-500/30'
            : ''}"
        >
          <Card.Header class="flex flex-row items-center gap-4 space-y-0 p-4">
            <div
              class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg {isComplete
                ? 'bg-green-500/10 text-green-600'
                : 'bg-muted text-muted-foreground'}"
            >
              {#if isComplete}
                <CheckCircle2 class="h-5 w-5" />
              {:else}
                <step.icon class="h-5 w-5" />
              {/if}
            </div>

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <Card.Title class="text-sm font-medium">
                  {step.title}
                </Card.Title>
                {#if !step.required}
                  <Badge variant="secondary" class="text-xs">Optional</Badge>
                {/if}
              </div>
              <Card.Description class="text-xs">
                {step.description}
              </Card.Description>
            </div>

            <ChevronRight class="h-4 w-4 shrink-0 text-muted-foreground" />
          </Card.Header>
        </Card.Root>
      </a>
    {/each}
  </div>

  <div class="flex justify-end">
    <Button
      variant="outline"
      disabled={isFinishing}
      onclick={handleFinishSetup}
    >
      {isFinishing ? "Finishing..." : "Finish Setup"}
    </Button>
  </div>
</div>
