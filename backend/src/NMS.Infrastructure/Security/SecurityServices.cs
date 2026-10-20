using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NMS.Application.Abstractions;
using NMS.Application.Auth;
using NMS.Domain.Identity;

namespace NMS.Infrastructure.Security;

/// <summary>Configuration section "Jwt". The signing key is a secret supplied by the owner, never stored in code.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Base64 text of at least 32 random bytes.</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    public SymmetricSecurityKey CreateKey() => new(Convert.FromBase64String(SigningKey));
}

/// <summary>Configuration section "Encryption". Base64 text of exactly 32 random bytes.</summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    [Required]
    public string Key { get; set; } = string.Empty;
}

public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);
}

/// <summary>AES-256-GCM column encryption for sensitive fields (medical notes, identity documents, salary).</summary>
public sealed class EncryptionService : IEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public EncryptionService(IOptions<EncryptionOptions> options)
        : this(Convert.FromBase64String(options.Value.Key))
    {
    }

    public EncryptionService(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new InvalidOperationException("The encryption key must be exactly 32 bytes (base64 encoded).");
        }

        _key = key;
    }

    public string Encrypt(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var nonce = data[..NonceSize];
        var tag = data[NonceSize..(NonceSize + TagSize)];
        var cipher = data[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
}

public sealed class TokenService(IOptions<JwtOptions> jwt, IOptions<AuthOptions> auth, IClock clock) : ITokenService
{
    public AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> roles)
    {
        var options = jwt.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(auth.Value.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new("preferred_language", user.PreferredLanguage),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(options.CreateKey(), SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
