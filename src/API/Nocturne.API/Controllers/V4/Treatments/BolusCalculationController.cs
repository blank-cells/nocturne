using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Treatments;

[ApiController]
[Route("api/v4/insulin/calculations")]
[Authorize]
[Produces("application/json")]
public class BolusCalculationController(IBolusCalculationRepository repo)
    : V4CrudControllerBase<BolusCalculation, UpsertBolusCalculationRequest, UpsertBolusCalculationRequest, IBolusCalculationRepository>(repo)
{
    protected override BolusCalculation MapCreateToModel(UpsertBolusCalculationRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        BloodGlucoseInput = request.BloodGlucoseInput,
        BloodGlucoseInputSource = request.BloodGlucoseInputSource,
        CarbInput = request.CarbInput,
        InsulinOnBoard = request.InsulinOnBoard,
        InsulinRecommendation = request.InsulinRecommendation,
        CarbRatio = request.CarbRatio,
        CalculationType = request.CalculationType,
        InsulinRecommendationForCarbs = request.InsulinRecommendationForCarbs,
        InsulinProgrammed = request.InsulinProgrammed,
        EnteredInsulin = request.EnteredInsulin,
        SplitNow = request.SplitNow,
        SplitExt = request.SplitExt,
        PreBolus = request.PreBolus,
    };

    protected override BolusCalculation MapUpdateToModel(Guid id, UpsertBolusCalculationRequest request, BolusCalculation existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        BloodGlucoseInput = request.BloodGlucoseInput,
        BloodGlucoseInputSource = request.BloodGlucoseInputSource,
        CarbInput = request.CarbInput,
        InsulinOnBoard = request.InsulinOnBoard,
        InsulinRecommendation = request.InsulinRecommendation,
        CarbRatio = request.CarbRatio,
        CalculationType = request.CalculationType,
        InsulinRecommendationForCarbs = request.InsulinRecommendationForCarbs,
        InsulinProgrammed = request.InsulinProgrammed,
        EnteredInsulin = request.EnteredInsulin,
        SplitNow = request.SplitNow,
        SplitExt = request.SplitExt,
        PreBolus = request.PreBolus,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        AdditionalProperties = existing.AdditionalProperties,
    };
}
