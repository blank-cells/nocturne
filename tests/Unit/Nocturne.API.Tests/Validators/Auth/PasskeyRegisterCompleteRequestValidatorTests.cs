using FluentValidation.TestHelper;
using Nocturne.API.Controllers.Authentication;
using Nocturne.API.Validators.Auth;
using Xunit;

namespace Nocturne.API.Tests.Validators.Auth;

public class PasskeyRegisterCompleteRequestValidatorTests
{
    private readonly PasskeyRegisterCompleteRequestValidator _validator = new();

    private static PasskeyRegisterCompleteRequest ValidRequest() => new()
    {
        AttestationResponseJson = "{\"id\":\"test\"}",
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
    public void Empty_attestation_response_fails(string? value)
    {
        var request = ValidRequest();
        request.AttestationResponseJson = value!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AttestationResponseJson);
    }

    [Fact]
    public void Label_exceeding_max_length_fails()
    {
        var request = ValidRequest();
        request.Label = new string('a', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Label);
    }

    [Fact]
    public void Null_label_passes()
    {
        var request = ValidRequest();
        request.Label = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Label);
    }
}
