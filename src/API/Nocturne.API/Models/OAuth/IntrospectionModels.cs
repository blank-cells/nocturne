namespace Nocturne.API.Models.OAuth;

/// <summary>
/// Token introspection response (RFC 7662)
/// </summary>
public class TokenIntrospectionResponse
{
    public bool Active { get; set; }
    public string? Scope { get; set; }
    public string? ClientId { get; set; }
    public string? Sub { get; set; }
    public long? Exp { get; set; }
    public long? Iat { get; set; }
    public string? Jti { get; set; }
    public string? TokenType { get; set; }
}
