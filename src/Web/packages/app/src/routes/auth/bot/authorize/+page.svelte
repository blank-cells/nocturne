<script lang="ts">
	import type { PageData, ActionData } from "./$types";

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const returnUrl = $derived(
		data.mode === "confirm" ? `/auth/bot/authorize?state=${data.stateToken}` : "/",
	);
</script>

{#if form?.success}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4 p-6 text-center">
		<h1 class="text-2xl font-bold">Connected</h1>
		<p class="text-muted-foreground max-w-md">
			Your chat account is now linked to Nocturne. You can close this tab and head back to your chat app.
		</p>
	</div>
{:else if data.mode === "error"}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4 p-6 text-center">
		<h1 class="text-2xl font-bold">Can't Complete This Link</h1>
		<p class="text-destructive max-w-md">{data.message}</p>
	</div>
{:else if data.mode === "slug-prompt"}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4 p-6">
		<h1 class="text-2xl font-bold">Which Nocturne Instance?</h1>
		<p class="text-muted-foreground max-w-md text-center">
			Enter the slug of the Nocturne instance you'd like to link your chat account to.
		</p>
		{#if form?.error}
			<p class="text-destructive">{form.error}</p>
		{/if}
		<form method="POST" action="?/pick-tenant" class="flex flex-col gap-3 w-full max-w-sm">
			<input type="hidden" name="state" value={data.stateToken} />
			<label class="flex flex-col gap-1">
				<span class="text-sm font-medium">Instance slug</span>
				<input
					type="text"
					name="slug"
					required
					pattern="[a-z0-9][a-z0-9\-]{'{0,62}'}[a-z0-9]?"
					placeholder="e.g. myfamily"
					class="px-3 py-2 border rounded-md bg-background"
				/>
			</label>
			<button
				type="submit"
				class="px-6 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
			>
				Continue
			</button>
		</form>
	</div>
{:else if data.mode === "confirm"}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4 p-6">
		<h1 class="text-2xl font-bold">Connect Chat Account</h1>
		{#if !data.isAuthenticated}
			<p class="text-muted-foreground max-w-md text-center">
				Sign in to your Nocturne account to finish connecting your chat account.
			</p>
			<a
				href="/auth/login?returnUrl={encodeURIComponent(returnUrl)}"
				class="px-6 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
			>
				Sign in
			</a>
		{:else}
			<p class="text-muted-foreground max-w-md text-center">
				Allow the Nocturne bot to access glucose data on your behalf and send alerts to your
				<strong>{data.pendingPlatform}</strong> account?
			</p>
			{#if form?.error}
				<p class="text-destructive">{form.error}</p>
			{/if}
			<form method="POST" action="?/claim">
				<input type="hidden" name="state" value={data.stateToken} />
				<button
					type="submit"
					class="px-6 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
				>
					Connect
				</button>
			</form>
		{/if}
	</div>
{/if}
