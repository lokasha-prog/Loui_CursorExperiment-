using System.Security.Cryptography;
using FluentAssertions;
using NMS.Infrastructure.Security;
using Xunit;

namespace NMS.UnitTests.Infrastructure;

public class EncryptionTests
{
    private static EncryptionService NewService() => new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Text_survives_a_round_trip_including_arabic()
    {
        var service = NewService();
        const string text = "حساسية من الفول السوداني — peanut allergy";

        var cipher = service.Encrypt(text);

        cipher.Should().NotContain("peanut");
        service.Decrypt(cipher).Should().Be(text);
    }

    [Fact]
    public void Same_text_encrypts_differently_each_time()
    {
        var service = NewService();

        service.Encrypt("same").Should().NotBe(service.Encrypt("same"));
    }

    [Fact]
    public void Tampered_data_is_rejected()
    {
        var service = NewService();
        var bytes = Convert.FromBase64String(service.Encrypt("secret"));
        bytes[^1] ^= 0xFF;

        var decrypt = () => service.Decrypt(Convert.ToBase64String(bytes));

        decrypt.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void A_different_key_cannot_decrypt()
    {
        var cipher = NewService().Encrypt("secret");

        var decrypt = () => NewService().Decrypt(cipher);

        decrypt.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Key_must_be_32_bytes()
    {
        var create = () => new EncryptionService(new byte[16]);

        create.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Password_hashes_verify_and_differ_per_hash()
    {
        var service = new PasswordService();
        var first = service.Hash("Correct-Horse-1");

        service.Verify(first, "Correct-Horse-1").Should().BeTrue();
        service.Verify(first, "wrong").Should().BeFalse();
        service.Hash("Correct-Horse-1").Should().NotBe(first);
    }
}
