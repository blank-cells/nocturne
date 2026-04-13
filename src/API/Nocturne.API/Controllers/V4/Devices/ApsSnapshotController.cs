using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Models.V4;
using Nocturne.Core.Contracts.V4.Repositories;

namespace Nocturne.API.Controllers.V4.Devices;

/// <summary>
/// Controller for managing APS snapshot data
/// </summary>
[ApiController]
[Route("api/v4/device-status/aps")]
[Authorize]
[Produces("application/json")]
public class ApsSnapshotController(IApsSnapshotRepository repo)
    : V4ReadOnlyControllerBase<ApsSnapshot, IApsSnapshotRepository>(repo);
