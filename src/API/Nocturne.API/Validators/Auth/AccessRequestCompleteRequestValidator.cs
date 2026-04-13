using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

public class AccessRequestCompleteRequestValidator : AbstractValidator<AccessRequestCompleteRequest>
{
    public AccessRequestCompleteRequestValidator()
    {
        RuleFor(x => x.AttestationResponseJson).NotEmpty();
        RuleFor(x => x.ChallengeToken).NotEmpty();
    }
}
