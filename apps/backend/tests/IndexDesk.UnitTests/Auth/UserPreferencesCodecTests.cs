using FluentAssertions;
using IndexDesk.Modules.Auth.Services;
using Xunit;

namespace IndexDesk.UnitTests.Auth;

public class UserPreferencesCodecTests
{
    [Fact]
    public void Parse_WithNullOrEmpty_ReturnsDefaultFalse()
    {
        UserPreferencesCodec.Parse(null).HideValues.Should().BeFalse();
        UserPreferencesCodec.Parse(string.Empty).HideValues.Should().BeFalse();
        UserPreferencesCodec.Parse("   ").HideValues.Should().BeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"hideValues\": \"yes\"}")]
    public void Parse_WithMissingOrInvalidHideValues_ReturnsFalse(string preferences)
    {
        UserPreferencesCodec.Parse(preferences).HideValues.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"hideValues\": true}", true)]
    [InlineData("{\"hideValues\": false}", false)]
    [InlineData("{\"hideValues\": true, \"theme\": \"dark\"}", true)]
    public void Parse_WithKnownPayload_ExtractsHideValues(string preferences, bool expected)
    {
        UserPreferencesCodec.Parse(preferences).HideValues.Should().Be(expected);
    }

    [Fact]
    public void Serialize_OverEmpty_PersistsHideValues()
    {
        var json = UserPreferencesCodec.Serialize("{}", hideValues: true);

        json.Should().Contain("\"hideValues\":true");
        UserPreferencesCodec.Parse(json).HideValues.Should().BeTrue();
    }

    [Fact]
    public void Serialize_PreservesUnknownKeys_AndOverwritesHideValues()
    {
        var current = "{\"theme\":\"dark\",\"hideValues\":false}";

        var json = UserPreferencesCodec.Serialize(current, hideValues: true);

        json.Should().Contain("\"theme\":\"dark\"");
        json.Should().Contain("\"hideValues\":true");
        UserPreferencesCodec.Parse(json).HideValues.Should().BeTrue();
    }

    [Fact]
    public void Serialize_WithCorruptedCurrent_DropsGarbageButWritesTypedValue()
    {
        var json = UserPreferencesCodec.Serialize("{corrupted", hideValues: true);

        UserPreferencesCodec.Parse(json).HideValues.Should().BeTrue();
    }
}
