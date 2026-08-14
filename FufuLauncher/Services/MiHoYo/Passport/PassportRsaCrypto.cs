/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Security.Cryptography;
using System.Text;

namespace FufuLauncher.Services.MiHoYo.Passport;

public static class PassportRsaCrypto
{
    // Coming from https://github.com/UIGF-org/Hoyolab.Salt#cn-passport-api-rsa_public_key
    private const string CnPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDDvekdPMHN3AYhm/vktJT+YJr7
        cI5DcsNKqdsx5DZX0gDuWFuIjzdwButrIYPNmRJ1G8ybDIF7oDW2eEpm5sMbL9zs
        9ExXCdvqrn51qELbqj0XxtMTIpaCHFSI50PfPpTFV9Xt/hmyVwokoOXFlAEgCn+Q
        CgGs52bFoYMtyi+xEQIDAQAB
        -----END PUBLIC KEY-----
        """;
    
    private const string OverseaPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4PMS2JVMwBsOIrYWRluY
        wEiFZL7Aphtm9z5Eu/anzJ09nB00uhW+ScrDWFECPwpQto/GlOJYCUwVM/raQpAj
        /xvcjK5tNVzzK94mhk+j9RiQ+aWHaTXmOgurhxSp3YbwlRDvOgcq5yPiTz0+kSeK
        ZJcGeJ95bvJ+hJ/UMP0Zx2qB5PElZmiKvfiNqVUk8A8oxLJdBB5eCpqWV6CUqDKQ
        KSQP4sM0mZvQ1Sr4UcACVcYgYnCbTZMWhJTWkrNXqI8TMomekgny3y+d6NX/cFa6
        6jozFIF4HCX5aW8bp8C8vq2tFvFbleQ/Q3CU56EWWKMrOcpmFtRmC18s9biZBVR/
        8QIDAQAB
        -----END PUBLIC KEY-----
        """;
    
    public static string EncryptCn(string source) => Encrypt(source, CnPublicKeyPem);
    
    public static string EncryptOversea(string source) => Encrypt(source, OverseaPublicKeyPem);

    private static string Encrypt(string source, string publicKeyPem)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(source), RSAEncryptionPadding.Pkcs1));
    }
}
