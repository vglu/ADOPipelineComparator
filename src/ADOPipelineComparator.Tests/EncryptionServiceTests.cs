using ADOPipelineComparator.Core.Services;

namespace ADOPipelineComparator.Tests;

public sealed class EncryptionServiceTests
{
    [Fact]
    public void EncryptAndDecrypt_Roundtrip_Succeeds()
    {
        var service = new EncryptionService("unit-test-key");
        const string plain = "my-secret-pat";

        var encrypted = service.Encrypt(plain);
        var decrypted = service.Decrypt(encrypted);

        Assert.StartsWith("enc:", encrypted, StringComparison.Ordinal);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Decrypt_PlainValue_ReturnsInput()
    {
        var service = new EncryptionService("unit-test-key");

        var value = service.Decrypt("plain-value");

        Assert.Equal("plain-value", value);
    }
}
