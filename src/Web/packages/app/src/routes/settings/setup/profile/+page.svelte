<script lang="ts">
  import { tick } from "svelte";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import ScheduleEditor from "$lib/components/setup/ScheduleEditor.svelte";
  import TargetRangeEditor from "$lib/components/setup/TargetRangeEditor.svelte";
  import * as Card from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import * as Select from "$lib/components/ui/select";
  import * as Popover from "$lib/components/ui/popover";
  import * as Command from "$lib/components/ui/command";
  import { Button } from "$lib/components/ui/button";
  import { CheckCircle, ChevronsUpDown, Check } from "lucide-svelte";
  import { cn } from "$lib/utils";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import {
    convertToDisplayUnits,
    convertFromDisplayUnits,
  } from "$lib/utils/formatting";
  import {
    getProfileSummary,
    createTherapySettings,
    updateTherapySettings,
    createBasalSchedule,
    updateBasalSchedule,
    createCarbRatioSchedule,
    updateCarbRatioSchedule,
    createSensitivitySchedule,
    updateSensitivitySchedule,
    createTargetRangeSchedule,
    updateTargetRangeSchedule,
  } from "$lib/api/generated/profiles.generated.remote";

  // ── Data loading ──────────────────────────────────────────────────

  const summaryQuery = getProfileSummary(undefined);

  // ── Form references ─────────────────────────────────────────────

  const createSettingsForm = createTherapySettings;
  const updateSettingsForm = updateTherapySettings;
  const createBasalForm = createBasalSchedule;
  const updateBasalForm = updateBasalSchedule;

  // Hidden form element refs for programmatic submission
  let settingsFormEl = $state<HTMLFormElement | null>(null);
  let basalFormEl = $state<HTMLFormElement | null>(null);

  // ── Form state ────────────────────────────────────────────────────

  let saveError = $state<string | null>(null);
  let saving = $state(false);

  // Basics
  // ── Timezone → units mapping ──────────────────────────────────
  // Countries/regions that use mmol/L. The rest default to mg/dL.
  const mmolTimezones = new Set([
    // UK & Ireland
    "Europe/London",
    "Europe/Dublin",
    // Canada
    "America/Toronto",
    "America/Vancouver",
    "America/Winnipeg",
    "America/Edmonton",
    "America/Halifax",
    "America/St_Johns",
    "America/Regina",
    "America/Whitehorse",
    "America/Yellowknife",
    "America/Iqaluit",
    "America/Moncton",
    "America/Thunder_Bay",
    "America/Nipigon",
    "America/Rainy_River",
    "America/Rankin_Inlet",
    "America/Resolute",
    "America/Swift_Current",
    "America/Atikokan",
    "America/Pangnirtung",
    "America/Dawson",
    "America/Dawson_Creek",
    "America/Fort_Nelson",
    "America/Creston",
    "America/Glace_Bay",
    "America/Goose_Bay",
    "America/Blanc-Sablon",
    "America/Cambridge_Bay",
    "America/Inuvik",
    // Australia
    "Australia/Sydney",
    "Australia/Melbourne",
    "Australia/Brisbane",
    "Australia/Perth",
    "Australia/Adelaide",
    "Australia/Hobart",
    "Australia/Darwin",
    "Australia/Lord_Howe",
    "Australia/Lindeman",
    "Australia/Currie",
    "Australia/Eucla",
    "Australia/Broken_Hill",
    // New Zealand
    "Pacific/Auckland",
    "Pacific/Chatham",
    // Western Europe (mmol/L)
    "Europe/Amsterdam",
    "Europe/Berlin",
    "Europe/Paris",
    "Europe/Brussels",
    "Europe/Luxembourg",
    "Europe/Zurich",
    "Europe/Vienna",
    "Europe/Rome",
    "Europe/Madrid",
    "Europe/Lisbon",
    "Europe/Stockholm",
    "Europe/Oslo",
    "Europe/Copenhagen",
    "Europe/Helsinki",
    "Europe/Athens",
    "Europe/Bucharest",
    "Europe/Budapest",
    "Europe/Prague",
    "Europe/Warsaw",
    "Europe/Vilnius",
    "Europe/Riga",
    "Europe/Tallinn",
    "Europe/Sofia",
    "Europe/Zagreb",
    "Europe/Ljubljana",
    "Europe/Bratislava",
    "Europe/Belgrade",
    "Europe/Sarajevo",
    "Europe/Skopje",
    "Europe/Podgorica",
    "Europe/Tirane",
    // Russia & neighbors
    "Europe/Moscow",
    "Europe/Samara",
    "Europe/Volgograd",
    "Asia/Yekaterinburg",
    "Asia/Novosibirsk",
    "Asia/Krasnoyarsk",
    "Asia/Irkutsk",
    "Asia/Yakutsk",
    "Asia/Vladivostok",
    "Asia/Magadan",
    "Asia/Kamchatka",
    "Europe/Kaliningrad",
    "Europe/Minsk",
    "Europe/Kiev",
    "Europe/Chisinau",
    // China, Hong Kong, Macau
    "Asia/Shanghai",
    "Asia/Hong_Kong",
    "Asia/Macau",
    // India, Sri Lanka, Bangladesh, Nepal, Pakistan
    "Asia/Kolkata",
    "Asia/Colombo",
    "Asia/Dhaka",
    "Asia/Kathmandu",
    "Asia/Karachi",
    // Southeast Asia
    "Asia/Singapore",
    "Asia/Kuala_Lumpur",
    "Asia/Bangkok",
    "Asia/Jakarta",
    "Asia/Ho_Chi_Minh",
    "Asia/Manila",
    // Middle East (mmol/L users)
    "Asia/Riyadh",
    "Asia/Dubai",
    "Asia/Qatar",
    "Asia/Bahrain",
    "Asia/Kuwait",
    "Asia/Muscat",
    // Africa (mmol/L users)
    "Africa/Johannesburg",
    "Africa/Lagos",
    "Africa/Nairobi",
    "Africa/Cairo",
    "Africa/Casablanca",
    "Africa/Algiers",
    "Africa/Tunis",
    // South Korea, Japan use mg/dL — intentionally excluded
    // Caribbean (mostly mg/dL) — intentionally excluded
    // Central & South America (mostly mg/dL) — intentionally excluded
  ]);

  function unitsForTimezone(tz: string): "mg/dL" | "mmol/L" {
    return mmolTimezones.has(tz) ? "mmol/L" : "mg/dL";
  }

  let profileName = $state("Default");
  const detectedTz = Intl.DateTimeFormat().resolvedOptions().timeZone;
  let units = $state(unitsForTimezone(detectedTz));
  let timezone = $state(detectedTz);
  const allTimezones = Intl.supportedValuesOf("timeZone");
  let tzOpen = $state(false);
  let tzSearch = $state("");
  const filteredTimezones = $derived(
    tzSearch
      ? allTimezones.filter((tz) =>
          tz.toLowerCase().includes(tzSearch.toLowerCase())
        )
      : allTimezones
  );
  let carbsHr = $state(20);

  // Schedule entries
  let basalEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 0.5 },
  ]);
  let carbRatioEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 10 },
  ]);
  let sensitivityEntries = $state<Array<{ time: string; value: number }>>([
    { time: "00:00", value: 50 },
  ]);
  let targetEntries = $state<
    Array<{ time: string; low: number; high: number }>
  >([{ time: "00:00", low: 70, high: 180 }]);

  // Track existing IDs for update vs create
  let existingSettingsId = $state<string | undefined>();
  let existingBasalId = $state<string | undefined>();
  let existingCarbRatioId = $state<string | undefined>();
  let existingSensitivityId = $state<string | undefined>();
  let existingTargetRangeId = $state<string | undefined>();

  // ── Auto-complete detection ─────────────────────────────────────

  const isExternallyManaged = $derived.by(() => {
    const settings = (summaryQuery.current?.therapySettings as any[])?.[0];
    return settings?.isExternallyManaged === true;
  });

  function setUnits(newUnits: "mg/dL" | "mmol/L") {
    if (newUnits === units) return;

    const prev = units === "mmol/L" ? "mmol" : ("mg/dl" as const);
    const next = newUnits === "mmol/L" ? "mmol" : ("mg/dl" as const);
    const convert = (v: number) =>
      convertToDisplayUnits(convertFromDisplayUnits(v, prev), next);

    sensitivityEntries = sensitivityEntries.map((e) => ({
      ...e,
      value: convert(e.value),
    }));
    targetEntries = targetEntries.map((e) => ({
      ...e,
      low: convert(e.low),
      high: convert(e.high),
    }));

    units = newUnits;
    glucoseUnits.current = next;
  }

  // ── Pre-populate from summary ───────────────────────────────────

  $effect(() => {
    const summary = summaryQuery.current;
    if (!summary) return;

    const settings = (summary.therapySettings as any[])?.[0];
    if (settings) {
      existingSettingsId = settings.id;
      profileName = settings.profileName ?? "Default";
      units = settings.units ?? "mg/dL";
      timezone = settings.timezone || detectedTz;
      carbsHr = settings.carbsHr ?? 20;
    }

    const basal = (summary.basalSchedules as any[])?.[0];
    if (basal) {
      existingBasalId = basal.id;
      basalEntries = (basal.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const carbRatio = (summary.carbRatioSchedules as any[])?.[0];
    if (carbRatio) {
      existingCarbRatioId = carbRatio.id;
      carbRatioEntries = (carbRatio.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const sensitivity = (summary.sensitivitySchedules as any[])?.[0];
    if (sensitivity) {
      existingSensitivityId = sensitivity.id;
      sensitivityEntries = (sensitivity.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        value: e.value ?? 0,
      }));
    }

    const targetRange = (summary.targetRangeSchedules as any[])?.[0];
    if (targetRange) {
      existingTargetRangeId = targetRange.id;
      targetEntries = (targetRange.entries ?? []).map((e: any) => ({
        time: e.time ?? "00:00",
        low: e.low ?? 0,
        high: e.high ?? 0,
      }));
    }
  });

  // ── Derived labels ──────────────────────────────────────────────

  const sensitivityUnit = $derived(
    units === "mmol/L" ? "mmol/L per U" : "mg/dL per U"
  );
  const targetUnit = $derived(units === "mmol/L" ? "mmol/L" : "mg/dL");

  // Sync appearance store on init
  $effect(() => {
    glucoseUnits.current = units === "mmol/L" ? "mmol" : "mg/dl";
  });

  // ── Programmatic form submission helpers ─────────────────────────

  let settingsSubmitResolve: ((success: boolean) => void) | null = null;
  let basalSubmitResolve: ((success: boolean) => void) | null = null;

  function submitHiddenForm(
    el: HTMLFormElement | null,
    setResolver: (resolver: (success: boolean) => void) => void
  ): Promise<boolean> {
    return new Promise((resolve) => {
      setResolver(resolve);
      tick().then(() => el?.requestSubmit());
    });
  }

  // ── Save all profile data ───────────────────────────────────────

  async function handleSave(): Promise<boolean> {
    saveError = null;
    saving = true;

    try {
      // 1. Save therapy settings (form-based)
      const settingsOk = await submitHiddenForm(
        settingsFormEl,
        (r) => (settingsSubmitResolve = r)
      );
      if (!settingsOk) {
        saveError = "Failed to save therapy settings.";
        return false;
      }

      // 2. Save basal schedule (form-based)
      const basalOk = await submitHiddenForm(
        basalFormEl,
        (r) => (basalSubmitResolve = r)
      );
      if (!basalOk) {
        saveError = "Failed to save basal schedule.";
        return false;
      }

      // 3. Save remaining schedules (command-based)
      const timestamp = new Date().toISOString();

      const carbRatioPayload = {
        profileName,
        entries: carbRatioEntries.map((e) => ({
          time: e.time,
          value: e.value,
        })),
        timestamp,
      };
      if (existingCarbRatioId) {
        await updateCarbRatioSchedule({
          id: existingCarbRatioId,
          request: carbRatioPayload,
        });
      } else {
        await createCarbRatioSchedule(carbRatioPayload);
      }

      const sensitivityPayload = {
        profileName,
        entries: sensitivityEntries.map((e) => ({
          time: e.time,
          value: e.value,
        })),
        timestamp,
      };
      if (existingSensitivityId) {
        await updateSensitivitySchedule({
          id: existingSensitivityId,
          request: sensitivityPayload,
        });
      } else {
        await createSensitivitySchedule(sensitivityPayload);
      }

      const targetRangePayload = {
        profileName,
        entries: targetEntries.map((e) => ({
          time: e.time,
          low: e.low,
          high: e.high,
        })),
        timestamp,
      };
      if (existingTargetRangeId) {
        await updateTargetRangeSchedule({
          id: existingTargetRangeId,
          request: targetRangePayload,
        });
      } else {
        await createTargetRangeSchedule(targetRangePayload);
      }

      return true;
    } catch {
      saveError = "Something went wrong. Please try again.";
      return false;
    } finally {
      saving = false;
    }
  }
</script>

<svelte:head>
  <title>Therapy Profile - Setup - Nocturne</title>
</svelte:head>

<!-- Hidden therapy settings form -->
{#if existingSettingsId}
  <form
    bind:this={settingsFormEl}
    class="hidden"
    {...updateSettingsForm
      .for(existingSettingsId)
      .enhance(async ({ submit }) => {
        await submit();
        const success = !!updateSettingsForm.for(existingSettingsId!).result;
        settingsSubmitResolve?.(success);
        settingsSubmitResolve = null;
      })}
  >
    <input type="hidden" name="id" value={existingSettingsId} />
    <input type="hidden" name="request.profileName" value={profileName} />
    <input type="hidden" name="request.units" value={units} />
    <input type="hidden" name="request.timezone" value={timezone} />
    <input type="hidden" name="n:request.carbsHr" value={carbsHr} />
    <input
      type="hidden"
      name="request.timestamp"
      value={new Date().toISOString()}
    />
  </form>
{:else}
  <form
    bind:this={settingsFormEl}
    class="hidden"
    {...createSettingsForm.enhance(async ({ submit }) => {
      await submit();
      const success = !!createSettingsForm.result;
      settingsSubmitResolve?.(success);
      settingsSubmitResolve = null;
    })}
  >
    <input type="hidden" name="profileName" value={profileName} />
    <input type="hidden" name="units" value={units} />
    <input type="hidden" name="timezone" value={timezone} />
    <input type="hidden" name="n:carbsHr" value={carbsHr} />
    <input type="hidden" name="timestamp" value={new Date().toISOString()} />
  </form>
{/if}

<!-- Hidden basal schedule form -->
{#if existingBasalId}
  <form
    bind:this={basalFormEl}
    class="hidden"
    {...updateBasalForm.for(existingBasalId).enhance(async ({ submit }) => {
      await submit();
      const success = !!updateBasalForm.for(existingBasalId!).result;
      basalSubmitResolve?.(success);
      basalSubmitResolve = null;
    })}
  >
    <input type="hidden" name="id" value={existingBasalId} />
    <input type="hidden" name="request.profileName" value={profileName} />
    <input
      type="hidden"
      name="request.timestamp"
      value={new Date().toISOString()}
    />
    {#each basalEntries as entry, i}
      <input
        type="hidden"
        name="request.entries[{i}].time"
        value={entry.time}
      />
      <input
        type="hidden"
        name="n:request.entries[{i}].value"
        value={entry.value}
      />
    {/each}
  </form>
{:else}
  <form
    bind:this={basalFormEl}
    class="hidden"
    {...createBasalForm.enhance(async ({ submit }) => {
      await submit();
      const success = !!createBasalForm.result;
      basalSubmitResolve?.(success);
      basalSubmitResolve = null;
    })}
  >
    <input type="hidden" name="profileName" value={profileName} />
    <input type="hidden" name="timestamp" value={new Date().toISOString()} />
    {#each basalEntries as entry, i}
      <input type="hidden" name="entries[{i}].time" value={entry.time} />
      <input type="hidden" name="n:entries[{i}].value" value={entry.value} />
    {/each}
  </form>
{/if}

<WizardShell
  title="Therapy Profile"
  description="Configure your therapy profile with basal rates, carb ratios, sensitivity factors, and target ranges."
  currentStep={8}
  totalSteps={8}
  prevHref="/settings/setup/connectors"
  nextHref="/settings"
  showSkip={true}
  saveDisabled={!profileName}
  {saving}
  onSave={handleSave}
>
  {#if isExternallyManaged}
    <Card.Root>
      <Card.Content class="py-8 text-center space-y-2">
        <CheckCircle class="h-12 w-12 mx-auto text-green-500" />
        <p class="font-medium">Profile Synced Automatically</p>
        <p class="text-sm text-muted-foreground">
          Your therapy profile is being managed by a connected system. Changes
          will sync automatically.
        </p>
      </Card.Content>
    </Card.Root>
  {:else}
    <!-- Basics -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Basics</Card.Title>
      </Card.Header>
      <Card.Content>
        <div class="grid gap-4 sm:grid-cols-2">
          <div class="space-y-2">
            <Label for="profile-name">Profile Name</Label>
            <Input
              id="profile-name"
              bind:value={profileName}
              placeholder="Default"
            />
          </div>

          <div class="space-y-2">
            <Label for="units">Units</Label>
            <Select.Root
              type="single"
              value={units}
              onValueChange={(v) => setUnits(v as "mg/dL" | "mmol/L")}
            >
              <Select.Trigger id="units">
                {units || "Select units"}
              </Select.Trigger>
              <Select.Content>
                <Select.Item value="mg/dL" label="mg/dL" />
                <Select.Item value="mmol/L" label="mmol/L" />
              </Select.Content>
            </Select.Root>
          </div>

          <div class="space-y-2">
            <Label>Timezone</Label>
            <Popover.Root bind:open={tzOpen}>
              <Popover.Trigger>
                {#snippet child({ props })}
                  <Button
                    variant="outline"
                    role="combobox"
                    aria-expanded={tzOpen}
                    class="w-full justify-between font-normal"
                    {...props}
                  >
                    <span class="truncate">
                      {timezone || "Select timezone"}
                    </span>
                    <ChevronsUpDown class="ml-2 h-4 w-4 shrink-0 opacity-50" />
                  </Button>
                {/snippet}
              </Popover.Trigger>
              <Popover.Content
                class="w-[--bits-popover-anchor-width] p-0"
                align="start"
              >
                <Command.Root shouldFilter={false}>
                  <Command.Input
                    placeholder="Search timezones..."
                    bind:value={tzSearch}
                  />
                  <Command.List class="max-h-60">
                    <Command.Empty>No timezone found.</Command.Empty>
                    {#each filteredTimezones as tz}
                      <Command.Item
                        value={tz}
                        onSelect={() => {
                          timezone = tz;
                          tzOpen = false;
                          tzSearch = "";
                          if (!existingSettingsId)
                            setUnits(unitsForTimezone(tz));
                        }}
                      >
                        <Check
                          class={cn(
                            "mr-2 h-4 w-4",
                            timezone === tz ? "opacity-100" : "opacity-0"
                          )}
                        />
                        {tz}
                      </Command.Item>
                    {/each}
                  </Command.List>
                </Command.Root>
              </Popover.Content>
            </Popover.Root>
          </div>

          <div class="space-y-2">
            <Label for="carbs-hr">Carb Absorption Rate</Label>
            <div class="flex items-center gap-2">
              <Input
                id="carbs-hr"
                type="number"
                bind:value={carbsHr}
                step={1}
                min={1}
                class="flex-1"
              />
              <span class="text-sm text-muted-foreground whitespace-nowrap">
                g/hr
              </span>
            </div>
          </div>
        </div>
      </Card.Content>
    </Card.Root>

    <!-- Schedules -->
    <Card.Root>
      <Card.Header>
        <Card.Title>Schedules</Card.Title>
      </Card.Header>
      <Card.Content class="space-y-6">
        <ScheduleEditor
          label="Basal Rates"
          unit="U/hr"
          bind:entries={basalEntries}
          step={0.05}
        />

        <Separator />

        <ScheduleEditor
          label="Carb Ratios (I:C)"
          unit="g/U"
          bind:entries={carbRatioEntries}
          step={0.5}
        />

        <Separator />

        <ScheduleEditor
          label="Insulin Sensitivity (ISF)"
          unit={sensitivityUnit}
          bind:entries={sensitivityEntries}
          step={1}
        />

        <Separator />

        <TargetRangeEditor
          label="Target Range"
          unit={targetUnit}
          bind:entries={targetEntries}
        />
      </Card.Content>
    </Card.Root>
  {/if}

  {#if saveError}
    <p class="text-sm text-destructive">{saveError}</p>
  {/if}
</WizardShell>
