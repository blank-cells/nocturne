using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class PasskeyLoginCompleteRequestValidator : AbstractValidator<PasskeyLoginCompleteRequest>
{
    public PasskeyLoginCompleteRequestValidator()
    {
        RuleFor(x => x.AssertionResponseJson).NotEmpty();
    }
}
