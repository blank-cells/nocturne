<script lang="ts">
  import WizardShell from "$lib/components/setup/WizardShell.svelte";
  import { PatientClinicalForm } from "$lib/components/patient";

  let saving = $state(false);
  let saveDisabled = $state(true);
  let saveFn = $state<(() => Promise<boolean>) | undefined>(undefined);

  function handleState(api: { save: () => Promise<boolean>; saving: boolean; isValid: boolean }) {
    saving = api.saving;
    saveDisabled = !api.isValid;
    saveFn = api.save;
  }

  async function handleSave(): Promise<boolean> {
    return saveFn ? await saveFn() : false;
  }
</script>

<svelte:head>
  <title>Patient Record - Setup - Nocturne</title>
</svelte:head>

<WizardShell
  title="Patient Record"
  description="Tell us a bit about yourself and your diabetes. Only diabetes type is required."
  currentStep={3}
  totalSteps={8}
  prevHref="/settings/setup/permissions"
  nextHref="/settings/setup/devices"
  {saveDisabled}
  {saving}
  onSave={handleSave}
>
  <PatientClinicalForm extended={false} onstate={handleState} />
</WizardShell>
