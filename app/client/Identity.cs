// SPDX-License-Identifier: GPL-2.0-only
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AetherDrop;

/// <summary>
/// The receiver's TLS identity, made here so no OpenSSL is needed: the same
/// self-signed RSA-2048 certificate (CN=AirDrop receiver, one year) that
/// winawdl's airdrop\identity.py would create, in the files it looks for.
/// </summary>
public static class Identity
{
    public static string KeysDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "winawdl", "keys");

    public static void Ensure()
    {
        var cert = Path.Combine(KeysDir, "certificate.self-signed.pem");
        var key = Path.Combine(KeysDir, "key.self-signed.pem");
        if (File.Exists(cert) && File.Exists(key))
        {
            try
            {
                using var existing = X509Certificate2.CreateFromPemFile(cert);
                if (existing.NotAfter > DateTime.Now.AddDays(7)) return;
            }
            catch (CryptographicException) { }
        }
        Directory.CreateDirectory(KeysDir);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=AirDrop receiver", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        using var made = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
        File.WriteAllText(key, new string(PemEncoding.Write("PRIVATE KEY", rsa.ExportPkcs8PrivateKey())) + "\n");
        File.WriteAllText(cert, new string(PemEncoding.Write("CERTIFICATE", made.RawData)) + "\n");
    }
}
