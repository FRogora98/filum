using Filum.Engine.Memory;

namespace Filum.Engine.UnitTests.Memory;

// Spec 048: a key, a token or a password never reaches the memory; ordinary text is left alone.
public sealed class SecretFilterTests
{
    [Theory]
    [InlineData("my key is sk-" + "proj-abcdefghijklmnopqrstuvwxyz123456 thanks")]
    [InlineData("export ANTHROPIC_API_KEY=sk-" + "ant-api03-abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("token gh" + "p_abcdefghijklmnopqrstuvwxyz0123456789AB")]
    [InlineData("AKI" + "AABCDEFGHIJKLMNOP is the access key")]
    [InlineData("jwt ey" + "JhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
    [InlineData("-----BEGIN RSA PRIVATE" + " KEY-----\nMIIEowIBAAKCAQEA\n-----END RSA PRIVATE KEY-----")]
    public void Redact_WhenTextHoldsAKeyOrAToken_ShouldRemoveIt(string text)
    {
        var redacted = SecretFilter.Redact(text);

        Assert.Contains(SecretFilter.Removed, redacted);
        Assert.DoesNotMatch(@"sk-[A-Za-z0-9]{10}|ghp_[A-Za-z0-9]{10}|AKIA[A-Z]{10}|eyJhbGci|MIIEowIBAAKCAQEA", redacted);
    }

    [Theory]
    [InlineData("the wifi password: Tr0ub4dor&3", "the wifi password: [secret removed by Filum]")]
    [InlineData("api_key = \"abc123def456\"", "api_key = [secret removed by Filum]")]
    [InlineData("Client secret=s3cr3tvalue;", "Client secret=[secret removed by Filum];")]
    public void Redact_WhenAPasswordIsLabelled_ShouldKeepTheLabelAndRemoveTheValue(string text, string expected)
    {
        Assert.Equal(expected, SecretFilter.Redact(text));
    }

    [Theory]
    [InlineData("I forgot my password again, can you remind me how to reset it?")]
    [InlineData("The secret to a good risotto is patience.")]
    [InlineData("Ask Ana about the sk-8 tournament on Friday.")]
    [InlineData("My token of appreciation was a book.")]
    public void Redact_WhenTextOnlyTalksAboutSecrets_ShouldLeaveItAlone(string text)
    {
        Assert.Equal(text, SecretFilter.Redact(text));
    }
}
