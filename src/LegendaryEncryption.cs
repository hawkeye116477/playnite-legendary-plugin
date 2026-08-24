using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommonPlugin;
using LegendaryLibraryNS.Models;
using Playnite;
using Playnite.Common;

namespace LegendaryLibraryNS;

public class LegendaryEncryption
{
    public static void Encrypt(string filePath, string content)
    {
        var tokenInfo = Serialization.FromJson<OauthResponse>(content);
        if (tokenInfo == null)
        {
            return;
        }
        var userInfoContent = new LegendaryUserInfo
        {
            Account_id = tokenInfo.Account_id,
            DisplayName = tokenInfo.DisplayName
        };
        SetEncryptionKey(userInfoContent);
        var finalEncryptionKey = GetEncryptionKey(tokenInfo.Account_id);

        using Aes cipher = Aes.Create();
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

    public static string? Decrypt(string filePath)
    {
        try
        {
            var userInfoContent = LegendaryLauncher.GetUserInfo();
            var key = GetEncryptionKey(userInfoContent.Account_id);
            if (key.Length != 32)
            {
                throw new CryptographicException("Invalid encryption key");
            }

            using var stream = new FileStream(filePath, FileMode.Open);
            byte[] encryptedIv = new byte[16];
            stream.ReadExactly(encryptedIv, 0, 16);

            byte[] iv;
            using Aes ivCipher = Aes.Create();
            ivCipher.Key = key;
            ivCipher.Mode = CipherMode.ECB;
            ivCipher.Padding = PaddingMode.None;
            using ICryptoTransform decryptor = ivCipher.CreateDecryptor();
            iv = decryptor.TransformFinalBlock(encryptedIv, 0, encryptedIv.Length);

            byte[] encryptedData = new byte[stream.Length - 16];
            stream.ReadExactly(encryptedData, 0, encryptedData.Length);

            using var cipher = Aes.Create();
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
            logger.Error(ex, "Failed to decrypt tokens.");
            FileSystem.DeleteFileSafe(filePath);
            return null;
        }
    }

    public static byte[] GetEncryptionKey(string userId)
    {
        bool isKeyNull = false;
        var key = "";
        try
        {
            key = Keyring.GetPassword("legendary", userId);
        }
        catch
        {
            isKeyNull = true;
        }

        var userInfoContent = LegendaryLauncher.GetUserInfo();
        if (isKeyNull || key.IsNullOrEmpty())
        {
            using var sha256 = SHA256.Create();
            if (!userInfoContent.Account_id.IsNullOrEmpty() && !userInfoContent.Key.IsNullOrEmpty())
            {
                try
                {
                    key = Convert.ToBase64String(
                        sha256.ComputeHash(Encoding.UTF8.GetBytes(userInfoContent.Account_id + userInfoContent.Key)));
                }
                catch
                {
                    key = "";
                }
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
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(keyBytes);
        var key = Convert.ToBase64String(keyBytes);
        try
        {
            Keyring.SetPassword("legendary", userInfo.Account_id, key);
            File.WriteAllText(LegendaryLauncher.UserInfoPath, Serialization.ToJson(userInfo));
        }
        catch
        {
            userInfo.Key = key;
            File.WriteAllText(LegendaryLauncher.UserInfoPath, Serialization.ToJson(userInfo));
        }
    }
}