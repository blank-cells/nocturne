<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import * as Avatar from "$lib/components/ui/avatar";
  import * as Dialog from "$lib/components/ui/dialog";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import {
    User,
    Mail,
    Shield,
    Clock,
    Key,
    LogOut,
    Settings,
    Fingerprint,
    Plus,
    Trash2,
    ShieldAlert,
    RefreshCw,
    Copy,
    Check,
    AlertTriangle,
    Loader2,
    Info,
    Server,
    Smartphone,
  } from "lucide-svelte";
  import * as InputOTP from "$lib/components/ui/input-otp";
  import QRCode from "qrcode";
  import type { PageData } from "./$types";
  import { formatSessionExpiry } from "$lib/stores/auth-store.svelte";
  import { formatDate } from "$lib/utils/formatting";
  import { startRegistration } from "@simplewebauthn/browser";
  import {
    registerOptions,
    registerComplete,
    listCredentials,
    removeCredential,
    getRecoveryStatus,
    regenerateRecoveryCodes,
  } from "$lib/api/generated/passkeys.generated.remote";
  import {
    setup as totpSetup,
    verifySetup as totpVerifySetup,
    listCredentials as totpListCredentials,
    removeCredential as totpRemoveCredential,
  } from "$lib/api/generated/totps.generated.remote";
  import LinkedOidcIdentities from "$lib/components/settings/LinkedOidcIdentities.svelte";
  import { page } from "$app/state";

  const { data }: { data: PageData } = $props();

  const user = $derived(data.user);

  /** Get initials from user name */
  function getInitials(name: string): string {
    return name
      .split(" ")
      .map((n) => n[0])
      .join("")
      .toUpperCase()
      .slice(0, 2);
  }

  /** Time until session expires in seconds */
  const timeUntilExpiry = $derived.by(() => {
    if (!user?.expiresAt) return null;
    const now = new Date();
    const expiresAt = new Date(user.expiresAt);
    const diff = expiresAt.getTime() - now.getTime();
    return Math.max(0, Math.floor(diff / 1000));
  });

  /** Handle logout */
  function handleLogout() {
    window.location.href = "/auth/logout";
  }

  // ============================================================================
  // Security State
  // ============================================================================

  const credentialsQuery = listCredentials();
  const recoveryQuery = getRecoveryStatus();

  let errorMessage = $state<string | null>(null);
  let successMessage = $state<string | null>(null);

  // Passkey add flow
  let isRegistering = $state(false);
  let showLabelDialog = $state(false);
  let newPasskeyLabel = $state("");

  // Passkey remove flow
  let isRemoving = $state<string | null>(null);
  let showRemoveDialog = $state(false);
  let removeTarget = $state<{ id?: string; label?: string | null } | null>(null);

  // Recovery codes
  let showRegenerateDialog = $state(false);
  let isRegenerating = $state(false);
  let showNewCodesDialog = $state(false);
  let newRecoveryCodes = $state<string[]>([]);
  let copiedCodes = $state(false);

  // ============================================================================
  // TOTP Authenticator State
  // ============================================================================

  const totpQuery = totpListCredentials();
  let showTotpSetup = $state(false);
  let totpSetupData = $state<{ provisioningUri?: string; base32Secret?: string; challengeToken?: string } | null>(null);
  let totpQrDataUrl = $state<string | null>(null);
  let totpVerifyCode = $state("");
  let totpLabel = $state("");
  let totpSetupLoading = $state(false);
  let totpSetupError = $state<string | null>(null);
  let totpRemovingId = $state<string | null>(null);
  let showTotpRemoveDialog = $state(false);
  let totpRemoveTarget = $state<{ id?: string; label?: string | null } | null>(null);

  const totpCredentials = $derived(totpQuery.current ?? []);
  const maxTotpCredentials = 10;

  const credentials = $derived(credentialsQuery.current?.credentials ?? []);
  const primaryAuthFactorCount = $derived(
    credentialsQuery.current?.primaryAuthFactorCount ?? 0
  );
  const recoveryStatus = $derived(recoveryQuery.current);
  const isSecurityLoading = $derived(credentialsQuery.loading);
  const canRemovePasskey = $derived(primaryAuthFactorCount > 1);

  // Handle ?linked= query param from OIDC link flow return
  $effect(() => {
    const linked = page.url.searchParams.get("linked");
    if (linked === "success") {
      successMessage = "Account linked successfully.";
      clearMessages();
    } else if (linked === "already") {
      successMessage = "This account was already linked.";
      clearMessages();
    }
    if (linked && typeof window !== "undefined") {
      const url = new URL(window.location.href);
      url.searchParams.delete("linked");
      history.replaceState({}, "", url.toString());
    }
  });
  const maxPasskeys = 20;

  // ============================================================================
  // Passkey registration
  // ============================================================================

  async function handleAddPasskey() {
    if (!user?.subjectId || !user?.name) return;
    isRegistering = true;
    errorMessage = null;

    try {
      const response = await registerOptions({ subjectId: user.subjectId, username: user.name });
      const options = JSON.parse(response.options ?? "");
      const challengeToken = response.challengeToken ?? "";

      const attestation = await startRegistration({ optionsJSON: options });

      await registerComplete({
        attestationResponseJson: JSON.stringify(attestation),
        challengeToken,
      });

      newPasskeyLabel = "";
      showLabelDialog = true;
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : "Failed to register passkey.";
    } finally {
      isRegistering = false;
    }
  }

  function handleLabelDialogClose() {
    showLabelDialog = false;
    newPasskeyLabel = "";
    successMessage = "Passkey added successfully.";
    clearMessages();
  }

  // ============================================================================
  // Passkey removal
  // ============================================================================

  function confirmRemovePasskey(credential: { id?: string; label?: string | null }) {
    removeTarget = credential;
    showRemoveDialog = true;
  }

  async function handleRemovePasskey() {
    if (!removeTarget?.id) return;
    isRemoving = removeTarget.id;
    errorMessage = null;
    showRemoveDialog = false;

    try {
      await removeCredential(removeTarget.id);
      successMessage = "Passkey removed.";
      clearMessages();
    } catch (err) {
      errorMessage =
        err instanceof Error ? err.message : "Failed to remove passkey.";
    } finally {
      isRemoving = null;
      removeTarget = null;
    }
  }

  // ============================================================================
  // Recovery codes
  // ============================================================================

  async function handleRegenerateCodes() {
    isRegenerating = true;
    errorMessage = null;
    showRegenerateDialog = false;

    try {
      const result = await regenerateRecoveryCodes();
      newRecoveryCodes = result.codes ?? [];
      showNewCodesDialog = true;
    } catch (err) {
      errorMessage = "Failed to regenerate recovery codes.";
    } finally {
      isRegenerating = false;
    }
  }

  async function copyRecoveryCodes() {
    const text = newRecoveryCodes.join("\n");
    await navigator.clipboard.writeText(text);
    copiedCodes = true;
    setTimeout(() => (copiedCodes = false), 2000);
  }

  // ============================================================================
  // TOTP Authenticator Management
  // ============================================================================

  async function handleStartTotpSetup() {
    totpSetupLoading = true;
    totpSetupError = null;
    totpVerifyCode = "";
    totpLabel = "";
    totpQrDataUrl = null;

    try {
      const result = await totpSetup();
      totpSetupData = {
        provisioningUri: result.provisioningUri,
        base32Secret: result.base32Secret,
        challengeToken: result.challengeToken,
      };

      if (result.provisioningUri) {
        totpQrDataUrl = await QRCode.toDataURL(result.provisioningUri, {
          width: 200,
          margin: 2,
          color: { dark: "#000000", light: "#ffffff" },
        });
      }

      showTotpSetup = true;
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : "Failed to start authenticator setup.";
    } finally {
      totpSetupLoading = false;
    }
  }

  async function handleCompleteTotpSetup() {
    if (totpSetupLoading) return;
    if (!totpSetupData?.challengeToken || totpVerifyCode.length !== 6) return;
    totpSetupLoading = true;
    totpSetupError = null;

    try {
      await totpVerifySetup({
        challengeToken: totpSetupData.challengeToken,
        code: totpVerifyCode,
        label: totpLabel.trim() || undefined,
      });

      showTotpSetup = false;
      totpSetupData = null;
      totpQrDataUrl = null;
      totpVerifyCode = "";
      totpLabel = "";
      successMessage = "Authenticator app added successfully.";
      clearMessages();
    } catch (err) {
      totpSetupError = err instanceof Error ? err.message : "Verification failed. Check the code and try again.";
    } finally {
      totpSetupLoading = false;
    }
  }

  function confirmRemoveTotp(credential: { id?: string; label?: string | null }) {
    totpRemoveTarget = credential;
    showTotpRemoveDialog = true;
  }

  async function handleRemoveTotp() {
    if (!totpRemoveTarget?.id) return;
    totpRemovingId = totpRemoveTarget.id;
    errorMessage = null;
    showTotpRemoveDialog = false;

    try {
      await totpRemoveCredential(totpRemoveTarget.id);
      successMessage = "Authenticator removed.";
      clearMessages();
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : "Failed to remove authenticator.";
    } finally {
      totpRemovingId = null;
      totpRemoveTarget = null;
    }
  }

  function clearMessages() {
    setTimeout(() => {
      successMessage = null;
      errorMessage = null;
    }, 3000);
  }
</script>

<svelte:head>
  <title>Account - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  {#if user}
    <div class="space-y-1">
      <h1 class="text-2xl font-bold tracking-tight">Account</h1>
      <p class="text-muted-foreground">
        Manage your account and security settings
      </p>
    </div>

    <!-- User Profile Card -->
    <Card.Root>
      <Card.Header>
        <div class="flex items-start gap-4">
          <Avatar.Root class="h-16 w-16">
            <Avatar.Fallback class="bg-primary/10 text-primary text-xl">
              {getInitials(user.name)}
            </Avatar.Fallback>
          </Avatar.Root>
          <div class="space-y-1 flex-1">
            <Card.Title class="text-xl">{user.name}</Card.Title>
            {#if user.email}
              <Card.Description class="flex items-center gap-2">
                <Mail class="h-4 w-4" />
                {user.email}
              </Card.Description>
            {/if}
          </div>
        </div>
      </Card.Header>
      <Card.Content class="space-y-6">
        <!-- Account Details -->
        <div class="space-y-4">
          <h3
            class="text-sm font-medium text-muted-foreground uppercase tracking-wider"
          >
            Account Details
          </h3>

          <div class="grid gap-4 sm:grid-cols-2">
            <div class="space-y-1">
              <p class="text-sm text-muted-foreground">Subject ID</p>
              <p class="text-sm font-mono bg-muted px-2 py-1 rounded">
                {user.subjectId}
              </p>
            </div>

            {#if user.expiresAt}
              <div class="space-y-1">
                <p class="text-sm text-muted-foreground">Session Expires</p>
                <p class="text-sm flex items-center gap-2">
                  <Clock class="h-4 w-4 text-muted-foreground" />
                  {formatDate(user.expiresAt)}
                  {#if timeUntilExpiry !== null}
                    <span class="text-muted-foreground">
                      ({formatSessionExpiry(timeUntilExpiry)})
                    </span>
                  {/if}
                </p>
              </div>
            {/if}
          </div>
        </div>

        <Separator />

        <!-- Roles -->
        <div class="space-y-4">
          <h3
            class="text-sm font-medium text-muted-foreground uppercase tracking-wider flex items-center gap-2"
          >
            <Shield class="h-4 w-4" />
            Roles
          </h3>

          {#if user.roles.length > 0}
            <div class="flex flex-wrap gap-2">
              {#each user.roles as role}
                <Badge variant="secondary" class="text-sm">
                  {role}
                </Badge>
              {/each}
            </div>
          {:else}
            <p class="text-sm text-muted-foreground">No roles assigned</p>
          {/if}
        </div>

        <Separator />

        <!-- Permissions -->
        <div class="space-y-4">
          <h3
            class="text-sm font-medium text-muted-foreground uppercase tracking-wider flex items-center gap-2"
          >
            <Key class="h-4 w-4" />
            Permissions
          </h3>

          {#if user.permissions.length > 0}
            <div class="flex flex-wrap gap-2">
              {#each user.permissions as permission}
                <Badge variant="outline" class="text-xs font-mono">
                  {permission}
                </Badge>
              {/each}
            </div>
          {:else}
            <p class="text-sm text-muted-foreground">No explicit permissions</p>
          {/if}
        </div>
      </Card.Content>
      <Card.Footer class="flex flex-col sm:flex-row gap-2 border-t pt-6">
        <Button variant="outline" href="/settings" class="w-full sm:w-auto">
          <Settings class="mr-2 h-4 w-4" />
          Back to Settings
        </Button>
        <Button
          variant="destructive"
          onclick={handleLogout}
          class="w-full sm:w-auto"
        >
          <LogOut class="mr-2 h-4 w-4" />
          Log Out
        </Button>
      </Card.Footer>
    </Card.Root>

    {#if errorMessage}
      <div
        class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3"
      >
        <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
        <p class="text-sm text-destructive">{errorMessage}</p>
      </div>
    {/if}

    {#if successMessage}
      <div
        class="flex items-start gap-3 rounded-md border border-green-200 bg-green-50 p-3 dark:border-green-900/50 dark:bg-green-900/20"
      >
        <Check
          class="mt-0.5 h-4 w-4 shrink-0 text-green-600 dark:text-green-400"
        />
        <p class="text-sm text-green-800 dark:text-green-200">
          {successMessage}
        </p>
      </div>
    {/if}

    {#if isSecurityLoading}
      <Card.Root>
        <Card.Content class="flex items-center justify-center py-12">
          <Loader2 class="h-6 w-6 animate-spin text-muted-foreground" />
        </Card.Content>
      </Card.Root>
    {:else}
      <!-- Section 1: Registered Passkeys -->
      <Card.Root>
        <Card.Header>
          <div class="flex items-center justify-between">
            <div>
              <Card.Title class="flex items-center gap-2">
                <Fingerprint class="h-5 w-5" />
                Passkeys
              </Card.Title>
              <Card.Description>
                Passkeys provide secure, phishing-resistant authentication using
                your device's biometrics or security key.
              </Card.Description>
            </div>
            <Button
              variant="outline"
              size="sm"
              disabled={isRegistering || credentials.length >= maxPasskeys}
              onclick={handleAddPasskey}
            >
              {#if isRegistering}
                <Loader2 class="mr-1.5 h-3.5 w-3.5 animate-spin" />
              {:else}
                <Plus class="mr-1.5 h-3.5 w-3.5" />
              {/if}
              Add passkey
            </Button>
          </div>
        </Card.Header>
        <Card.Content class="space-y-3">
          {#if credentials.length === 0}
            <div class="flex flex-col items-center justify-center py-8 text-center">
              <div
                class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
              >
                <Fingerprint class="h-6 w-6 text-muted-foreground" />
              </div>
              <p class="text-sm text-muted-foreground max-w-sm">
                No passkeys registered. Add a passkey to enable passwordless
                sign-in.
              </p>
            </div>
          {:else}
            {#each credentials as credential (credential.id)}
              <div
                class="flex items-center justify-between gap-4 rounded-md border p-3"
              >
                <div class="space-y-1 flex-1 min-w-0">
                  <p class="text-sm font-medium">
                    {credential.label ?? "Unnamed passkey"}
                  </p>
                  <div
                    class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
                  >
                    <span class="flex items-center gap-1">
                      <Clock class="h-3 w-3" />
                      Created {formatDate(credential.createdAt)}
                    </span>
                    {#if credential.lastUsedAt}
                      <span class="flex items-center gap-1">
                        <Clock class="h-3 w-3" />
                        Last used {formatDate(credential.lastUsedAt)}
                      </span>
                    {/if}
                  </div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  class="text-destructive hover:text-destructive shrink-0"
                  disabled={!canRemovePasskey ||
                    isRemoving === credential.id}
                  onclick={() => confirmRemovePasskey(credential)}
                >
                  {#if isRemoving === credential.id}
                    <Loader2 class="h-3.5 w-3.5 animate-spin" />
                  {:else}
                    <Trash2 class="h-3.5 w-3.5" />
                  {/if}
                </Button>
              </div>
            {/each}
          {/if}

          {#if credentials.length >= maxPasskeys}
            <p class="text-xs text-muted-foreground">
              Maximum of {maxPasskeys} passkeys reached.
            </p>
          {/if}

          {#if !canRemovePasskey && credentials.length > 0}
            <p class="text-xs text-muted-foreground">
              You cannot remove your only passkey without an alternative sign-in
              method linked to your account.
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Section 1b: Linked OIDC identities -->
      <LinkedOidcIdentities {primaryAuthFactorCount} />

      <!-- Section 2: Authenticator Apps -->
      <Card.Root>
        <Card.Header>
          <div class="flex items-center justify-between">
            <div>
              <Card.Title class="flex items-center gap-2">
                <Smartphone class="h-5 w-5" />
                Authenticator Apps
              </Card.Title>
              <Card.Description>
                Use an authenticator app like Google Authenticator or Authy to
                generate time-based one-time passwords for sign-in.
              </Card.Description>
            </div>
            <Button
              variant="outline"
              size="sm"
              disabled={totpSetupLoading || totpCredentials.length >= maxTotpCredentials}
              onclick={handleStartTotpSetup}
            >
              {#if totpSetupLoading}
                <Loader2 class="mr-1.5 h-3.5 w-3.5 animate-spin" />
              {:else}
                <Plus class="mr-1.5 h-3.5 w-3.5" />
              {/if}
              Add authenticator
            </Button>
          </div>
        </Card.Header>
        <Card.Content class="space-y-3">
          {#if totpCredentials.length === 0}
            <div class="flex flex-col items-center justify-center py-8 text-center">
              <div
                class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
              >
                <Smartphone class="h-6 w-6 text-muted-foreground" />
              </div>
              <p class="text-sm text-muted-foreground max-w-sm">
                No authenticator apps registered. Add one for an additional
                sign-in method.
              </p>
            </div>
          {:else}
            {#each totpCredentials as credential (credential.id)}
              <div
                class="flex items-center justify-between gap-4 rounded-md border p-3"
              >
                <div class="space-y-1 flex-1 min-w-0">
                  <p class="text-sm font-medium">
                    {credential.label ?? "Unnamed authenticator"}
                  </p>
                  <div
                    class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
                  >
                    <span class="flex items-center gap-1">
                      <Clock class="h-3 w-3" />
                      Created {formatDate(credential.createdAt)}
                    </span>
                    {#if credential.lastUsedAt}
                      <span class="flex items-center gap-1">
                        <Clock class="h-3 w-3" />
                        Last used {formatDate(credential.lastUsedAt)}
                      </span>
                    {/if}
                  </div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  class="text-destructive hover:text-destructive shrink-0"
                  disabled={totpRemovingId === credential.id}
                  onclick={() => confirmRemoveTotp(credential)}
                >
                  {#if totpRemovingId === credential.id}
                    <Loader2 class="h-3.5 w-3.5 animate-spin" />
                  {:else}
                    <Trash2 class="h-3.5 w-3.5" />
                  {/if}
                </Button>
              </div>
            {/each}
          {/if}

          {#if totpCredentials.length >= maxTotpCredentials}
            <p class="text-xs text-muted-foreground">
              Maximum of {maxTotpCredentials} authenticators reached.
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Section 3: Recovery Codes -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="flex items-center gap-2">
            <ShieldAlert class="h-5 w-5" />
            Recovery Codes
          </Card.Title>
          <Card.Description>
            Recovery codes allow you to access your account if you lose all your
            passkeys. Store them in a safe place.
          </Card.Description>
        </Card.Header>
        <Card.Content class="space-y-4">
          {#if recoveryStatus}
            <div class="flex items-center justify-between">
              <div class="space-y-1">
                <p class="text-sm font-medium">
                  {recoveryStatus.remainingCodes} of {recoveryStatus.totalCodes} recovery
                  codes remaining
                </p>
                <p class="text-xs text-muted-foreground">
                  Each code can only be used once.
                </p>
              </div>
              <Badge
                variant={(recoveryStatus.remainingCodes ?? 0) > 2
                  ? "secondary"
                  : "destructive"}
              >
                {recoveryStatus.remainingCodes} remaining
              </Badge>
            </div>
          {:else}
            <p class="text-sm text-muted-foreground">
              No recovery codes have been generated yet.
            </p>
          {/if}

          <Separator />

          <Button
            variant="outline"
            disabled={isRegenerating}
            onclick={() => (showRegenerateDialog = true)}
          >
            {#if isRegenerating}
              <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
            {:else}
              <RefreshCw class="mr-1.5 h-4 w-4" />
            {/if}
            Regenerate recovery codes
          </Button>
        </Card.Content>
      </Card.Root>

      <!-- Section 4: Recovery Mode Info -->
      <Card.Root class="border-muted">
        <Card.Header>
          <Card.Title class="flex items-center gap-2 text-muted-foreground">
            <Server class="h-5 w-5" />
            Server-Side Account Recovery
          </Card.Title>
        </Card.Header>
        <Card.Content>
          <div
            class="flex items-start gap-3 rounded-md border border-border bg-muted/30 p-4"
          >
            <Info class="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" />
            <div class="space-y-2">
              <p class="text-sm text-muted-foreground">
                If you lose all your passkeys and recovery codes, you can recover
                your account by setting the
                <code
                  class="rounded bg-muted px-1.5 py-0.5 text-xs font-mono text-foreground"
                >
                  NOCTURNE_RECOVERY_MODE
                </code>
                environment variable on your server.
              </p>
              <p class="text-sm text-muted-foreground">
                This enables a temporary recovery flow that allows you to register
                a new passkey. It requires physical access to the server
                environment.
              </p>
            </div>
          </div>
        </Card.Content>
      </Card.Root>
    {/if}
  {:else}
    <!-- Not logged in -->
    <div
      class="min-h-[70vh] flex flex-col items-center justify-center p-4 animate-in fade-in slide-in-from-bottom-4 duration-500"
    >
      <Card.Root class="w-full max-w-md text-center shadow-lg">
        <Card.Header class="pb-4 pt-8">
          <div
            class="mx-auto w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center mb-6"
          >
            <User class="h-8 w-8 text-primary" />
          </div>
          <Card.Title class="text-2xl font-bold">Not Signed In</Card.Title>
          <Card.Description class="text-base mt-2">
            Sign in to access your account dashboard and manage your settings.
          </Card.Description>
        </Card.Header>
        <Card.Content class="pb-8">
          <Button
            href="/auth/login"
            size="lg"
            class="w-full sm:w-auto min-w-[200px] font-medium"
          >
            <User class="mr-2 h-5 w-5" />
            Sign In with Nocturne
          </Button>
        </Card.Content>
      </Card.Root>
    </div>
  {/if}
</div>

<!-- Label Dialog (after passkey registration) -->
<Dialog.Root bind:open={showLabelDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Name your passkey</Dialog.Title>
      <Dialog.Description>
        Give this passkey a name to help you identify it later (e.g. "MacBook
        Touch ID", "YubiKey 5").
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="space-y-2">
        <Label for="passkey-label">Label (optional)</Label>
        <Input
          id="passkey-label"
          type="text"
          placeholder="e.g. MacBook Touch ID"
          bind:value={newPasskeyLabel}
        />
      </div>
    </div>
    <Dialog.Footer>
      <Button variant="outline" onclick={handleLabelDialogClose}>
        Skip
      </Button>
      <Button onclick={handleLabelDialogClose}>
        Save
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Remove Confirmation Dialog -->
<Dialog.Root bind:open={showRemoveDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Remove passkey</Dialog.Title>
      <Dialog.Description>
        Are you sure you want to remove "{removeTarget?.label ??
          "Unnamed passkey"}"? You will no longer be able to sign in with this
        passkey.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (showRemoveDialog = false)}>
        Cancel
      </Button>
      <Button variant="destructive" onclick={handleRemovePasskey}>
        Remove
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- Regenerate Confirmation Dialog -->
<Dialog.Root bind:open={showRegenerateDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Regenerate recovery codes</Dialog.Title>
      <Dialog.Description>
        This will invalidate all existing recovery codes and generate new ones.
        Make sure to save the new codes in a safe place.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button
        variant="outline"
        onclick={() => (showRegenerateDialog = false)}
      >
        Cancel
      </Button>
      <Button variant="destructive" onclick={handleRegenerateCodes}>
        Regenerate
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- New Recovery Codes Dialog -->
<Dialog.Root bind:open={showNewCodesDialog}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>New recovery codes</Dialog.Title>
      <Dialog.Description>
        Save these codes in a safe place. Each code can only be used once. This
        is the only time they will be shown.
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      <div class="grid grid-cols-2 gap-2 rounded-md border bg-muted/30 p-4">
        {#each newRecoveryCodes as code}
          <p class="font-mono text-sm text-center">{code}</p>
        {/each}
      </div>
      <Button variant="outline" class="w-full" onclick={copyRecoveryCodes}>
        {#if copiedCodes}
          <Check class="mr-1.5 h-4 w-4 text-green-600" />
          Copied
        {:else}
          <Copy class="mr-1.5 h-4 w-4" />
          Copy all codes
        {/if}
      </Button>
    </div>
    <Dialog.Footer>
      <Button
        onclick={() => {
          showNewCodesDialog = false;
          newRecoveryCodes = [];
          copiedCodes = false;
        }}
      >
        Done
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- TOTP Setup Dialog -->
<Dialog.Root bind:open={showTotpSetup}>
  <Dialog.Content class="max-w-md">
    <Dialog.Header>
      <Dialog.Title>Set up authenticator app</Dialog.Title>
      <Dialog.Description>
        Scan the QR code with your authenticator app, then enter the 6-digit
        code to verify.
      </Dialog.Description>
    </Dialog.Header>
    <div class="space-y-4 py-4">
      {#if totpQrDataUrl}
        <div class="flex justify-center">
          <div class="rounded-md border bg-white p-2">
            <img src={totpQrDataUrl} alt="TOTP QR code" class="h-[200px] w-[200px]" />
          </div>
        </div>
      {/if}

      {#if totpSetupData?.base32Secret}
        <div class="space-y-1">
          <p class="text-xs text-muted-foreground">
            Or enter this secret manually:
          </p>
          <p class="rounded-md border bg-muted/30 px-3 py-2 font-mono text-xs text-center select-all break-all">
            {totpSetupData.base32Secret}
          </p>
        </div>
      {/if}

      <div class="space-y-2">
        <Label for="totp-label">Label (optional)</Label>
        <Input
          id="totp-label"
          type="text"
          placeholder="e.g. Google Authenticator"
          bind:value={totpLabel}
        />
      </div>

      <div class="space-y-2">
        <Label>Verification code</Label>
        <div class="flex justify-center">
          <InputOTP.Root maxlength={6} bind:value={totpVerifyCode} onComplete={handleCompleteTotpSetup}>
            {#snippet children({ cells })}
              <InputOTP.Group>
                {#each cells.slice(0, 3) as cell}
                  <InputOTP.Slot {cell} />
                {/each}
              </InputOTP.Group>
              <InputOTP.Separator />
              <InputOTP.Group>
                {#each cells.slice(3, 6) as cell}
                  <InputOTP.Slot {cell} />
                {/each}
              </InputOTP.Group>
            {/snippet}
          </InputOTP.Root>
        </div>
      </div>

      {#if totpSetupError}
        <div class="flex items-start gap-3 rounded-md border border-destructive/20 bg-destructive/5 p-3">
          <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
          <p class="text-sm text-destructive">{totpSetupError}</p>
        </div>
      {/if}
    </div>
    <Dialog.Footer>
      <Button
        variant="outline"
        onclick={() => {
          showTotpSetup = false;
          totpSetupData = null;
          totpQrDataUrl = null;
          totpVerifyCode = "";
          totpLabel = "";
          totpSetupError = null;
        }}
      >
        Cancel
      </Button>
      <Button
        disabled={totpSetupLoading || totpVerifyCode.length !== 6}
        onclick={handleCompleteTotpSetup}
      >
        {#if totpSetupLoading}
          <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
        {/if}
        Verify and save
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<!-- TOTP Remove Confirmation Dialog -->
<Dialog.Root bind:open={showTotpRemoveDialog}>
  <Dialog.Content>
    <Dialog.Header>
      <Dialog.Title>Remove authenticator</Dialog.Title>
      <Dialog.Description>
        Are you sure you want to remove "{totpRemoveTarget?.label ??
          "Unnamed authenticator"}"? You will no longer be able to sign in with
        this authenticator app.
      </Dialog.Description>
    </Dialog.Header>
    <Dialog.Footer>
      <Button variant="outline" onclick={() => (showTotpRemoveDialog = false)}>
        Cancel
      </Button>
      <Button variant="destructive" onclick={handleRemoveTotp}>
        Remove
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
