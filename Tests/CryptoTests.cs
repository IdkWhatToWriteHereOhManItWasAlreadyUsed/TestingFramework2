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
        private const string DefaultPassword = "abcdfgdefgfffffqqw";
        private const string ShortPassword = "abcdefg";
        private const string RussianPassword = "пароль";

        [Test]
        [Priority(2)]
        public static void Algo1Test()
        {
            string plainText = GenerateRandomText(EnglishAlphabet, 37777777);
            string password = DefaultPassword;

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(1)]
        public static void Algo2Test()
        {
            string plainText = GenerateRandomText(EnglishAlphabet, 3777777);
            string password = "abcdfgodefgfffffqqw";

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [Priority(1)]
        public static void Algo3Test()
        {
            return;

            string plainText = GenerateRandomText(EnglishAlphabet, 37777777);
            string password = DefaultPassword;

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.ChaCha20_Poly1305);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [MaxTime(800)]
        public static void MaxExecutionTimeTest_WontFail()
        {
            string plainText = GenerateRandomText(EnglishAlphabet, 7777);
            string password = "abcdefgfffffqqw";

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [MaxTime(8000)]
        public static void LongTest1()
        {
            string plainText = GenerateRandomText(EnglishAlphabet, 7777);
            string password = "dfgdfgabcdsddhgfsefgfffffqqw";

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [MaxTime(3000)]
        public static void LongTest2()
        {
            string plainText = GenerateRandomText(RussianAlphabet, 77777777);
            string password = "dfgdfgabcdsddhgfsefgfffffqqw";

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        [MaxTime(3500)]
        public static void LongTest3()
        {
            string plainText = GenerateRandomText(RussianAlphabet, 77777777);
            string password = RussianPassword;

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
        }

        [Test]
        public static void ExceptionTest()
        {
            int invalidAlgorithm = 777;

            Assert.Throws<ArgumentException>(() =>
            {
                SlowEncryptor.Encrypt("dssdsdgdsfesfydwgiuroesdrhpibfdf", "pass", (SlowEncryptor.AlgorithmType)invalidAlgorithm);
            });
        }

        [Test]
        [MaxTime(4000)]
        [Priority(0)]
        public static void MaxExecutionTimeTest_WillFail()
        {
            string plainText = GenerateRandomText(EnglishAlphabet, 777666777666);
            string password = ShortPassword;

            var (cipherText, salt, algorithm) = SlowEncryptor.Encrypt(plainText, password, SlowEncryptor.AlgorithmType.AES_CBC);
            string decryptedText = SlowEncryptor.Decrypt(cipherText, password, salt, algorithm);

            Assert.AreEqual(plainText, decryptedText);
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