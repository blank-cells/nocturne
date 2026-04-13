using FluentValidation;
using Nocturne.API.Controllers.V4.PlatformAdmin;

namespace Nocturne.API.Validators.Admin;

public class ApproveAccessRequestRequestValidator : AbstractValidator<ApproveAccessRequestRequest>
{
    public ApproveAccessRequestRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.RoleIds.Count > 0 || (x.DirectPermissions != null && x.DirectPermissions.Count > 0))
            .WithMessage("At least one role or direct permission is required.");
    }
}
