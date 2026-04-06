<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import ConnectorSetup from "$lib/components/connectors/ConnectorSetup.svelte";
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
</script>

<svelte:head>
  <title>Connect Data Sources - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Connect Data Sources"
  description="Set up a server-side connector to start pulling data into Nocturne. You can always add more connectors later."
  currentStep={7}
  totalSteps={8}
  prevHref="/settings/setup/uploaders"
  nextHref="/settings/setup/profile"
  showSkip={true}
  saveDisabled={false}
  onSave={async () => true}
>
  <ConnectorSetup>
    {#snippet resultActions({ reset })}
      <Button variant="outline" onclick={reset}>Add Another</Button>
    {/snippet}
  </ConnectorSetup>
</WizardShell>
