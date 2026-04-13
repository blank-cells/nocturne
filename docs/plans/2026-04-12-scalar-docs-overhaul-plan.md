# Scalar Documentation Overhaul — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split the single OpenAPI document into Nocturne (V4) and Nightscout (V1-V3), reorganise V4 controllers into semantic subfolders with convention-based tag derivation, and add XML documentation via `<inheritdoc>` from service interfaces.

**Architecture:** Two NSwag documents filtered by API group. Custom operation processor derives tags from controller namespace. XML docs live on service interfaces; controllers use `<inheritdoc cref>`.

**Tech Stack:** NSwag, Scalar.AspNetCore, ASP.NET Core OpenAPI, XML documentation

---

### Task 1: Convention-Based Tag Operation Processor

**Files:**
- Create: `src/API/Nocturne.API/OpenApi/FolderBasedTagOperationProcessor.cs`

Write an NSwag `IOperationProcessor` that:
1. Checks for explicit `[Tags]` attribute — if present, use it and return
2. Reads the controller's namespace
3. For `Controllers.V4.<Group>` — tag is the `<Group>` segment (e.g., "Glucose")
4. For `Controllers.V4` (root, no subfolder) — tag is controller name minus "Controller" suffix
5. For `Controllers.V1` / `V2` / `V3` — tag is "V1" / "V2" / "V3"
6. For `Controllers.Authentication` — handled by explicit `[Tags]`, processor won't reach here
7. Removes any existing tags and replaces with the derived one

**Commit:** `feat: add convention-based tag operation processor`

---

### Task 2: Split OpenAPI Into Two Documents

**Files:**
- Modify: `src/API/Nocturne.API/Program.cs`

Replace the single `AddOpenApiDocument` with two:

**Nocturne document** (`nocturne`):
- Filters to controllers in `V4` namespace + root-level controllers (Authentication, WellKnown)
- Uses `FolderBasedTagOperationProcessor`
- Title: "Nocturne API", version: "0.0.1"
- Contact: Discord link
- License: AGPL-3.0-or-later

**Nightscout document** (`nightscout`):
- Filters to controllers in `V1`, `V2`, `V3` namespaces
- Uses `FolderBasedTagOperationProcessor`
- Title: "Nightscout API"
- Contact: Discord link
- License: AGPL-3.0-or-later

Register security schemes from existing auth configuration.

**Commit:** `feat: split OpenAPI into Nocturne and Nightscout documents`

---

### Task 3: Update Scalar Configuration

**Files:**
- Modify: `src/API/Nocturne.API/Program.cs` (Scalar section)

Update `MapScalarApiReference` to:
- Add both documents with `nocturne` as default
- Sort tags alphabetically
- Collapse all tags by default

**Commit:** `feat: update Scalar config for dual documents`

---

### Task 4: Update nswag.json

**Files:**
- Modify: `src/API/Nocturne.API/nswag.json`

- Change document name from `"v1"` to `"nocturne"`
- Change apiVersion to `"0.0.1"`

**Commit:** `chore: update nswag.json for nocturne document`

---

### Task 5: Hide MetadataController

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/MetadataController.cs`

Add `[ApiExplorerSettings(IgnoreApi = true)]` to the class.

**Commit:** `chore: hide MetadataController from API docs`

---

### Task 6: Document Base Controllers

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V4/Base/V4ReadOnlyControllerBase.cs`
- Modify: `src/API/Nocturne.API/Controllers/V4/Base/V4CrudControllerBase.cs`

Add XML doc `<summary>`, `<param>`, and `<remarks>` to all virtual methods:
- `GetAll` — describe pagination, filtering, sorting
- `GetById` — describe ID lookup
- `Create` — describe creation with timestamp requirement
- `Update` — describe update by ID
- `Delete` — describe deletion by ID

**Commit:** `docs: add XML documentation to base controllers`

---

### Task 7: Move V4 Controllers — Glucose Group

**Model:** haiku (simple file moves + namespace updates)

Move to `Controllers/V4/Glucose/`:
- `SensorGlucoseController.cs`
- `MeterGlucoseController.cs`
- `EntriesController.cs`
- `CalibrationController.cs`
- `BGCheckController.cs`

For each: update namespace, remove `[Tags]` attribute.

**Commit:** `refactor: move glucose controllers to V4/Glucose/`

---

### Task 8: Move V4 Controllers — Treatments Group

Move to `Controllers/V4/Treatments/`:
- `TreatmentsController.cs`
- `BolusController.cs`
- `BolusCalculationController.cs`
- `NutritionController.cs`
- `FoodsController.cs`
- `ConnectorFoodEntriesController.cs`
- `MealMatchingController.cs`
- `NoteController.cs`

**Commit:** `refactor: move treatment controllers to V4/Treatments/`

---

### Task 9: Move V4 Controllers — Monitoring Group

Move to `Controllers/V4/Monitoring/`:
- `AlertsController.cs`
- `AlertRulesController.cs`
- `AlertInvitesController.cs`
- `AlertCustomSoundsController.cs`
- `TrackerAlertsController.cs`
- `TrackersController.cs`
- `NotificationsController.cs`

**Commit:** `refactor: move monitoring controllers to V4/Monitoring/`

---

### Task 10: Move V4 Controllers — Devices Group

Move to `Controllers/V4/Devices/`:
- `DeviceAgeController.cs`
- `DeviceEventController.cs`
- `BatteryController.cs`
- `PumpSnapshotController.cs`
- `UploaderSnapshotController.cs`
- `ApsSnapshotController.cs`

**Commit:** `refactor: move device controllers to V4/Devices/`

---

### Task 11: Move V4 Controllers — Analytics Group

Move to `Controllers/V4/Analytics/`:
- `ChartDataController.cs`
- `AnalyticsController.cs`
- `SummaryController.cs`
- `DataOverviewController.cs`
- `CorrelationController.cs`
- `PredictionController.cs`
- `RetrospectiveController.cs`
- `StateSpansController.cs`

**Commit:** `refactor: move analytics controllers to V4/Analytics/`

---

### Task 12: Move V4 Controllers — Health Group

Move to `Controllers/V4/Health/`:
- `HeartRateController.cs`
- `StepCountController.cs`
- `BodyWeightController.cs`
- `PatientRecordController.cs`

**Commit:** `refactor: move health controllers to V4/Health/`

---

### Task 13: Move V4 Controllers — Profiles Group

Move to `Controllers/V4/Profiles/`:
- `ProfileController.cs`
- `UISettingsController.cs`
- `UserPreferencesController.cs`
- `MyFitnessPalSettingsController.cs`
- `ClockFacesController.cs`

**Commit:** `refactor: move profile controllers to V4/Profiles/`

---

### Task 14: Move V4 Controllers — Identity Group

Move to `Controllers/V4/Identity/`:
- `MyPermissionsController.cs`
- `MyTenantsController.cs`
- `RoleController.cs`
- `MemberInviteController.cs`
- `ConnectedAppsController.cs`
- `ChatIdentityController.cs`
- `ChatIdentityDirectoryController.cs`

**Commit:** `refactor: move identity controllers to V4/Identity/`

---

### Task 15: Move V4 Controllers — Connectors Group

Move to `Controllers/V4/Connectors/` (folder already exists with HomeAssistantWebhookController):
- `WebhookSettingsController.cs` (from `V4/Notifications/`)
- `ConfigurationController.cs` (from `Internal/`)

For ConfigurationController: update namespace AND route from `internal/config` to `api/v4/connectors/config`.

Delete empty folders: `V4/Notifications/`, `Internal/`.

**Commit:** `refactor: consolidate connector controllers to V4/Connectors/`

---

### Task 16: Move V4 Controllers — System Group

Move to `Controllers/V4/System/`:
- `StatusController.cs`
- `SystemController.cs`
- `SystemEventsController.cs`
- `ServicesController.cs`
- `CompatibilityController.cs`
- `DebugController.cs`
- `ApiSecretController.cs`

**Commit:** `refactor: move system controllers to V4/System/`

---

### Task 17: Rename V4/Admin → V4/TenantAdmin + Move Root Admin → V4/PlatformAdmin

Rename `Controllers/V4/Admin/` to `Controllers/V4/TenantAdmin/`. Move remaining admin controllers:
- `DeduplicationController.cs`, `MigrationController.cs`, `NightscoutTransitionController.cs`, `ProcessingController.cs`, `CompressionLowController.cs`, `BackfillController.cs`, `DiscrepancyController.cs` → `V4/TenantAdmin/`

Move `Controllers/Admin/AccessRequestController.cs` and `Controllers/Admin/TenantController.cs` → `V4/PlatformAdmin/`. Delete root `Admin/` folder.

**Commit:** `refactor: split admin controllers into TenantAdmin and PlatformAdmin`

---

### Task 18: Move Root Auth Controllers → Authentication/

Move to `Controllers/Authentication/`:
- `OAuthController.cs`
- `OidcController.cs`
- `PasskeyController.cs`
- `TotpController.cs`
- `DirectGrantController.cs`

Each keeps explicit `[Tags("Authentication")]`.

**Commit:** `refactor: move auth controllers to Authentication/`

---

### Task 19: Update Nightscout Controller Tags

Remove individual `[Tags]` from all V1, V2, V3 controllers. The convention processor will assign "V1", "V2", "V3" tags automatically.

**Commit:** `refactor: remove explicit tags from Nightscout controllers`

---

### Task 20: Add XML Docs — Simple Wrapper Controllers (haiku)

For each simple wrapper controller, add `<inheritdoc cref="IServiceInterface.Method"/>` to action methods. Controllers:
- ApsSnapshotController, BGCheckController, BolusCalculationController, BolusController, CalibrationController, DeviceEventController, MeterGlucoseController, NoteController, PumpSnapshotController, SensorGlucoseController, UploaderSnapshotController
- WellKnownController, ConfigurationController (Internal→Connectors)

**Commit:** `docs: add inheritdoc to simple wrapper controllers`

---

### Task 21: Add XML Docs — Complex Controllers (sonnet, batched)

For each complex controller, read the service interface and controller logic, then:
1. Add `/// <summary>` and `/// <remarks>` to service interface methods (if missing)
2. Add `<inheritdoc cref>` on the controller action methods
3. For methods with no service interface (direct DB access, multi-service orchestration), write docs directly on the controller

This is the largest task and should be batched into subagent groups by folder.

**Commit:** One commit per folder group (e.g., `docs: add XML docs to monitoring controllers`)

---

### Task 22: Build Verification

Run `dotnet build` to verify:
- No namespace resolution errors
- NSwag generates both documents
- No compilation errors from moved files

Run `dotnet test --filter "Category!=Integration&Category!=Performance"` to verify no test breakage.

**Commit:** (none, verification only)
