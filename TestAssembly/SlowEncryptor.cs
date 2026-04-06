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

        private const int IvSizeAesCbc = 16; 
        private const int IvSizeAesGcm = 12; 
        private const int IvSizeChaCha20 = 12;

        private const int TagSize = 16;

        public enum AlgorithmType
        {
            AES_CBC,
            AES_GCM,
            ChaCha20_Poly1305
        }

        public static (string cipherText, string saltBase64, AlgorithmType algo) Encrypt(string plainText, string password, AlgorithmType algorithm)
        {
            byte[] salt = GenerateRandomBytes(SaltSize);
            byte[] key = GenerateSlowKey(password, salt);

            int ivSize = GetIvSize(algorithm);
            byte[] iv = GenerateRandomBytes(ivSize);

            byte[] encryptedBytes = algorithm switch
            {
                AlgorithmType.AES_CBC => EncryptAesCbc(plainText, key, iv),
                AlgorithmType.AES_GCM => EncryptAesGcm(plainText, key, iv),
                AlgorithmType.ChaCha20_Poly1305 => EncryptChaCha20Poly1305(plainText, key, iv),
                _ => throw new ArgumentException("Unsupported algorithm")
            };

            byte[] result = CombineIvAndCiphertext(iv, encryptedBytes);

            return (Convert.ToBase64String(result), Convert.ToBase64String(salt), algorithm);
        }

        public static string Decrypt(string cipherText, string password, string saltBase64, AlgorithmType algorithm)
        {
            byte[] combinedBytes = Convert.FromBase64String(cipherText);
            byte[] salt = Convert.FromBase64String(saltBase64);

            int ivSize = GetIvSize(algorithm);
            byte[] iv = ExtractIv(combinedBytes, ivSize);
            byte[] encryptedBytes = ExtractCiphertext(combinedBytes, ivSize);
            byte[] key = GenerateSlowKey(password, salt);

            return algorithm switch
            {
                AlgorithmType.AES_CBC => DecryptAesCbc(encryptedBytes, key, iv),
                AlgorithmType.AES_GCM => DecryptAesGcm(encryptedBytes, key, iv),
                AlgorithmType.ChaCha20_Poly1305 => DecryptChaCha20Poly1305(encryptedBytes, key, iv),
                _ => throw new ArgumentException("Unsupported algorithm")
            };
        }

        private static int GetIvSize(AlgorithmType algorithm)
        {
            return algorithm switch
            {
                AlgorithmType.AES_CBC => IvSizeAesCbc,
                AlgorithmType.AES_GCM => IvSizeAesGcm,
                AlgorithmType.ChaCha20_Poly1305 => IvSizeChaCha20,
                _ => throw new ArgumentException("Unsupported algorithm")
            };
        }

        private static byte[] GenerateRandomBytes(int size)
        {
            byte[] bytes = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        private static byte[] GenerateSlowKey(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA512);
            return pbkdf2.GetBytes(KeySize);
        }

        private static byte[] CombineIvAndCiphertext(byte[] iv, byte[] ciphertext)
        {
            byte[] result = new byte[iv.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
            return result;
        }

        private static byte[] ExtractIv(byte[] combined, int ivSize)
        {
            byte[] iv = new byte[ivSize];
            Buffer.BlockCopy(combined, 0, iv, 0, ivSize);
            return iv;
        }

        private static byte[] ExtractCiphertext(byte[] combined, int ivSize)
        {
            byte[] ciphertext = new byte[combined.Length - ivSize];
            Buffer.BlockCopy(combined, ivSize, ciphertext, 0, ciphertext.Length);
            return ciphertext;
        }

        // AES-CBC
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

        // AES-GCM
        private static byte[] EncryptAesGcm(string plainText, byte[] key, byte[] iv)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[plainBytes.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(iv, plainBytes, ciphertext, tag);

            return CombineCiphertextAndTag(ciphertext, tag);
        }

        private static string DecryptAesGcm(byte[] encryptedData, byte[] key, byte[] iv)
        {
            byte[] ciphertext = ExtractCiphertextWithoutTag(encryptedData);
            byte[] tag = ExtractTag(encryptedData, ciphertext.Length);
            byte[] plainBytes = new byte[ciphertext.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(iv, ciphertext, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        // ChaCha20-Poly1305
        private static byte[] EncryptChaCha20Poly1305(string plainText, byte[] key, byte[] iv)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[plainBytes.Length];

            using var chacha = new ChaCha20Poly1305(key);
            chacha.Encrypt(iv, plainBytes, ciphertext, tag);

            return CombineCiphertextAndTag(ciphertext, tag);
        }

        private static string DecryptChaCha20Poly1305(byte[] encryptedData, byte[] key, byte[] iv)
        {
            byte[] ciphertext = ExtractCiphertextWithoutTag(encryptedData);
            byte[] tag = ExtractTag(encryptedData, ciphertext.Length);
            byte[] plainBytes = new byte[ciphertext.Length];

            using var chacha = new ChaCha20Poly1305(key);
            chacha.Decrypt(iv, ciphertext, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        private static byte[] CombineCiphertextAndTag(byte[] ciphertext, byte[] tag)
        {
            byte[] result = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
            return result;
        }

        private static byte[] ExtractCiphertextWithoutTag(byte[] encryptedData)
        {
            byte[] ciphertext = new byte[encryptedData.Length - TagSize];
            Buffer.BlockCopy(encryptedData, 0, ciphertext, 0, ciphertext.Length);
            return ciphertext;
        }

        private static byte[] ExtractTag(byte[] encryptedData, int ciphertextLength)
        {
            byte[] tag = new byte[TagSize];
            Buffer.BlockCopy(encryptedData, ciphertextLength, tag, 0, TagSize);
            return tag;
        }
    }
}