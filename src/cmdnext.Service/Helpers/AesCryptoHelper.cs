using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Helpers
{
    public class AesCryptoHelper : ICryptoHelper
    {
        private readonly byte[] _key;
        private readonly byte[] _iv = new byte[16]; // Initialization Vector - 16 bytes for AES

        public AesCryptoHelper(string key)
        {
            // Derive a 256-bit key from the provided string
            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            // For simplicity, we're using a zero IV. In production, you should generate a random IV
            // and store it with the encrypted data.
            Array.Clear(_iv, 0, _iv.Length);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var memoryStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            {
                using var streamWriter = new StreamWriter(cryptoStream);
                streamWriter.Write(plainText);
            }

            return Convert.ToBase64String(memoryStream.ToArray());
        }

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            var buffer = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var memoryStream = new MemoryStream(buffer);
            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
            {
                using var streamReader = new StreamReader(cryptoStream);
                return streamReader.ReadToEnd();
            }
        }

        public string GenerateSalt()
        {
            var random = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(random);
            return Convert.ToBase64String(random);
        }

        public string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = password + salt;
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(bytes);
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }
    }
}
