<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import {
    Fingerprint,
    Loader2,
    Check,
    Copy,
    AlertTriangle,
    ShieldCheck,
  } from "lucide-svelte";
  import { startRegistration } from "@simplewebauthn/browser";
  import {
    setupOptions,
    setupComplete,
  } from "$lib/api/generated/passkeys.generated.remote";
  import { setAuthCookies } from "$routes/auth/auth.remote";
  import { invalidateAll } from "$app/navigation";

  // Form state
  let displayName = $state("");
  let username = $state("");

  // Registration state
  let isRegistering = $state(false);
  let registrationComplete = $state(false);
  let recoveryCodes = $state<string[]>([]);
  let errorMessage = $state<string | null>(null);
  let codesCopied = $state(false);

  const canRegister = $derived(
    displayName.trim().length > 0 && username.trim().length > 0
  );

  const canProceed = $derived(registrationComplete && (codesCopied || recoveryCodes.length === 0));

  async function handleRegister() {
    isRegistering = true;
    errorMessage = null;

    try {
      // Use setup endpoints — creates the first user + starts passkey registration
      const response = await setupOptions({
        username: username.trim(),
        displayName: displayName.trim(),
      });
      const options = JSON.parse(response.options ?? "");
      const challengeToken = response.challengeToken ?? "";

      const attestation = await startRegistration({ optionsJSON: options });

      const result = await setupComplete({
        attestationResponseJson: JSON.stringify(attestation),
        challengeToken,
      });

      // Set auth cookies so the user is logged in immediately
      if (result.accessToken) {
        await setAuthCookies({
          accessToken: result.accessToken,
          refreshToken: result.refreshToken ?? undefined,
          expiresIn: result.expiresIn ?? undefined,
        });
        await invalidateAll();
      }

      registrationComplete = true;
      recoveryCodes = result.recoveryCodes ?? [];
    } catch (err) {
      errorMessage =
        err instanceof Error ? err.message : "Failed to register passkey";
    } finally {
      isRegistering = false;
    }
  }

  async function copyRecoveryCodes() {
    const text = recoveryCodes.join("\n");
    try {
      await navigator.clipboard.writeText(text);
      codesCopied = true;
    } catch {
      // Fallback: select text for manual copy
      codesCopied = true;
    }
  }

  async function handleSave(): Promise<boolean> {
    // The passkey is already registered; we just need to confirm codes were saved
    return canProceed;
  }
</script>

<svelte:head>
  <title>Passkey Setup - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Set Up Your Passkey"
  description="Create a passkey for secure, passwordless authentication. You will also receive recovery codes in case you lose access to your passkey."
  currentStep={1}
  totalSteps={8}
  nextHref="/settings/setup/patient"
  showSkip={false}
  saveDisabled={!canProceed}
  onSave={handleSave}
>
  {#if !registrationComplete}
    <!-- Registration form -->
    <div class="space-y-4">
      <div class="space-y-2">
        <Label for="display-name">Display name</Label>
        <Input
          id="display-name"
          type="text"
          placeholder="Your name"
          bind:value={displayName}
          disabled={isRegistering}
        />
        <p class="text-xs text-muted-foreground">
          This is how you will appear to others.
        </p>
      </div>

      <div class="space-y-2">
        <Label for="pk-username">Username</Label>
        <Input
          id="pk-username"
          type="text"
          placeholder="your-username"
          bind:value={username}
          disabled={isRegistering}
        />
        <p class="text-xs text-muted-foreground">
          A unique identifier for your account. Used for non-discoverable login.
        </p>
      </div>

      {#if errorMessage}
        <div
          class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3"
        >
          <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
          <p class="text-sm text-destructive">{errorMessage}</p>
        </div>
      {/if}

      <Button
        class="w-full"
        size="lg"
        disabled={!canRegister || isRegistering}
        onclick={handleRegister}
      >
        {#if isRegistering}
          <Loader2 class="mr-2 h-5 w-5 animate-spin" />
          Waiting for passkey...
        {:else}
          <Fingerprint class="mr-2 h-5 w-5" />
          Register passkey
        {/if}
      </Button>
    </div>
  {:else}
    <!-- Recovery codes -->
    <div class="space-y-4">
      <div
        class="flex items-start gap-3 rounded-md border border-green-500/20 bg-green-500/5 p-3"
      >
        <Check class="mt-0.5 h-4 w-4 shrink-0 text-green-600" />
        <p class="text-sm text-green-700 dark:text-green-400">
          Passkey registered successfully.
        </p>
      </div>

      <div class="space-y-3">
        <div class="flex items-center gap-2">
          <ShieldCheck class="h-5 w-5 text-primary" />
          <h3 class="font-medium">Recovery Codes</h3>
        </div>
        <p class="text-sm text-muted-foreground">
          Save these recovery codes in a safe place. If you lose access to your
          passkey, you can use one of these codes to sign in. Each code can only
          be used once.
        </p>

        {#if recoveryCodes.length > 0}
          <div
            class="grid grid-cols-2 gap-2 rounded-lg border bg-muted/50 p-4"
          >
            {#each recoveryCodes as code}
              <code class="rounded bg-background px-2 py-1 text-center text-sm font-mono">
                {code}
              </code>
            {/each}
          </div>

          <Button
            variant={codesCopied ? "outline" : "default"}
            class="w-full"
            onclick={copyRecoveryCodes}
          >
            {#if codesCopied}
              <Check class="mr-2 h-4 w-4" />
              Codes copied
            {:else}
              <Copy class="mr-2 h-4 w-4" />
              Copy recovery codes
            {/if}
          </Button>

          {#if !codesCopied}
            <p class="text-center text-xs text-muted-foreground">
              You must copy your recovery codes before continuing.
            </p>
          {/if}
        {:else}
          <p class="text-sm text-muted-foreground">
            No recovery codes were returned. You can generate new ones later
            from your account settings.
          </p>
        {/if}
      </div>
    </div>
  {/if}
</WizardShell>
