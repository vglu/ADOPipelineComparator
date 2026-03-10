using System.Security.Cryptography;
using System.Text;
using ADOPipelineComparator.Core.Interfaces;

namespace ADOPipelineComparator.Core.Services;

public sealed class EncryptionService : IEncryptionService
{
    private const string Prefix = "enc:";
    private readonly byte[] _key;

    public EncryptionService(string keyMaterial)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            throw new InvalidOperationException("Encryption key is missing. Set ENCRYPTION_KEY or appsettings:EncryptionKey.");
        }

        _key = BuildKey(keyMaterial);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(plainText));
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(cipherText));
        }

        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return cipherText;
        }

        var payload = Convert.FromBase64String(cipherText[Prefix.Length..]);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[16];
        var cipherBytes = new byte[payload.Length - iv.Length];
        Buffer.BlockCopy(payload, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(payload, iv.Length, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        var plaintextBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] BuildKey(string keyMaterial)
    {
        if (TryDecodeBase64Key(keyMaterial, out var base64Key))
        {
            return base64Key;
        }

        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(keyMaterial));
    }

    private static bool TryDecodeBase64Key(string keyMaterial, out byte[] key)
    {
        key = Array.Empty<byte>();

        try
        {
            var bytes = Convert.FromBase64String(keyMaterial);
            if (bytes.Length == 32)
            {
                key = bytes;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
