using System;
using System.Security.Cryptography;

namespace GroceryPos.Domain
{
    /// <summary>PBKDF2 with SHA1 (Rfc2898DeriveBytes default, available on net48).</summary>
    public static class PinHasher
    {
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int Iterations = 20000;

        public static string Hash(string pin)
        {
            if (string.IsNullOrEmpty(pin)) throw new ArgumentException("PIN required");
            byte[] salt = new byte[SaltBytes];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);
            using (var kdf = new Rfc2898DeriveBytes(pin, salt, Iterations))
            {
                byte[] hash = kdf.GetBytes(HashBytes);
                return "pbkdf2$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
            }
        }

        public static bool Verify(string pin, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
            int iter;
            if (!int.TryParse(parts[1], out iter)) return false;
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            using (var kdf = new Rfc2898DeriveBytes(pin, salt, iter))
            {
                byte[] actual = kdf.GetBytes(expected.Length);
                return ConstantTimeEquals(actual, expected);
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
