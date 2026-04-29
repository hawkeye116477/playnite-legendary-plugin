using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommonPlugin
{
    /// <summary>
    /// Based on https://www.codegrepper.com/code-examples/csharp/encrypt+text+file+in+C%23+with+key and https://stackoverflow.com/a/78577582
    /// </summary>
    public static class Encryption
    {
        private static byte[] GenerateRandomSalt()
        {
            var data = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            for (var i = 0; i < 10; i++)
            {
                rng.GetBytes(data);
            }

            return data;
        }

        public static void EncryptToFile(string filePath, string content, Encoding encoding, string password)
        {
            var salt = GenerateRandomSalt();
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var key = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, 100_000, HashAlgorithmName.SHA256, 32);
            using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);
            var byteContent = encoding.GetBytes(content);
            var cipherText = new byte[byteContent.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            RandomNumberGenerator.Fill(tag);
            aes.Encrypt(nonce, byteContent, cipherText, tag);
            using var outFile = new FileStream(filePath, FileMode.Create);
            outFile.Write(salt, 0, salt.Length);
            outFile.Write(nonce);
            outFile.Write(tag);
            outFile.Write(cipherText);
        }

        public static string DecryptFromFile(string inputFile, Encoding encoding, string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var salt = new byte[32];
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            using var fsCrypt = new FileStream(inputFile, FileMode.Open);
            fsCrypt.ReadExactly(salt);
            fsCrypt.ReadExactly(nonce);
            fsCrypt.ReadExactly(tag);
            var cipherText = new byte[fsCrypt.Length - salt.Length - nonce.Length - tag.Length];
            fsCrypt.ReadExactly(cipherText);
            var plainText = new byte[cipherText.Length];
            var key = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, 100_000, HashAlgorithmName.SHA256, 32);
            using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, cipherText, tag, plainText);
            return encoding.GetString(plainText);
        }
    }
}