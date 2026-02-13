using System;
using System.Security.Cryptography;
using System.Text;

namespace MyCrypto
{
    public class SlowEncryptor
    {
        private const int Iterations = 10000;
        private const int SaltSize = 32;
        private const int KeySize = 32;
        private const int IvSize = 16;

        public enum AlgorithmType
        {
            AES_CBC,
            AES_GCM,
            ChaCha20_Poly1305
        }

        public static (string cipherText, string saltBase64, AlgorithmType algo) Encrypt(string plainText, string password, AlgorithmType algorithm)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] key = GenerateSlowKey(password, salt);
            byte[] iv = new byte[IvSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            byte[] encryptedBytes;

            switch (algorithm)
            {
                case AlgorithmType.AES_CBC:
                    encryptedBytes = EncryptAesCbc(plainText, key, iv);
                    break;
                case AlgorithmType.AES_GCM:
                    encryptedBytes = EncryptAesGcm(plainText, key, iv);
                    break;
                case AlgorithmType.ChaCha20_Poly1305:
                    encryptedBytes = EncryptChaCha20Poly1305(plainText, key, iv);
                    break;
                default:
                    throw new ArgumentException("Unsupported algorithm");
            }

            byte[] result = new byte[iv.Length + encryptedBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, iv.Length, encryptedBytes.Length);

            return (Convert.ToBase64String(result), Convert.ToBase64String(salt), algorithm);
        }

        public static string Decrypt(string cipherText, string password, string saltBase64, AlgorithmType algorithm)
        {
            byte[] combinedBytes = Convert.FromBase64String(cipherText);
            byte[] salt = Convert.FromBase64String(saltBase64);

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(combinedBytes, 0, iv, 0, iv.Length);

            byte[] encryptedBytes = new byte[combinedBytes.Length - iv.Length];
            Buffer.BlockCopy(combinedBytes, iv.Length, encryptedBytes, 0, encryptedBytes.Length);

            byte[] key = GenerateSlowKey(password, salt);

            switch (algorithm)
            {
                case AlgorithmType.AES_CBC:
                    return DecryptAesCbc(encryptedBytes, key, iv);
                case AlgorithmType.AES_GCM:
                    return DecryptAesGcm(encryptedBytes, key, iv);
                case AlgorithmType.ChaCha20_Poly1305:
                    return DecryptChaCha20Poly1305(encryptedBytes, key, iv);
                default:
                    throw new ArgumentException("Unsupported algorithm");
            }
        }

        private static byte[] GenerateSlowKey(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA512);
            return pbkdf2.GetBytes(KeySize);
        }

        private static byte[] EncryptAesCbc(string plainText, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        private static string DecryptAesCbc(byte[] encryptedBytes, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        private static byte[] EncryptAesGcm(string plainText, byte[] key, byte[] iv)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[plainBytes.Length];

            using var aes = new AesGcm(key);
            aes.Encrypt(iv, plainBytes, ciphertext, tag);

            byte[] result = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
            return result;
        }

        private static string DecryptAesGcm(byte[] encryptedData, byte[] key, byte[] iv)
        {
            byte[] ciphertext = new byte[encryptedData.Length - 16];
            byte[] tag = new byte[16];

            Buffer.BlockCopy(encryptedData, 0, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(encryptedData, ciphertext.Length, tag, 0, tag.Length);

            byte[] plainBytes = new byte[ciphertext.Length];

            using var aes = new AesGcm(key);
            aes.Decrypt(iv, ciphertext, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        private static byte[] EncryptTripleDes(string plainText, byte[] key, byte[] iv)
        {
            using var tripleDes = TripleDES.Create();
            tripleDes.Key = AdjustKeyForTripleDES(key);
            tripleDes.IV = iv;
            tripleDes.Mode = CipherMode.CBC;
            tripleDes.Padding = PaddingMode.PKCS7;

            using var encryptor = tripleDes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        private static string DecryptTripleDes(byte[] encryptedBytes, byte[] key, byte[] iv)
        {
            using var tripleDes = TripleDES.Create();
            tripleDes.Key = AdjustKeyForTripleDES(key);
            tripleDes.IV = iv;
            tripleDes.Mode = CipherMode.CBC;
            tripleDes.Padding = PaddingMode.PKCS7;

            using var decryptor = tripleDes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        private static byte[] AdjustKeyForTripleDES(byte[] key)
        {
            byte[] tripleDesKey = new byte[24];
            Array.Copy(key, 0, tripleDesKey, 0, Math.Min(key.Length, tripleDesKey.Length));
            return tripleDesKey;
        }

        private static byte[] EncryptChaCha20Poly1305(string plainText, byte[] key, byte[] iv)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[plainBytes.Length];

            using var chacha = new ChaCha20Poly1305(key);
            chacha.Encrypt(iv, plainBytes, ciphertext, tag);

            byte[] result = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
            return result;
        }

        private static string DecryptChaCha20Poly1305(byte[] encryptedData, byte[] key, byte[] iv)
        {
            byte[] ciphertext = new byte[encryptedData.Length - 16];
            byte[] tag = new byte[16];

            Buffer.BlockCopy(encryptedData, 0, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(encryptedData, ciphertext.Length, tag, 0, tag.Length);

            byte[] plainBytes = new byte[ciphertext.Length];

            using var chacha = new ChaCha20Poly1305(key);
            chacha.Decrypt(iv, ciphertext, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}