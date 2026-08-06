using LegendaryLibraryNS.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using SIL.Secrets;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LegendaryLibraryNS
{
    public class LegendaryEncryption
    {
        public static void Encrypt(string filePath, string content)
        {
            var tokenInfo = Serialization.FromJson<OauthResponse>(content);
            var userInfoContent = new LegendaryUserInfo
            {
                account_id = tokenInfo.account_id,
                displayName = tokenInfo.displayName
            };
            SetEncryptionKey(userInfoContent);
            var finalEncryptionKey = GetEncryptionKey(tokenInfo.account_id);

            using var cipher = new AesManaged();
            cipher.KeySize = 256;
            cipher.BlockSize = 128;
            cipher.Padding = PaddingMode.PKCS7;
            cipher.Mode = CipherMode.CBC;
            cipher.Key = finalEncryptionKey;
            cipher.GenerateIV();

            using Aes ivCipher = Aes.Create();
            ivCipher.Key = finalEncryptionKey;
            ivCipher.Mode = CipherMode.ECB;
            ivCipher.Padding = PaddingMode.None;

            using ICryptoTransform ivEncryptor = ivCipher.CreateEncryptor();
            var encryptedIv = ivEncryptor.TransformFinalBlock(cipher.IV, 0, cipher.IV.Length);

            var utfString = Encoding.UTF8.GetBytes(content);
            using ICryptoTransform encryptor = cipher.CreateEncryptor();
            var encryptedData = encryptor.TransformFinalBlock(utfString, 0, utfString.Length);

            byte[] result = new byte[encryptedIv.Length + encryptedData.Length];
            Buffer.BlockCopy(encryptedIv, 0, result, 0, encryptedIv.Length);
            Buffer.BlockCopy(encryptedData, 0, result, encryptedIv.Length, encryptedData.Length);

            File.WriteAllBytes(filePath, result);
        }

        public static string Decrypt(string filePath)
        {
            try
            {
                var userInfoContent = LegendaryLauncher.GetUserInfo();
                var key = GetEncryptionKey(userInfoContent.account_id);
                if (key.Length != 32)
                {
                    return "";
                }
                using var stream = new FileStream(filePath, FileMode.Open);
                byte[] encryptedIv = new byte[16];
                stream.Read(encryptedIv, 0, 16);

                byte[] iv;
                using Aes ivCipher = Aes.Create();
                ivCipher.Key = key;
                ivCipher.Mode = CipherMode.ECB;
                ivCipher.Padding = PaddingMode.None;
                using ICryptoTransform decryptor = ivCipher.CreateDecryptor();
                iv = decryptor.TransformFinalBlock(encryptedIv, 0, encryptedIv.Length);

                byte[] encryptedData = new byte[stream.Length - 16];
                stream.Read(encryptedData, 0, encryptedData.Length);

                using var cipher = new AesManaged();
                cipher.KeySize = 256;
                cipher.BlockSize = 128;
                cipher.Padding = PaddingMode.PKCS7;
                cipher.Mode = CipherMode.CBC;
                cipher.Key = key;
                cipher.IV = iv;

                using ICryptoTransform finalDecryptor = cipher.CreateDecryptor();
                byte[] decryptedBytes = finalDecryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                var decryptedData = Encoding.UTF8.GetString(decryptedBytes);
                return decryptedData;
            }
            catch (Exception ex)
            {
                var logger = LogManager.GetLogger();
                logger.Error(ex, "Can't decrypt tokens");
                return null;
            }
        }

        public static byte[] GetEncryptionKey(string userId)
        {
            bool isKeyNull = false;
            var key = "";
            try
            {
                key = PasswordStore.GetPassword($"legendary", userId);
            }
            catch
            {
                isKeyNull = true;
            }
            if (isKeyNull || key.IsNullOrEmpty())
            {
                var userInfoContent = LegendaryLauncher.GetUserInfo();
                using var sha256 = SHA256.Create();
                if (!userInfoContent.account_id.IsNullOrEmpty() && !userInfoContent.key.IsNullOrEmpty())
                {
                    key = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(userInfoContent.account_id + userInfoContent.key)));
                }
                else
                {
                    key = "";
                }
            }
            return Convert.FromBase64String(key);
        }

        public static void SetEncryptionKey(LegendaryUserInfo userInfo)
        {
            var keyBytes = new byte[32];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(keyBytes);
            var key = Convert.ToBase64String(keyBytes);
            try
            {
                PasswordStore.SetPassword($"legendary", userInfo.account_id, key);
                File.WriteAllText(LegendaryLauncher.UserInfoPath, Serialization.ToJson(userInfo));
            }
            catch
            {
                userInfo.key = key;
                File.WriteAllText(LegendaryLauncher.UserInfoPath, Serialization.ToJson(userInfo));
            }
        }
    }
}
