# Scalar Documentation Overhaul

## Problem

The Scalar API documentation is minimally configured: a single OpenAPI document containing all 115 controllers across V1–V4, no security schemes, placeholder license text, and flat tag-based grouping with redundant "V4" prefixes. Consumers see a wall of endpoints with no version separation or semantic organisation.

## Two OpenAPI Documents

Split the single document into two:

- **Nocturne API** (default, document name `nocturne`) — all V4 endpoints, root-level auth controllers, platform admin, and discovery. Version `0.0.1`.
- **Nightscout API** (document name `nightscout`) — all V1, V2, V3 endpoints combined. Three tags: "V1", "V2", "V3", sorted ascending.

Scalar renders a document selector dropdown. The NSwag → Zod → remote codegen pipeline consumes the `nocturne` document only. The `nightscout` document exists purely for Scalar documentation.

`nswag.json` updates to reference document name `nocturne` with `apiVersion: 0.0.1`.

## Controller Folder Reorganisation

V4 controllers move into semantic subfolders. The folder structure becomes the single source of truth for tag grouping via a convention-based NSwag operation processor.

```
Controllers/
├── Authentication/                    [Tags("Authentication")] explicit
│   ├── OAuthController.cs
│   ├── OidcController.cs
│   ├── PasskeyController.cs
│   ├── TotpController.cs
│   └── DirectGrantController.cs
├── WellKnownController.cs            [Tags("Discovery")] explicit
├── MetadataController.cs             [ApiExplorerSettings(IgnoreApi = true)]
├── V1/                               Tag: "V1" (all controllers share one tag)
├── V2/                               Tag: "V2"
├── V3/                               Tag: "V3"
└── V4/
    ├── Glucose/
    │   ├── SensorGlucoseController.cs
    │   ├── MeterGlucoseController.cs
    │   ├── EntriesController.cs
    │   ├── CalibrationController.cs
    │   └── BGCheckController.cs
    ├── Treatments/
    │   ├── TreatmentsController.cs
    │   ├── BolusController.cs
    │   ├── BolusCalculationController.cs
    │   ├── NutritionController.cs
    │   ├── FoodsController.cs
    │   ├── ConnectorFoodEntriesController.cs
    │   ├── MealMatchingController.cs
    │   └── NoteController.cs
    ├── Monitoring/
    │   ├── AlertsController.cs
    │   ├── AlertRulesController.cs
    │   ├── AlertInvitesController.cs
    │   ├── AlertCustomSoundsController.cs
    │   ├── TrackerAlertsController.cs
    │   ├── TrackersController.cs
    │   └── NotificationsController.cs
    ├── Devices/
    │   ├── DeviceAgeController.cs
    │   ├── DeviceEventController.cs
    │   ├── BatteryController.cs
    │   ├── PumpSnapshotController.cs
    │   ├── UploaderSnapshotController.cs
    │   └── ApsSnapshotController.cs
    ├── Analytics/
    │   ├── ChartDataController.cs
    │   ├── AnalyticsController.cs
    │   ├── SummaryController.cs
    │   ├── DataOverviewController.cs
    │   ├── CorrelationController.cs
    │   ├── PredictionController.cs
    │   ├── RetrospectiveController.cs
    │   └── StateSpansController.cs
    ├── Health/
    │   ├── HeartRateController.cs
    │   ├── StepCountController.cs
    │   ├── BodyWeightController.cs
    │   └── PatientRecordController.cs
    ├── Profiles/
    │   ├── ProfileController.cs
    │   ├── UISettingsController.cs
    │   ├── UserPreferencesController.cs
    │   ├── MyFitnessPalSettingsController.cs
    │   └── ClockFacesController.cs
    ├── Identity/
    │   ├── MyPermissionsController.cs
    │   ├── MyTenantsController.cs
    │   ├── RoleController.cs
    │   ├── MemberInviteController.cs
    │   ├── ConnectedAppsController.cs
    │   ├── ChatIdentityController.cs
    │   └── ChatIdentityDirectoryController.cs
    ├── Connectors/
    │   ├── HomeAssistantWebhookController.cs
    │   ├── WebhookSettingsController.cs
    │   └── ConfigurationController.cs  (moved from Internal/, route → api/v4/connectors/config)
    ├── System/
    │   ├── StatusController.cs
    │   ├── SystemController.cs
    │   ├── SystemEventsController.cs
    │   ├── ServicesController.cs
    │   ├── CompatibilityController.cs
    │   ├── DebugController.cs
    │   └── ApiSecretController.cs
    ├── TenantAdmin/                   (renamed from Admin/)
    │   ├── OidcProviderAdminController.cs
    │   ├── SubjectAdminController.cs
    │   ├── DeduplicationController.cs
    │   ├── MigrationController.cs
    │   ├── NightscoutTransitionController.cs
    │   ├── ProcessingController.cs
    │   ├── CompressionLowController.cs
    │   ├── BackfillController.cs
    │   └── DiscrepancyController.cs
    ├── PlatformAdmin/                 (moved from root Admin/)
    │   ├── AccessRequestController.cs
    │   └── TenantController.cs
    └── Base/                          (unchanged, abstract base classes, not endpoints)
        ├── V4CrudControllerBase.cs
        └── V4ReadOnlyControllerBase.cs
```

Deleted folders: `Internal/`, `V4/Notifications/`, root `Admin/`.

## Convention-Based Tag Derivation

A custom NSwag operation processor derives tags from controller namespaces:

1. For V4 controllers: extract the segment after `V4` — e.g., `Controllers.V4.Glucose` → tag **"Glucose"**.
2. For V1/V2/V3 controllers: use the version segment — e.g., `Controllers.V1` → tag **"V1"**.
3. `Base/` is excluded (abstract classes with no routable actions — NSwag ignores them naturally).
4. Root-level controllers (`Authentication/`, `WellKnownController`) use explicit `[Tags]` as an opt-out override.
5. The processor checks for an explicit `[Tags]` attribute first; if present, it takes precedence over convention.

Existing `[Tags]` attributes on V4 controllers are removed. The `[Tags]` attribute becomes an opt-out override used only for root-level controllers that don't follow the namespace convention.

## XML Documentation Strategy

### Convention: document service interfaces, controllers inherit

The vast majority of controllers are thin wrappers around service methods. Documentation lives on service interfaces using `/// <summary>` and `/// <remarks>` (GitHub-Flavored Markdown supported in remarks):

```csharp
// Service interface (source of truth)
public interface IEntryService
{
    /// <summary>
    /// Retrieves glucose entries within the specified time range.
    /// </summary>
    /// <remarks>
    /// Returns entries ordered by timestamp descending. Use `from` and `to`
    /// to filter by date range, or omit for the most recent entries.
    /// </remarks>
    Task<List<Entry>> GetAllAsync(long? from, long? to, CancellationToken ct);
}

// Controller (thin wrapper)
/// <inheritdoc cref="IEntryService.GetAllAsync"/>
[HttpGet]
public async Task<ActionResult<List<Entry>>> GetAll(long? from, long? to, CancellationToken ct)
    => Ok(await _entryService.GetAllAsync(from, to, ct));
```

### Base controller actions

`V4CrudControllerBase` and `V4ReadOnlyControllerBase` carry generic documentation directly on their `virtual` methods (GetAll, GetById, Create, Update, Delete). Concrete controllers that don't override these actions inherit the docs automatically. Controllers that override can use `<inheritdoc/>` or `<inheritdoc cref="IService.Method"/>`.

### CS1591 warning

Remains suppressed. Documentation is added incrementally as controllers are reorganised, not enforced globally.

## OpenAPI Spec Metadata

Both documents share:

- **Contact**: `https://discord.gg/H75V4rMwp4`
- **License**: AGPL-3.0-or-later with link to license text

Nocturne doc:

- **Title**: "Nocturne API"
- **Description**: what Nocturne is, link to GitHub, link to tutorials (when ready)

Nightscout doc:

- **Title**: "Nightscout API"
- **Description**: legacy compatibility layer, link to original Nightscout docs

Security schemes are defined properly in the spec from the existing auth configuration so Scalar renders auth requirements per-endpoint and the "Try it" panel works.

## Scalar Configuration

```csharp
app.MapScalarApiReference(options =>
{
    options.WithTitle("Nocturne API Documentation");
    options.WithTheme(ScalarTheme.Mars);
    options.AddDocument("nocturne", "Nocturne API", "/openapi/nocturne.json", isDefault: true);
    options.AddDocument("nightscout", "Nightscout API", "/openapi/nightscout.json");
    options.SortTagsAlphabetically();
    options.WithDefaultOpenAllTags(false);
});
```

- Mars theme (dark, fits brand)
- Modern layout (single column)
- Tags sorted alphabetically, collapsed by default
- Nocturne as default document

## MetadataController

Hidden from both documents with `[ApiExplorerSettings(IgnoreApi = true)]`. Exists only for NSwag type exposure, not a real API surface.
