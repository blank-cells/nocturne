using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class PasskeyRegisterOptionsRequestValidator : AbstractValidator<PasskeyRegisterOptionsRequest>
{
    public PasskeyRegisterOptionsRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
    }
}
