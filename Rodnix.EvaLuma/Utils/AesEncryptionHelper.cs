using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Rodnix.EvaLuma.Utils
{
    public static class AesEncryptionHelper
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("EvalumaSecretKey_256Bits_1234567"); 
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("EvalumaInitVectr");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs);

            sw.Write(plainText);
            sw.Flush();
            cs.FlushFinalBlock();

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                using var aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var buffer = Convert.FromBase64String(cipherText);

                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }
    }
}
