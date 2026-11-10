using System;
using System.Security.Cryptography;
namespace Tecnova.Proyectos.Api.Security
{
    public static class Passwords
    {
        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var kdf = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256))
                return "v1$600000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(kdf.GetBytes(32));
        }
        public static bool Verify(string password, string stored)
        {
            try
            {
                var parts = stored.Split('$');
                if (parts.Length != 4 || parts[0] != "v1" || parts[1] != "600000") return false;
                using (var kdf = new Rfc2898DeriveBytes(password, Convert.FromBase64String(parts[2]), 600000, HashAlgorithmName.SHA256))
                {
                    var actual = kdf.GetBytes(32); var expected = Convert.FromBase64String(parts[3]);
                    if (expected.Length != actual.Length) return false;
                    int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                    return difference == 0;
                }
            }
            catch (FormatException) { return false; }
        }
    }
}
