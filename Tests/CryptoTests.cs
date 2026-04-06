using System;
using System.Text;
using MyCrypto;
using TestsFramework.Assert;
using TestsFramework.Attributes;

namespace Tests
{
    [TestClass]
    class CryptoTests
    {
        private const string EnglishAlphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const string RussianAlphabet = @"АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        [Test]
        [Priority(2)]
        [Arguments("abcdfgdefgfffffqqw", 1000, SlowEncryptor.AlgorithmType.AES_CBC)]
        [Arguments("simplepassword", 5000, SlowEncryptor.AlgorithmType.AES_CBC)]
        [Arguments("12345678", 100, SlowEncryptor.AlgorithmType.AES_CBC)]
        public static void EncryptionDecryptionTest_AesCbc(string password, int textLength, SlowEncryptor.AlgorithmType algorithm)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, algorithm);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(1)]
        [Arguments("abcdfgdefgfffffqqw", 1000, SlowEncryptor.AlgorithmType.AES_GCM)]
        [Arguments("complex_password_123!", 5000, SlowEncryptor.AlgorithmType.AES_GCM)]
        [Arguments("", 100, SlowEncryptor.AlgorithmType.AES_GCM)]
        public static void EncryptionDecryptionTest_AesGcm(string password, int textLength, SlowEncryptor.AlgorithmType algorithm)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, algorithm);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(1)]
        [Arguments("abcdfgdefgfffffqqw", 1000, SlowEncryptor.AlgorithmType.ChaCha20_Poly1305)]
        [Arguments("chacha_password", 5000, SlowEncryptor.AlgorithmType.ChaCha20_Poly1305)]
        [Skip("Алгоритм шифрования не поддерживается")]
        public static void EncryptionDecryptionTest_ChaCha20(string password, int textLength, SlowEncryptor.AlgorithmType algorithm)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, algorithm);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(2)]
        [Arguments("русскийпароль", 500, "Привет мир!")]
        [Arguments("пароль123", 1000, "Тестовое сообщение на русском")]
        [Arguments("p@ssw0rd", 2000, "Mixed english and русский text")]
        public static void EncryptionDecryptionTest_RussianText(string password, int textLength, string baseText)
        {
            string plainText = baseText + GenerateRandomText(RussianAlphabet, textLength);

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(3)]
        [Arguments("short", 10)]
        [Arguments("medium_length_password", 1000)]
        [Arguments("medium_length_password", 2000)]
        [Arguments("medium_length_password", 4000)]
        [Arguments("medium_length_password", 5000)]
        [Arguments("medium_length_password", 6000)]
        [Arguments("very_long_password_that_should_work_fine_with_the_encryptor", 10000)]
        public static void EncryptionDecryptionTest_DifferentPasswordLengths(string password, int textLength)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(3)]
        [Arguments(0)]
        [Arguments(1)]
        [Arguments(100)]
        public static void EncryptionDecryptionTest_EmptyAndSmallTexts(int textLength)
        {
            string plainText = textLength == 0 ? "" : GenerateRandomText(EnglishAlphabet, textLength);
            string password = "testpassword";

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [MaxTime(1000)]
        [Arguments(100, 100)]
        [Arguments(500, 50)]
        [Arguments(1000, 10)]
        [Arguments(1000, 10)]
        [Arguments(1000, 20)]
        [Arguments(1000, 30)]
        [Arguments(1000, 40)]
        [Arguments(1000, 44)]
        [Arguments(1000, 10)]
        [Arguments(1000, 11)]
        public static void PerformanceTest(int textLength, int iterations)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);
            string password = "perftest";

            for (int i = 0; i < iterations; i++)
            {
                var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
                string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);
                Assert.AreEqual(plainText, decryptedText);
            }
        }


        [Test]
        [Arguments(100, 100)]
        [Arguments(500, 500)]
        [Arguments(1000, 10)]
        [Arguments(1000, 10)]
        [Arguments(1000, 10)]
        [Arguments(1000, 20)]
        [Arguments(1000, 30)]
        [Arguments(1000, 40)]
        [Arguments(1000, 10)]
        [Arguments(1000, 11)]
        [Arguments(1000, 11)]
        [Arguments(1000, 12)]
        [Arguments(1000, 33)]
        [Arguments(1000, 33)]
        [Arguments(1000, 44)]
        [Arguments(1000, 10)]
        [Arguments(1000, 11)]
        [Arguments(1000, 10)]
        [Arguments(1000, 10)]
        [Arguments(1000, 10)]
        [Arguments(1000, 20)]
        [Arguments(1000, 30)]
        [Arguments(1000, 40)]
        [Arguments(1000, 44)]
        public static void PerformanceTest_2(int textLength, int iterations)
        {
            string plainText = GenerateRandomText(EnglishAlphabet, textLength);
            string password = "perftest";

            for (int i = 0; i < iterations; i++)
            {
                var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
                string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algo);
                Assert.AreEqual(plainText, decryptedText);
            }
        }

        [Test]
        public static void ExceptionTest_InvalidAlgorithm()
        {
            int invalidAlgorithm = 777;

            Assert.Throws<ArgumentException>(() =>
            {
                SlowEncryptor.Encrypt("test text", "password", (SlowEncryptor.AlgorithmType)invalidAlgorithm);
            });
        }

        [Test]
        [Arguments("wrongpassword")]
        [Skip("Тест временно отключен")]
        public static void DecryptWithWrongPasswordTest(string wrongPassword)
        {
            string plainText = "secret message";
            string correctPassword = "correctpassword";

            var (cipherText, salt, algo) = SlowEncryptor.Encrypt(plainText, correctPassword, SlowEncryptor.AlgorithmType.AES_CBC);

            Assert.Throws<Exception>(() =>
            {
                SlowEncryptor.Decrypt(cipherText, wrongPassword, salt, algo);
            });
        }

        private static string GenerateRandomText(string alphabet, long length)
        {
            var builder = new StringBuilder();
            var random = new Random();

            for (long i = 0; i < length; i++)
            {
                builder.Append(alphabet[random.Next() % alphabet.Length]);
            }

            return builder.ToString();
        }
    }
}