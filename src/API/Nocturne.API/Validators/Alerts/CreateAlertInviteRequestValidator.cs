using FluentValidation;
using Nocturne.API.Controllers.V4.Monitoring;

namespace Nocturne.API.Validators.Alerts;

public class CreateAlertInviteRequestValidator : AbstractValidator<CreateAlertInviteRequest>
{
    public CreateAlertInviteRequestValidator()
    {
        RuleFor(x => x.EscalationStepId).NotEmpty();
        RuleFor(x => x.PermissionScope).MaximumLength(100).When(x => x.PermissionScope is not null);
    }
}
