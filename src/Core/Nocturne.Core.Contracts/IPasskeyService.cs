namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for managing WebAuthn/FIDO2 passkey authentication
/// </summary>
public interface IPasskeyService
{
    Task<PasskeyRegistrationOptions> GenerateRegistrationOptionsAsync(Guid subjectId, string username, Guid tenantId);
    Task<PasskeyCredentialResult> CompleteRegistrationAsync(string attestationResponseJson, string challengeToken, Guid tenantId);
    Task<PasskeyAssertionOptions> GenerateDiscoverableAssertionOptionsAsync(Guid tenantId);
    Task<PasskeyAssertionOptions> GenerateAssertionOptionsAsync(string username, Guid tenantId);
    Task<PasskeyAssertionResult> CompleteAssertionAsync(string assertionResponseJson, string challengeToken, Guid tenantId);
    Task<List<PasskeyCredentialInfo>> GetCredentialsAsync(Guid subjectId, Guid tenantId);
    Task RemoveCredentialAsync(Guid credentialId, Guid subjectId, Guid tenantId);
    Task<int> GetCredentialCountAsync(Guid subjectId, Guid tenantId);
}

public record PasskeyRegistrationOptions(string OptionsJson, string ChallengeToken);
public record PasskeyAssertionOptions(string OptionsJson, string ChallengeToken);
public record PasskeyAssertionResult(Guid SubjectId, string Username, string DisplayName);
public record PasskeyCredentialResult(Guid CredentialId, Guid SubjectId);
public record PasskeyCredentialInfo(Guid Id, string? Label, DateTime CreatedAt, DateTime? LastUsedAt, Guid? AaGuid);
