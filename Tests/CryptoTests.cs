using System;
using System.Collections.Generic;
using System.Text;
using MyCrypto;
using TestsFramework.Assert;
using TestsFramework.Attributes;

namespace Tests
{
    [TestClass]
    class CryptoTests
    {
        [Test]
        [Priority(2)]
        public static void Algo1Test()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder plainTextBuilder = new();
            var rand = new Random();
            for (int i = 0; i < 37777777; i++)
            {
                plainTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "abcdfgdefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                plainTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(plainTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        [Priority(1)]
        public static void Algo2Test()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder plainTextBuilder = new();
            var rand = new Random();
            for (int i = 0; i < 3777777; i++)
            {
                plainTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "abcdfgodefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                plainTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(plainTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        [Priority(1)]
        public static void Algo3Test()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder plainTextBuilder = new();
            var rand = new Random();
            for (int i = 0; i < 37777777; i++)
            {
                plainTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "abcdfgdefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                plainTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.ChaCha20_Poly1305);
            Assert.AreEqual(plainTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }



        [Test]
        [MaxTime(777)]
        public static void MaxExecutionTimeTest_WontFail()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder cipherTextBuilder = new();
            var rand = new Random();
            for (System.Int128 i = 0; i < 7777; i++)
            {
                cipherTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "abcdefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                cipherTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(cipherTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        [MaxTime(7777)]
        public static void LongTest1()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder cipherTextBuilder = new();
            var rand = new Random();
            for (System.Int128 i = 0; i < 7777; i++)
            {
                cipherTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "dfgdfgabcdsddhgfsefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                cipherTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(cipherTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        [MaxTime(2777)]
        public static void LongTest2()
        {
            string alphabet = @"АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            StringBuilder cipherTextBuilder = new();
            var rand = new Random();
            for (System.Int128 i = 0; i < 77777777; i++)
            {
                cipherTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "dfgdfgabcdsddhgfsefgfffffqqw";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                cipherTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(cipherTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        [MaxTime(3337)]
        public static void LongTest3()
        {
            string alphabet = @"АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            StringBuilder cipherTextBuilder = new();
            var rand = new Random();
            for (System.Int128 i = 0; i < 77777777; i++)
            {
                cipherTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
            }

            string password = "пароль";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                cipherTextBuilder.ToString(),
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(cipherTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }

        [Test]
        public static void ExceptionTest()
        {
            int cryptoType = 777;

            Assert.Throws<ArgumentException>
                (
                    () =>
                    {
                       _ = SlowEncryptor.Encrypt("dssdsdgdsfesfydwgiuroesdrhpibfdf",
                            "pass", (SlowEncryptor.AlgorithmType)cryptoType);
                    }
                );
        }

        [Test]
        [MaxTime(3777)]
        [Priority(0)]
        public static void MaxExecutionTimeTest_WillFail()
        {
            string alphabet = @"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder cipherTextBuilder = new();
            var rand = new Random();
            for (System.Int128 i = 0; i < 777666777666; i++)
            {
                cipherTextBuilder.Append(alphabet[rand.Next() % alphabet.Length]);
                if (i % 1024  == 0)
                {
                  //  Console.WriteLine("sdf");
                }
            }

            string password = "abcdefg";
            (string plainText, string salt, SlowEncryptor.AlgorithmType algoType) = SlowEncryptor.Encrypt(
                cipherTextBuilder.ToString(), 
                password, SlowEncryptor.AlgorithmType.AES_CBC);
            Assert.AreEqual(cipherTextBuilder.ToString(), SlowEncryptor.Decrypt(plainText, password, salt, algoType));
        }
    }
}
