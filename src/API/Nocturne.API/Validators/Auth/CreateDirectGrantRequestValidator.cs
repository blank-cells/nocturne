using FluentValidation;
using Nocturne.API.Controllers.Authentication;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Validators.Auth;

public class CreateDirectGrantRequestValidator : AbstractValidator<CreateDirectGrantRequest>
{
    public CreateDirectGrantRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scopes).NotEmpty().WithMessage("At least one scope is required");
        RuleForEach(x => x.Scopes).Must(OAuthScopes.IsValid)
            .WithMessage(scope => $"Invalid scope: {scope}");
    }
}
