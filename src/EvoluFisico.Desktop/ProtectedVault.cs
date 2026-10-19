using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EvoluFisico.Desktop;

/// <summary>Versioned authenticated encryption for future transactional data-store migration.
/// This component is not yet wired into WebView2/IndexedDB persistence.</summary>
internal static class ProtectedVault
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    internal static byte[] Encrypt(byte[] plaintext, string? masterPassword = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key;
        string mode;
        byte[]? wrappedKey = null;
        if (masterPassword is null)
        {
            mode = "windows-dpapi";
            key = RandomNumberGenerator.GetBytes(32);
            wrappedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            if (masterPassword.Length < 12) throw new ArgumentException("A senha mestre deve ter pelo menos 12 caracteres.");
            mode = "master-password";
            key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, 32);
        }
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            var aad = Encoding.UTF8.GetBytes("EvoluFisicoDesktop/v1/" + mode);
            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plaintext, cipher, tag, aad);
            return JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, mode,
                Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag), Convert.ToBase64String(cipher),
                wrappedKey is null ? null : Convert.ToBase64String(wrappedKey)));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    internal static byte[] Decrypt(byte[] envelopeBytes, string? masterPassword = null)
    {
        ArgumentNullException.ThrowIfNull(envelopeBytes);
        var e = JsonSerializer.Deserialize<Envelope>(envelopeBytes)
            ?? throw new InvalidDataException("Cofre inválido.");
        if (e.Version != 1 || (e.Mode != "windows-dpapi" && e.Mode != "master-password"))
            throw new InvalidDataException("Formato de cofre não suportado.");
        var salt = Convert.FromBase64String(e.Salt);
        var nonce = Convert.FromBase64String(e.Nonce);
        var tag = Convert.FromBase64String(e.Tag);
        var cipher = Convert.FromBase64String(e.Ciphertext);
        if (salt.Length != SaltSize || nonce.Length != NonceSize || tag.Length != TagSize)
            throw new InvalidDataException("Parâmetros de criptografia inválidos.");
        byte[] key;
        if (e.Mode == "windows-dpapi")
        {
            if (e.WrappedKey is null) throw new InvalidDataException("Chave protegida ausente.");
            key = ProtectedData.Unprotect(Convert.FromBase64String(e.WrappedKey), null, DataProtectionScope.CurrentUser);
        }
        else
        {
            if (masterPassword is null) throw new CryptographicException("Senha mestre necessária.");
            key = Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, 32);
        }
        try
        {
            if (key.Length != 32) throw new CryptographicException("Chave inválida.");
            var plaintext = new byte[cipher.Length];
            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plaintext, Encoding.UTF8.GetBytes("EvoluFisicoDesktop/v1/" + e.Mode));
            return plaintext;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private sealed record Envelope(int Version, string Mode, string Salt, string Nonce,
        string Tag, string Ciphertext, string? WrappedKey);
}
