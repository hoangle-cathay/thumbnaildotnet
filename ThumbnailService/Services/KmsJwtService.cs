using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ThumbnailService.Services
{
    public class KmsJwtService : IJwtService
    {
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly string _kmsKeyName;
        private readonly string _issuer;
        private readonly string _audience;

        public KmsJwtService(KeyManagementServiceClient kmsClient, IConfiguration config)
        {
            _kmsClient = kmsClient;
            _kmsKeyName = config["Jwt:KmsKeyName"] ?? throw new ArgumentNullException("Jwt:KmsKeyName");
            _issuer = config["Jwt:Issuer"] ?? "thumbnailservice";
            _audience = config["Jwt:Audience"] ?? "thumbnailservice-users";
        }

        public async Task<string> GenerateTokenAsync(Guid userId, string username)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: now,
                expires: now.AddHours(12)
            );

            var handler = new JwtSecurityTokenHandler();
            var unsignedJwt = handler.WriteToken(jwt);
            var headerAndPayload = unsignedJwt.Substring(0, unsignedJwt.LastIndexOf('.'));
            var bytesToSign = Encoding.UTF8.GetBytes(headerAndPayload);

            // Sign with KMS
            var signRequest = new AsymmetricSignRequest
            {
                Name = _kmsKeyName,
                Digest = new Digest { Sha256 = Google.Protobuf.ByteString.CopyFrom(System.Security.Cryptography.SHA256.HashData(bytesToSign)) }
            };
            var signResponse = await _kmsClient.AsymmetricSignAsync(signRequest);
            var signature = signResponse.Signature.ToBase64Url();

            return headerAndPayload + "." + signature;
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var headerAndPayload = token.Substring(0, token.LastIndexOf('.'));
            var signature = token.Substring(token.LastIndexOf('.') + 1);
            var bytesToVerify = Encoding.UTF8.GetBytes(headerAndPayload);
            var sigBytes = signature.FromBase64Url();

            // Get public key from KMS
            var publicKey = await _kmsClient.GetPublicKeyAsync(_kmsKeyName);
            var rsa = publicKey.ToRSA();

            var valid = rsa.VerifyData(bytesToVerify, sigBytes, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            if (!valid) return null;

            var validationParams = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateIssuerSigningKey = true
            };
            try
            {
                var principal = handler.ValidateToken(token, validationParams, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }

    public static class KmsExtensions
    {
        public static string ToBase64Url(this ByteString bytes)
        {
            return Convert.ToBase64String(bytes.ToByteArray())
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
        public static byte[] FromBase64Url(this string s)
        {
            string padded = s.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
        public static System.Security.Cryptography.RSA ToRSA(this PublicKey publicKey)
        {
            var key = System.Security.Cryptography.RSA.Create();
            key.ImportFromPem(publicKey.Pem);
            return key;
        }
    }
}