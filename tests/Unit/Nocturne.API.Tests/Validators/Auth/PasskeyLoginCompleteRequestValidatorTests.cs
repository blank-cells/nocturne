using FluentValidation.TestHelper;
using Nocturne.API.Controllers.Authentication;
using Nocturne.API.Validators.Auth;
using Xunit;

namespace Nocturne.API.Tests.Validators.Auth;

public class PasskeyLoginCompleteRequestValidatorTests
{
    private readonly PasskeyLoginCompleteRequestValidator _validator = new();

    private static PasskeyLoginCompleteRequest ValidRequest() => new()
    {
        AssertionResponseJson = "{\"id\":\"test\"}",
        ChallengeToken = "token123",
    };

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_assertion_response_fails(string? value)
    {
        var request = ValidRequest();
        request.AssertionResponseJson = value!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AssertionResponseJson);
    }
}
