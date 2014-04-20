﻿using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using NUnit.Framework;
using SevenPass.IO;
using SevenPass.IO.Models;

namespace SevenPass.Tests.IO
{
    [TestFixture]
    public class FileFormatTests
    {
        [Test]
        public async Task Decrypt_should_decrypt_content()
        {
            using (var input = TestFiles.Read("IO.Demo7Pass.kdbx"))
            {
                input.Seek(222);

                var masterSeed = CryptographicBuffer.DecodeFromHexString(
                    "2b4656399a5bdf9fdfe9e8705a34b6f484f9b1b940c3d7cfb7ffece3b634e0ae");
                var masterKey = CryptographicBuffer.DecodeFromHexString(
                    "87730050341ff55c46421f2f2a5f4e1e018d0443d19cacc8682f128f1874d0a4");
                var encryptionIV = CryptographicBuffer.DecodeFromHexString(
                    "f360c29e1a603a6548cfbb28da6fff50");

                var decrypted = await FileFormat.Decrypt(input,
                    masterSeed, masterKey, encryptionIV);
                var buffer = decrypted.ToArray(0, 32).AsBuffer();

                Assert.AreEqual(
                    "54347fe32f3edbccae1fc60f72c11dafd0a72487b315f9b174ed1073ed67a6e0",
                    CryptographicBuffer.EncodeToHexString(buffer));
            }
        }

        [Test]
        public async Task Headers_should_detect_1x_file_format()
        {
            using (var file = new InMemoryRandomAccessStream())
            {
                await file.WriteAsync(CryptographicBuffer
                    .DecodeFromHexString("03D9A29A65FB4BB5"));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNull(result.Headers);
                Assert.AreEqual(FileFormats.KeePass1x, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_new_format()
        {
            using (var file = new InMemoryRandomAccessStream())
            {
                // Schema: 4.01
                await file.WriteAsync(CryptographicBuffer
                    .DecodeFromHexString("03D9A29A67FB4BB501000400"));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNull(result.Headers);
                Assert.AreEqual(FileFormats.NewVersion, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_not_supported_files()
        {
            using (var file = new InMemoryRandomAccessStream())
            {
                await file.WriteAsync(
                    CryptographicBuffer.GenerateRandom(1024));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNull(result.Headers);
                Assert.AreEqual(FileFormats.NotSupported, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_old_format()
        {
            using (var file = new InMemoryRandomAccessStream())
            {
                // Schema; 2.01
                await file.WriteAsync(CryptographicBuffer
                    .DecodeFromHexString("03D9A29A67FB4BB501000200"));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNull(result.Headers);
                Assert.AreEqual(FileFormats.OldVersion, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_partial_support_format()
        {
            using (var database = TestFiles.Read("IO.Demo7Pass.kdbx"))
            using (var file = new InMemoryRandomAccessStream())
            {
                var buffer = WindowsRuntimeBuffer.Create(512);
                buffer = await database.ReadAsync(
                    buffer, 512, InputStreamOptions.None);

                await file.WriteAsync(buffer);
                file.Seek(8);

                // Schema; 3.Max
                await file.WriteAsync(CryptographicBuffer
                    .DecodeFromHexString("FFFF0300"));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNotNull(result.Headers);
                Assert.AreEqual(FileFormats.PartialSupported, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_pre_release_format()
        {
            using (var file = new InMemoryRandomAccessStream())
            {
                await file.WriteAsync(CryptographicBuffer
                    .DecodeFromHexString("03D9A29A66FB4BB5"));

                file.Seek(0);
                var result = await FileFormat.Headers(file);

                Assert.IsNull(result.Headers);
                Assert.AreEqual(FileFormats.OldVersion, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_detect_supported_format()
        {
            using (var input = TestFiles.Read("IO.Demo7Pass.kdbx"))
            {
                var result = await FileFormat.Headers(input);

                Assert.IsNotNull(result.Headers);
                Assert.AreEqual(FileFormats.Supported, result.Format);
            }
        }

        [Test]
        public async Task Headers_should_parse_fields()
        {
            using (var input = TestFiles.Read("IO.Demo7Pass.kdbx"))
            {
                var result = await FileFormat.Headers(input);

                var headers = result.Headers;
                Assert.IsNotNull(headers);

                Assert.IsTrue(headers.UseGZip);
                Assert.AreEqual(6000, headers.TransformRounds);

                Assert.AreEqual(
                    "2b4656399a5bdf9fdfe9e8705a34b6f484f9b1b940c3d7cfb7ffece3b634e0ae",
                    CryptographicBuffer.EncodeToHexString(headers.MasterSeed));
                Assert.AreEqual(
                    "9525f6992beb739cbaa73ae6e050627fcaff378d3cd6f6c232d20aa92f6d0927",
                    CryptographicBuffer.EncodeToHexString(headers.TransformSeed));
                Assert.AreEqual(
                    "f360c29e1a603a6548cfbb28da6fff50",
                    CryptographicBuffer.EncodeToHexString(headers.EncryptionIV));
                Assert.AreEqual(
                    "54347fe32f3edbccae1fc60f72c11dafd0a72487b315f9b174ed1073ed67a6e0",
                    CryptographicBuffer.EncodeToHexString(headers.StartBytes));
                Assert.AreEqual(
                    "5ba62e1b5d5dfbcb295ef3bd2b627e74b141d7db3e1959fce539342ba3762121",
                    CryptographicBuffer.EncodeToHexString(headers.Hash));
            }
        }
    }
}