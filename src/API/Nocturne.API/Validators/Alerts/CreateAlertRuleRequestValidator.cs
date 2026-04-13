using FluentValidation;
using Nocturne.API.Controllers.V4.Monitoring;

namespace Nocturne.API.Validators.Alerts;

public class CreateAlertRuleRequestValidator : AbstractValidator<CreateAlertRuleRequest>
{
    public CreateAlertRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.ConditionType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.HysteresisMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConfirmationReadings).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Severity).MaximumLength(50).When(x => x.Severity is not null);
        RuleFor(x => x.Schedules).Must(s => s == null || s.Any(sch => sch.IsDefault))
            .WithMessage("At least one schedule must be marked as default");
    }
}
