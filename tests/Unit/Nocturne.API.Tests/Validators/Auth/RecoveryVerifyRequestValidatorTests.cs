using FluentValidation.TestHelper;
using Nocturne.API.Controllers.Authentication;
using Nocturne.API.Validators.Auth;
using Xunit;

namespace Nocturne.API.Tests.Validators.Auth;

public class RecoveryVerifyRequestValidatorTests
{
    private readonly RecoveryVerifyRequestValidator _validator = new();

    private static RecoveryVerifyRequest ValidRequest() => new()
    {
        Username = "testuser",
        Code = "ABCD-1234",
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
    [InlineData("   ")]
    public void Empty_username_fails(string? username)
    {
        var request = ValidRequest();
        request.Username = username!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Username_exceeding_max_length_fails()
    {
        var request = ValidRequest();
        request.Username = new string('a', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_code_fails(string? code)
    {
        var request = ValidRequest();
        request.Code = code!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Code_exceeding_max_length_fails()
    {
        var request = ValidRequest();
        request.Code = new string('a', 51);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }
}
