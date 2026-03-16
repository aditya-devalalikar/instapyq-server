using System;
using System.Security.Cryptography;
using System.Text;

namespace pqy_server.Helpers
{
    public static class TokenHelpers
    {
        public static string ComputeSha256Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }
    }
}