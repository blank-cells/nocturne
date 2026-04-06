<script lang="ts">
  import { page } from "$app/state";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import * as Card from "$lib/components/ui/card";
  import * as AlertDialog from "$lib/components/ui/alert-dialog";
  import * as Collapsible from "$lib/components/ui/collapsible";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import PermissionPicker from "$lib/components/rbac/PermissionPicker.svelte";
  import {
    Users,
    Trash2,
    Check,
    AlertTriangle,
    Clock,
    Link,
    Copy,
    Loader2,
    ChevronDown,
    ChevronUp,
    Settings2,
    UserPlus,
    ShieldAlert,
  } from "lucide-svelte";
  import { formatDate } from "$lib/utils/formatting";
  import { getMembers } from "$lib/api/generated/memberinvites.generated.remote";
  import {
    listInvites,
    createInvite,
    revokeInvite,
    removeMember,
  } from "$api/generated/tenants.generated.remote";
  import { getMultitenancyInfo } from "$api/generated/metadatas.generated.remote";
  import { getRoles } from "$lib/api/generated/roles.generated.remote";
  import {
    setMemberRoles,
    setMemberPermissions,
  } from "$lib/api/generated/memberinvites.generated.remote";

  const effectivePermissions: string[] = $derived(
    (page.data as any).effectivePermissions ?? [],
  );
  const hasStar = $derived(effectivePermissions.includes("*"));
  const canInvite = $derived(
    hasStar || effectivePermissions.includes("members.invite"),
  );
  const canManageMembers = $derived(
    hasStar ||
      effectivePermissions.includes("members.manage") ||
      effectivePermissions.includes("sharing.manage"),
  );
  const canEditMemberRoles = $derived(
    hasStar || effectivePermissions.includes("members.manage"),
  );

  // Tenant
  const multitenancyQuery = $derived(getMultitenancyInfo());
  const tenantId = $derived(multitenancyQuery.current?.currentTenantId ?? null);

  // Queries
  const membersQuery = $derived(getMembers());
  const invitesQuery = $derived(tenantId ? listInvites(tenantId) : null);
  const rolesQuery = $derived(getRoles());

  // Data
  const allMembers = $derived(membersQuery.current ?? []);
  const invites = $derived(invitesQuery?.current ?? []);
  const activeInvites = $derived(invites.filter((i) => i.isValid));
  const allRoles = $derived(rolesQuery.current ?? []);

  // --- Invite form state ---
  let showCreateInvite = $state(false);
  let inviteLabel = $state("");
  let inviteRoleIds = $state<string[]>([]);
  let inviteDirectPermissions = $state<string[]>([]);
  let showInvitePermissions = $state(false);
  let allowMultipleUses = $state(false);
  let limitTo24Hours = $state(false);
  let createdInviteUrl = $state<string | null>(null);
  let copiedInvite = $state(false);
  let isCreatingInvite = $state(false);

  // --- Loading states ---
  let isRevokingInvite = $state<string | null>(null);
  let isRemovingMember = $state<string | null>(null);
  let errorMessage = $state<string | null>(null);
  let successMessage = $state<string | null>(null);

  // --- Member edit state ---
  let expandedMember = $state<string | null>(null);
  let editingRoleIds = $state<string[]>([]);
  let editingPermissions = $state<string[]>([]);
  let isSavingMember = $state(false);

  function clearMessages() {
    setTimeout(() => {
      successMessage = null;
      errorMessage = null;
    }, 3000);
  }

  function resetInviteForm() {
    inviteLabel = "";
    inviteRoleIds = [];
    inviteDirectPermissions = [];
    showInvitePermissions = false;
    allowMultipleUses = false;
    limitTo24Hours = false;
    showCreateInvite = false;
    createdInviteUrl = null;
    errorMessage = null;
  }

  async function copyInviteUrl() {
    if (createdInviteUrl) {
      await navigator.clipboard.writeText(createdInviteUrl);
      copiedInvite = true;
      setTimeout(() => (copiedInvite = false), 2000);
    }
  }

  async function handleCreateInvite() {
    if (!tenantId) return;
    isCreatingInvite = true;
    errorMessage = null;
    try {
      const result = await createInvite({
        id: tenantId,
        request: {
          roleIds: inviteRoleIds.length > 0 ? inviteRoleIds : undefined,
          directPermissions:
            inviteDirectPermissions.length > 0
              ? inviteDirectPermissions
              : undefined,
          label: inviteLabel || undefined,
          expiresInDays: 7,
          maxUses: allowMultipleUses ? undefined : 1,
          limitTo24Hours,
        },
      });
      if (result.inviteUrl) {
        createdInviteUrl = result.inviteUrl.startsWith("http")
          ? result.inviteUrl
          : `${window.location.origin}${result.inviteUrl}`;
      }
    } catch {
      errorMessage = "Failed to create invite. Please try again.";
    } finally {
      isCreatingInvite = false;
    }
  }

  async function handleRevokeInvite(inviteId: string) {
    if (!tenantId) return;
    isRevokingInvite = inviteId;
    errorMessage = null;
    try {
      await revokeInvite({ id: tenantId, inviteId });
      successMessage = "Invite revoked successfully.";
      clearMessages();
    } catch {
      errorMessage = "Failed to revoke invite. Please try again.";
      clearMessages();
    } finally {
      isRevokingInvite = null;
    }
  }

  async function handleRemoveMember(subjectId: string) {
    if (!tenantId) return;
    isRemovingMember = subjectId;
    errorMessage = null;
    try {
      await removeMember({ id: tenantId, subjectId });
      successMessage = "Member removed successfully.";
      clearMessages();
    } catch {
      errorMessage = "Failed to remove member. Please try again.";
      clearMessages();
    } finally {
      isRemovingMember = null;
    }
  }

  function toggleExpandMember(member: any) {
    const id = member.subjectId;
    if (expandedMember === id) {
      expandedMember = null;
    } else {
      expandedMember = id;
      editingRoleIds = (member.roles ?? []).map((r: any) => r.roleId as string);
      editingPermissions = [...(member.directPermissions ?? [])];
    }
  }

  function toggleRole(roleId: string) {
    if (editingRoleIds.includes(roleId)) {
      editingRoleIds = editingRoleIds.filter((r) => r !== roleId);
    } else {
      editingRoleIds = [...editingRoleIds, roleId];
    }
  }

  function toggleInviteRole(roleId: string) {
    if (inviteRoleIds.includes(roleId)) {
      inviteRoleIds = inviteRoleIds.filter((r) => r !== roleId);
    } else {
      inviteRoleIds = [...inviteRoleIds, roleId];
    }
  }

  async function saveMemberChanges(memberId: string) {
    isSavingMember = true;
    errorMessage = null;
    try {
      await Promise.all([
        setMemberRoles({ memberId, roleIds: editingRoleIds }),
        setMemberPermissions({
          memberId,
          directPermissions: editingPermissions,
        }),
      ]);
      successMessage = "Member updated successfully.";
      expandedMember = null;
      clearMessages();
    } catch {
      errorMessage = "Failed to update member. Please try again.";
      clearMessages();
    } finally {
      isSavingMember = false;
    }
  }

  function getRoleName(roleId: string): string {
    const role = allRoles.find((r) => r.id === roleId);
    return role?.name ?? roleId;
  }
</script>

<svelte:head>
  <title>Members - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto max-w-4xl p-6 space-y-6">
  <div class="space-y-1">
    <h1 class="text-2xl font-bold tracking-tight">Members</h1>
    <p class="text-muted-foreground">
      Manage members, invites, and access to your data
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

  <!-- Invite Section -->
  {#if canInvite}
    <div class="space-y-4">
      <div class="flex items-center justify-between gap-4">
        <h2 class="text-lg font-semibold flex items-center gap-2">
          <UserPlus class="h-5 w-5" />
          Invite Members
        </h2>
        {#if !showCreateInvite}
          <Button
            variant="outline"
            size="sm"
            onclick={() => (showCreateInvite = true)}
          >
            <Link class="mr-1.5 h-3.5 w-3.5" />
            Create Invite Link
          </Button>
        {/if}
      </div>

      {#if showCreateInvite}
        <Card.Root>
          <Card.Header>
            <Card.Title class="text-lg">Create Invite Link</Card.Title>
            <Card.Description>
              Generate a shareable link. Anyone with this link can accept the
              invite after signing in.
            </Card.Description>
          </Card.Header>
          <Card.Content>
            {#if createdInviteUrl}
              <div class="space-y-4">
                <div
                  class="flex items-start gap-3 rounded-md border border-green-200 bg-green-50 p-3 dark:border-green-900/50 dark:bg-green-900/20"
                >
                  <Check
                    class="mt-0.5 h-4 w-4 shrink-0 text-green-600 dark:text-green-400"
                  />
                  <p class="text-sm text-green-800 dark:text-green-200">
                    Invite link created. Share it with the new member.
                  </p>
                </div>

                <div class="flex gap-2">
                  <Input
                    type="text"
                    value={createdInviteUrl}
                    readonly
                    class="font-mono text-sm"
                  />
                  <Button variant="outline" size="icon" onclick={copyInviteUrl}>
                    {#if copiedInvite}
                      <Check class="h-4 w-4 text-green-600" />
                    {:else}
                      <Copy class="h-4 w-4" />
                    {/if}
                  </Button>
                </div>

                <Button
                  variant="outline"
                  class="w-full"
                  onclick={() => resetInviteForm()}
                >
                  Done
                </Button>
              </div>
            {:else}
              <div class="space-y-4">
                <div class="space-y-2">
                  <Label for="invite-label">Label (optional)</Label>
                  <Input
                    id="invite-label"
                    type="text"
                    placeholder="e.g. Mom, Endocrinologist"
                    bind:value={inviteLabel}
                  />
                </div>

                <!-- Role multi-select -->
                <div class="space-y-2">
                  <Label>Roles</Label>
                  <div class="grid gap-2 sm:grid-cols-2">
                    {#each allRoles as role (role.id)}
                      <div class="flex items-center gap-2">
                        <Checkbox
                          id="invite-role-{role.id}"
                          checked={inviteRoleIds.includes(role.id ?? '')}
                          onCheckedChange={() => toggleInviteRole(role.id ?? '')}
                        />
                        <label
                          for="invite-role-{role.id}"
                          class="text-sm text-foreground cursor-pointer select-none"
                        >
                          {role.name}
                        </label>
                      </div>
                    {/each}
                  </div>
                </div>

                <!-- Direct permissions (collapsible) -->
                <Collapsible.Root
                  open={showInvitePermissions}
                  onOpenChange={(open) => (showInvitePermissions = open)}
                >
                  <Collapsible.Trigger class="flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors w-full">
                    {#if showInvitePermissions}
                      <ChevronUp class="h-4 w-4" />
                    {:else}
                      <ChevronDown class="h-4 w-4" />
                    {/if}
                    Direct Permissions (optional)
                  </Collapsible.Trigger>
                  <Collapsible.Content>
                    <div class="mt-3">
                      <PermissionPicker bind:selected={inviteDirectPermissions} />
                    </div>
                  </Collapsible.Content>
                </Collapsible.Root>

                <div
                  class="flex items-start gap-2 rounded-md border p-3 bg-muted/30"
                >
                  <Checkbox
                    id="limit-to-24-hours"
                    checked={limitTo24Hours}
                    onCheckedChange={(checked) => {
                      limitTo24Hours = checked === true;
                    }}
                  />
                  <div class="flex-1">
                    <label
                      for="limit-to-24-hours"
                      class="text-sm font-medium cursor-pointer select-none"
                    >
                      Only last 24 hours
                    </label>
                    <p class="text-xs text-muted-foreground mt-0.5">
                      Restrict access to only the most recent 24 hours of data.
                    </p>
                  </div>
                </div>

                <div
                  class="flex items-start gap-2 rounded-md border p-3 bg-muted/30"
                >
                  <Checkbox
                    id="allow-multiple-uses"
                    checked={allowMultipleUses}
                    onCheckedChange={(checked) => {
                      allowMultipleUses = checked === true;
                    }}
                  />
                  <div class="flex-1">
                    <label
                      for="allow-multiple-uses"
                      class="text-sm font-medium cursor-pointer select-none"
                    >
                      Allow multiple uses
                    </label>
                    <p class="text-xs text-muted-foreground mt-0.5">
                      By default, invite links can only be used once.
                    </p>
                  </div>
                </div>

                <div class="flex gap-3">
                  <Button
                    type="button"
                    variant="outline"
                    class="flex-1"
                    onclick={() => resetInviteForm()}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="button"
                    class="flex-1"
                    disabled={isCreatingInvite ||
                      (inviteRoleIds.length === 0 &&
                        inviteDirectPermissions.length === 0)}
                    onclick={handleCreateInvite}
                  >
                    {#if isCreatingInvite}
                      <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
                    {/if}
                    Create Link
                  </Button>
                </div>
              </div>
            {/if}
          </Card.Content>
        </Card.Root>
      {/if}
    </div>
  {/if}

  <!-- Pending Invites -->
  {#if canInvite && activeInvites.length > 0 && !showCreateInvite}
    <Card.Root>
      <Card.Header class="pb-3">
        <Card.Title class="text-base flex items-center gap-2">
          <Link class="h-4 w-4" />
          Pending Invites
        </Card.Title>
      </Card.Header>
      <Card.Content class="space-y-3">
        {#each activeInvites as invite (invite.id)}
          <div
            class="flex items-center justify-between gap-4 rounded-md border p-3"
          >
            <div class="space-y-1 flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <p class="text-sm font-medium">
                  {invite.label ?? "Invite Link"}
                </p>
                {#if invite.roleIds?.length}
                  {#each invite.roleIds as roleId}
                    <Badge variant="secondary" class="text-xs">
                      {getRoleName(roleId)}
                    </Badge>
                  {/each}
                {/if}
              </div>
              <p class="text-xs text-muted-foreground">
                Expires {formatDate(invite.expiresAt)}
                {#if invite.maxUses}
                  &middot; {invite.useCount}/{invite.maxUses} uses
                {:else}
                  &middot; {invite.useCount}
                  {invite.useCount === 1 ? "use" : "uses"}
                {/if}
                {#if invite.limitTo24Hours}
                  &middot; Last 24 hours only
                {/if}
              </p>
              {#if invite.usedBy && invite.usedBy.length > 0}
                <div class="mt-2 pt-2 border-t space-y-1">
                  <p
                    class="text-xs font-medium text-muted-foreground uppercase tracking-wider"
                  >
                    Used by
                  </p>
                  {#each invite.usedBy as usage}
                    <p class="text-xs text-foreground">
                      <Check class="inline h-3 w-3 mr-1 text-primary" />
                      {usage.name ?? "Unknown"}
                      <span class="text-muted-foreground ml-1">
                        on {formatDate(usage.joinedAt)}
                      </span>
                    </p>
                  {/each}
                </div>
              {/if}
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              class="text-destructive hover:text-destructive shrink-0"
              disabled={isRevokingInvite === invite.id}
              onclick={() => handleRevokeInvite(invite.id!)}
            >
              {#if isRevokingInvite === invite.id}
                <Loader2 class="h-3.5 w-3.5 animate-spin" />
              {:else}
                <Trash2 class="h-3.5 w-3.5" />
              {/if}
            </Button>
          </div>
        {/each}
      </Card.Content>
    </Card.Root>
  {/if}

  <!-- Active Members -->
  {#if canManageMembers}
    <div class="space-y-4">
      <h2 class="text-lg font-semibold flex items-center gap-2">
        <Users class="h-5 w-5" />
        Active Members
      </h2>

      {#if allMembers.length === 0}
        <Card.Root>
          <Card.Content
            class="flex flex-col items-center justify-center py-12 text-center"
          >
            <div
              class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-muted"
            >
              <Users class="h-6 w-6 text-muted-foreground" />
            </div>
            <p class="text-sm text-muted-foreground max-w-sm">
              No members. Invite someone to share your data.
            </p>
          </Card.Content>
        </Card.Root>
      {:else}
        {#each allMembers as member (member.subjectId)}
          <Card.Root>
            <Card.Header>
              <div class="flex items-start justify-between gap-4">
                <div class="space-y-1 flex-1 min-w-0">
                  <Card.Title class="flex items-center gap-2 flex-wrap">
                    <span class="truncate">
                      {member.name ?? "Unknown"}
                    </span>
                    {#each member.roles ?? [] as role}
                      <Badge variant="secondary" class="text-xs">
                        {role.name ?? role.slug ?? "Unknown"}
                      </Badge>
                    {/each}
                    {#if member.directPermissions?.length}
                      <Badge variant="outline" class="text-xs">
                        {member.directPermissions.length} direct permission{member
                          .directPermissions.length !== 1
                          ? "s"
                          : ""}
                      </Badge>
                    {/if}
                  </Card.Title>
                  {#if member.label}
                    <Card.Description>{member.label}</Card.Description>
                  {/if}
                </div>
                <div class="flex items-center gap-2 shrink-0">
                  {#if canEditMemberRoles}
                    <Button
                      variant="outline"
                      size="sm"
                      onclick={() => toggleExpandMember(member)}
                    >
                      <Settings2 class="mr-1.5 h-3.5 w-3.5" />
                      {expandedMember === member.subjectId ? "Close" : "Edit"}
                    </Button>
                  {/if}
                  <AlertDialog.Root>
                    <AlertDialog.Trigger>
                      {#snippet child({ props })}
                        <Button
                          {...props}
                          variant="outline"
                          size="sm"
                          class="text-destructive border-destructive/30 hover:bg-destructive/10"
                          disabled={isRemovingMember === member.subjectId}
                        >
                          {#if isRemovingMember === member.subjectId}
                            <Loader2 class="h-3.5 w-3.5 animate-spin" />
                          {:else}
                            <Trash2 class="h-3.5 w-3.5" />
                          {/if}
                        </Button>
                      {/snippet}
                    </AlertDialog.Trigger>
                    <AlertDialog.Content>
                      <AlertDialog.Header>
                        <AlertDialog.Title>Remove member</AlertDialog.Title>
                        <AlertDialog.Description>
                          Remove {member.name ?? "this member"} from the tenant?
                          They will lose access to all tenant data.
                        </AlertDialog.Description>
                      </AlertDialog.Header>
                      <AlertDialog.Footer>
                        <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                        <AlertDialog.Action
                          onclick={() =>
                            handleRemoveMember(member.subjectId!)}
                        >
                          Remove
                        </AlertDialog.Action>
                      </AlertDialog.Footer>
                    </AlertDialog.Content>
                  </AlertDialog.Root>
                </div>
              </div>
            </Card.Header>

            {#if expandedMember === member.subjectId && canEditMemberRoles}
              <Card.Content class="space-y-4 border-t pt-4">
                <!-- Role selection -->
                <div class="space-y-2">
                  <Label>Roles</Label>
                  <div class="grid gap-2 sm:grid-cols-2">
                    {#each allRoles as role (role.id)}
                      <div class="flex items-center gap-2">
                        <Checkbox
                          id="member-role-{member.subjectId}-{role.id}"
                          checked={editingRoleIds.includes(role.id ?? '')}
                          onCheckedChange={() => toggleRole(role.id ?? '')}
                        />
                        <label
                          for="member-role-{member.subjectId}-{role.id}"
                          class="text-sm text-foreground cursor-pointer select-none"
                        >
                          {role.name}
                        </label>
                      </div>
                    {/each}
                  </div>
                </div>

                <Separator />

                <!-- Direct permissions -->
                <div class="space-y-2">
                  <Label>Direct Permissions</Label>
                  <PermissionPicker bind:selected={editingPermissions} />
                </div>

                <div class="flex gap-3">
                  <Button
                    variant="outline"
                    class="flex-1"
                    onclick={() => (expandedMember = null)}
                  >
                    Cancel
                  </Button>
                  <Button
                    class="flex-1"
                    disabled={isSavingMember}
                    onclick={() => saveMemberChanges(member.subjectId!)}
                  >
                    {#if isSavingMember}
                      <Loader2 class="mr-1.5 h-4 w-4 animate-spin" />
                    {/if}
                    Save Changes
                  </Button>
                </div>
              </Card.Content>
            {:else}
              <Card.Content>
                <div
                  class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground"
                >
                  {#if member.limitTo24Hours}
                    <span
                      class="flex items-center gap-1.5 text-amber-600 dark:text-amber-400"
                    >
                      <Clock class="h-3 w-3" />
                      24-hour limit
                    </span>
                  {/if}
                  <span class="flex items-center gap-1.5">
                    <Clock class="h-3 w-3" />
                    Joined {formatDate(member.sysCreatedAt)}
                  </span>
                  {#if member.lastUsedAt}
                    <span class="flex items-center gap-1.5">
                      <Clock class="h-3 w-3" />
                      Last active {formatDate(member.lastUsedAt)}
                    </span>
                  {/if}
                </div>
              </Card.Content>
            {/if}
          </Card.Root>
        {/each}
      {/if}
    </div>
  {/if}

  {#if !canInvite && !canManageMembers}
    <Card.Root>
      <Card.Content
        class="flex flex-col items-center justify-center py-12 text-center"
      >
        <div
          class="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-destructive/10"
        >
          <ShieldAlert class="h-6 w-6 text-destructive" />
        </div>
        <h2 class="text-lg font-semibold">Access Denied</h2>
        <p class="text-sm text-muted-foreground max-w-sm mt-2">
          You do not have permission to manage members. Contact your tenant
          administrator for access.
        </p>
      </Card.Content>
    </Card.Root>
  {/if}
</div>
