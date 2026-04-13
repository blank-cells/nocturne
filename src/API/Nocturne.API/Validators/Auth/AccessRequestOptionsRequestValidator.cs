using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class AccessRequestOptionsRequestValidator : AbstractValidator<AccessRequestOptionsRequest>
{
    public AccessRequestOptionsRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Message).MaximumLength(500);
    }
}
