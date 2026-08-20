using FluentAssertions;
using IndexDesk.Modules.Auth.Security;
using Xunit;

namespace IndexDesk.UnitTests.Auth;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _sut = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyStringInArgon2idFormat()
    {
        // Act
        var hash = _sut.HashPassword("SecurePassword123!");

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$argon2id$");

        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        parts.Should().HaveCount(5);
        parts[0].Should().Be("argon2id");
        parts[1].Should().StartWith("v=");
        parts[2].Should().StartWith("m=");
        parts[3].Should().NotBeNullOrWhiteSpace(); // salt (base64)
        parts[4].Should().NotBeNullOrWhiteSpace(); // hash (base64)
    }

    [Fact]
    public void VerifyPassword_SucceedsWithCorrectPassword()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = _sut.HashPassword(password);

        // Act
        var verified = _sut.VerifyPassword(password, hash);

        // Assert
        verified.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_FailsWithWrongPassword()
    {
        // Arrange
        var hash = _sut.HashPassword("SecurePassword123!");

        // Act
        var verified = _sut.VerifyPassword("WrongPassword456!", hash);

        // Assert
        verified.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_GeneratesDifferentSaltForSamePassword()
    {
        // Arrange
        const string password = "SecurePassword123!";

        // Act
        var hash1 = _sut.HashPassword(password);
        var hash2 = _sut.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2);

        var salt1 = hash1.Split('$', StringSplitOptions.RemoveEmptyEntries)[3];
        var salt2 = hash2.Split('$', StringSplitOptions.RemoveEmptyEntries)[3];
        salt1.Should().NotBe(salt2);
    }
}
