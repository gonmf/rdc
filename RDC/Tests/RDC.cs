using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RDC;

namespace Tests
{
    [TestClass]
    public class Rdc
    {
        const int Timeout = 3000;
        [TestMethod]
        public void DiffTransferPartial()
        {
            string path1 = Shared.TempFile();
            string path2 = Shared.TempFile();
            try
            {
                int port = new Random().Next(0x0401, 0xfffe);
                // Create and fill sender and rcvr file
                var rng = new RNGCryptoServiceProvider();
                var bytes = new byte[1024];
                using (var stream = File.OpenWrite(path1))
                {
                    for (int i = 0; i < 3 * 1024; ++i)
                    {
                        rng.GetBytes(bytes);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                using (var stream = File.OpenWrite(path2))
                {

                    stream.Write(bytes, 0, bytes.Length);
                    for (int i = 0; i < 2 * 1024; ++i)
                    {
                        rng.GetBytes(bytes);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                var rcvr2 = new Thread(() =>
                {
                    var requestListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    requestListener.Bind(new IPEndPoint(IPAddress.Any, port));
                    requestListener.Listen(64);
                    Socket socket = requestListener.Accept();
                    socket.SendTimeout = socket.ReceiveTimeout = Timeout;
                    using (var stream = new NetworkStream(socket))
                    {
                        FileReceiver.Receive(stream, path2);
                    }
                });
                rcvr2.Start();
                var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket2.Connect("localhost", port);
                socket2.SendTimeout = socket2.ReceiveTimeout = Timeout;
                using (var stream2 = new NetworkStream(socket2))
                {
                    FileSender.Send(stream2, path1);
                }
                rcvr2.Join();
                // Test files contents
                var hashFunction = new MD5CryptoServiceProvider();
                byte[] n1, n2;
                using (FileStream stream = File.OpenRead(path1))
                {
                    n1 = hashFunction.ComputeHash(stream);
                }
                using (FileStream stream = File.OpenRead(path2))
                {
                    n2 = hashFunction.ComputeHash(stream);
                }
                Assert.IsTrue(n1.AreEqual(n2));
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
            finally
            {
                File.Delete(path1);
                File.Delete(path2);
            }
        }
        [TestMethod]
        public void DiffTransferRandomSmaller()
        {
            DiffTransferRandomTest(1, 3);
        }
        [TestMethod]
        public void DiffTransferRandomBigger()
        {
            DiffTransferRandomTest(4, 2);
        }
        public void DiffTransferRandomTest(int senderSizMiB, int rcvrSizMiB)
        {
            string path1 = Shared.TempFile();
            string path2 = Shared.TempFile();
            try
            {
                int port = new Random().Next(0x0401, 0xfffe);
                // Create and fill sender and rcvr file
                var rng = new RNGCryptoServiceProvider();
                var bytes = new byte[1024];
                using (var stream = File.OpenWrite(path1))
                {
                    for (int i = 0; i < senderSizMiB * 1024; ++i)
                    {
                        rng.GetBytes(bytes);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                using (var stream = File.OpenWrite(path2))
                {
                    for (int i = 0; i < rcvrSizMiB * 1024; ++i)
                    {
                        rng.GetBytes(bytes);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                var rcvr2 = new Thread(() =>
                {
                    var requestListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    requestListener.Bind(new IPEndPoint(IPAddress.Any, port));
                    requestListener.Listen(64);
                    Socket socket = requestListener.Accept();
                    socket.SendTimeout = socket.ReceiveTimeout = Timeout;
                    using (var stream = new NetworkStream(socket))
                    {
                        FileReceiver.Receive(stream, path2);
                    }
                });
                rcvr2.Start();
                var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket2.Connect("localhost", port);
                socket2.SendTimeout = socket2.ReceiveTimeout = Timeout;
                using (var stream2 = new NetworkStream(socket2))
                {
                    FileSender.Send(stream2, path1);
                }
                rcvr2.Join();
                // Test files contents
                var hashFunction = new MD5CryptoServiceProvider();
                byte[] n1, n2;
                using (FileStream stream = File.OpenRead(path1))
                {
                    n1 = hashFunction.ComputeHash(stream);
                }
                using (FileStream stream = File.OpenRead(path2))
                {
                    n2 = hashFunction.ComputeHash(stream);
                }
                Assert.IsTrue(n1.AreEqual(n2));
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
            finally
            {
                File.Delete(path1);
                File.Delete(path2);
            }
        }
    }
}