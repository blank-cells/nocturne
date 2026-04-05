import * as patientRemote from "$api/generated/patientRecords.generated.remote";
import { getCatalog as getInsulinCatalog } from "$api/generated/insulins.generated.remote";
import {
  type PatientDevice,
  type PatientInsulin,
  type InsulinFormulation,
  DiabetesType,
} from "$api";

/** Creates reactive clinical form state bound to the patient record API */
export function createClinicalState() {
  const record = patientRemote.getPatientRecord();

  let diabetesType = $state<string>("");
  let diabetesTypeOther = $state("");
  let diagnosisDate = $state("");
  let dateOfBirth = $state("");
  let preferredName = $state("");
  let pronouns = $state("");
  let saving = $state(false);
  let saveError = $state<string | null>(null);

  // Pre-populate from existing record
  $effect(() => {
    const r = record.current;
    if (r) {
      diabetesType = r.diabetesType ?? "";
      diabetesTypeOther = r.diabetesTypeOther ?? "";
      diagnosisDate = r.diagnosisDate
        ? new Date(r.diagnosisDate).toISOString().split("T")[0]
        : "";
      dateOfBirth = r.dateOfBirth
        ? new Date(r.dateOfBirth).toISOString().split("T")[0]
        : "";
      preferredName = r.preferredName ?? "";
      pronouns = r.pronouns ?? "";
    }
  });

  async function save(): Promise<boolean> {
    saving = true;
    saveError = null;
    try {
      const current = record.current;
      await patientRemote.updatePatientRecord({
        id: current?.id,
        avatarUrl: current?.avatarUrl,
        createdAt: current?.createdAt instanceof Date ? current.createdAt.toISOString() : current?.createdAt,
        modifiedAt: current?.modifiedAt instanceof Date ? current.modifiedAt.toISOString() : current?.modifiedAt,
        diabetesType: (diabetesType as DiabetesType) || undefined,
        diabetesTypeOther:
          diabetesType === DiabetesType.Other
            ? diabetesTypeOther
            : undefined,
        diagnosisDate: diagnosisDate || undefined,
        dateOfBirth: dateOfBirth || undefined,
        preferredName: preferredName || undefined,
        pronouns: pronouns || undefined,
      });
      return true;
    } catch {
      saveError = "Something went wrong. Please try again.";
      return false;
    } finally {
      saving = false;
    }
  }

  return {
    get diabetesType() { return diabetesType; },
    set diabetesType(v: string) { diabetesType = v; },
    get diabetesTypeOther() { return diabetesTypeOther; },
    set diabetesTypeOther(v: string) { diabetesTypeOther = v; },
    get diagnosisDate() { return diagnosisDate; },
    set diagnosisDate(v: string) { diagnosisDate = v; },
    get dateOfBirth() { return dateOfBirth; },
    set dateOfBirth(v: string) { dateOfBirth = v; },
    get preferredName() { return preferredName; },
    set preferredName(v: string) { preferredName = v; },
    get pronouns() { return pronouns; },
    set pronouns(v: string) { pronouns = v; },
    get saving() { return saving; },
    get saveError() { return saveError; },
    get isValid() { return !!diabetesType; },
    save,
  };
}

/** Creates reactive device list state with CRUD */
export function createDeviceListState() {
  const devices = patientRemote.getDevices();
  const createForm = patientRemote.createDevice;
  const updateForm = patientRemote.updateDevice;

  async function remove(id: string): Promise<void> {
    await patientRemote.deleteDevice(id);
  }

  return {
    get items(): PatientDevice[] { return (devices.current ?? []) as PatientDevice[]; },
    get createForm() { return createForm; },
    get updateForm() { return updateForm; },
    remove,
  };
}

/** Creates reactive insulin list state with CRUD and catalog */
export function createInsulinListState() {
  const insulins = patientRemote.getInsulins();
  const catalog = getInsulinCatalog(undefined);
  const createForm = patientRemote.createInsulin;
  const updateForm = patientRemote.updateInsulin;

  async function remove(id: string): Promise<void> {
    await patientRemote.deleteInsulin(id);
  }

  return {
    get items(): PatientInsulin[] { return (insulins.current ?? []) as PatientInsulin[]; },
    get catalog(): InsulinFormulation[] { return (catalog.current ?? []) as InsulinFormulation[]; },
    get createForm() { return createForm; },
    get updateForm() { return updateForm; },
    remove,
  };
}
