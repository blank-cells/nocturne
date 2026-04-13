using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class PasskeyRegisterCompleteRequestValidator : AbstractValidator<PasskeyRegisterCompleteRequest>
{
    public PasskeyRegisterCompleteRequestValidator()
    {
        RuleFor(x => x.AttestationResponseJson).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(200).When(x => x.Label is not null);
    }
}
