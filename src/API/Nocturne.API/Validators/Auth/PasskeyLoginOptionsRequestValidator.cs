using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class PasskeyLoginOptionsRequestValidator : AbstractValidator<PasskeyLoginOptionsRequest>
{
    public PasskeyLoginOptionsRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
    }
}
