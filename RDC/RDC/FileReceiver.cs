using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace RDC
{
    /// <summary>
    /// This algorithm is a variant of the third (and final) deltas transfer algorithm described in
    /// the PhD thesis of the RSYNC application author. This algorihtm uses the MD5 cryptografic hash
    /// function and a rolling checksum hash function. Each file transfer takes makes a maximum of
    /// two passes on the original file. This file transfer algorithm is <b>not</b> compatible with
    /// the RSYNC algorihtm described in the paper. Maximum file size of 2GiB.
    /// 
    /// This algorithm does a lot of work in the receiving end, in order to avoid timeouts the network
    /// stream timeout values are temporally increased. The network stream provided must support this
    /// behaviour for the transfer of bigger files.
    /// </summary>
    public static class FileReceiver
    {
        private const int BlockSize = 5205; // RSYNC fixed block size
        private const int BufferSize = 65535; // Actual useful buffer size
        private static readonly MD5CryptoServiceProvider Md5 = new MD5CryptoServiceProvider();
        /// <summary>
        /// RSYNC file transfer with exchange of delta representatives (receiving end)
        /// </summary>
        /// <param name="stream">Connection stream</param>
        /// <param name="path">Path of file to transfer, must exist</param>
        public static void Receive(Stream stream, string path)
        {
            // Receive new file size
            int fileSizeNew = stream.ReadInt();
            int prevReadTimeout = stream.ReadTimeout;
            int prevWriteTimeout = stream.WriteTimeout;
            string tempPath = Shared.TempFile();
            var fileSizeOld = (int)new FileInfo(path).Length;
            var buffer = new byte[BufferSize];
            try
            {
                using (var fileStreamOld = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    Dictionary<int, List<Chunk>> remoteChunks = ReceiveChunkInformation(stream, fileSizeNew);
                    stream.ReadTimeout = stream.WriteTimeout = fileSizeNew;
                    List<Chunk> chunks = DiscoverChunksInLocalFile(remoteChunks, new C3C4TaylorsRollingChecksum(fileStreamOld, BlockSize), fileStreamOld,
                                                                    fileSizeOld, buffer);
                    stream.ReadTimeout = prevReadTimeout;
                    stream.WriteTimeout = prevWriteTimeout;
                    UniteCloseChunks(chunks);
                    int chunksToRequest = AddChunksNotFoundInfo(chunks);
                    chunksToRequest += AddChunkNotFoundTail(chunks, fileSizeNew);
                    using (var fileStreamNew = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        SendAndReceiveChunksAndRebuildFile(stream, chunks, fileStreamNew, fileStreamOld, chunksToRequest, buffer);
                    }
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                stream.ReadTimeout = prevReadTimeout;
                stream.WriteTimeout = prevWriteTimeout;
            }
            // Verify file hash is as expected
            byte[] fileReceivedHash;
            using (var fs = File.OpenRead(tempPath))
            {
                fileReceivedHash = Md5.ComputeHash(fs);
            }
            // Receive hash of whole file
            var fileHash = new byte[16];
            stream.ForceRead(fileHash, 0, 16);
            if (!fileReceivedHash.AreEqual(fileHash))
            {
                File.Delete(tempPath);
                throw new Exception("File transfer error: file hash test failure");
            }
            // File transfer okay
            File.Delete(path);
            File.Move(tempPath, path);
        }
        private static Dictionary<int, List<Chunk>> ReceiveChunkInformation(Stream stream, int fileSizeNew)
        {
            int numChunks = (fileSizeNew / BlockSize) + (((fileSizeNew % BlockSize) == 0) ? 0 : 1);
            var remoteChunks = new Dictionary<int, List<Chunk>>();
            for (int i = 0; i < numChunks; ++i)
            {
                int weakHash = stream.ReadInt();
                var strongHash = new byte[16];
                stream.ForceRead(strongHash, 0, strongHash.Length);
                int length = stream.ReadInt();
                List<Chunk> sameWeakHashChunks;
                if (!remoteChunks.TryGetValue(weakHash, out sameWeakHashChunks))
                {
                    sameWeakHashChunks = new List<Chunk>();
                    remoteChunks.Add(weakHash, sameWeakHashChunks);
                }
                sameWeakHashChunks.Add(new Chunk
                {
                    Found = false,
                    Length = length,
                    NewStart = i * BlockSize,
                    OldStart = -1,
                    StrongHash = strongHash
                });
            }
            return remoteChunks;
        }
        private static List<Chunk> DiscoverChunksInLocalFile(Dictionary<int, List<Chunk>> remoteChunks, C3C4TaylorsRollingChecksum weakHashFunction, Stream fileStreamOld, int fileSizeOld, byte[] buffer)
        {
            var chunks = new List<Chunk>();
            int i = 0;
            while (i++ < fileSizeOld)
            {
                int weakHash = weakHashFunction.Slide();
                List<Chunk> sameWeakHashChunks;
                if (remoteChunks.TryGetValue(weakHash, out sameWeakHashChunks))
                {
                    byte[] strongHash = null;
                    foreach (var chunk in sameWeakHashChunks)
                    {
                        if (chunk.Found || i < chunk.Length)
                            continue;
                        if (strongHash == null)
                        {
                            fileStreamOld.Seek(i - chunk.Length, 0);
                            fileStreamOld.ForceRead(buffer, 0, chunk.Length);
                            strongHash = Md5.ComputeHash(buffer, 0, chunk.Length);
                            fileStreamOld.Seek(i, 0);
                        }
                        if (strongHash.AreEqual(chunk.StrongHash))
                        {
                            chunk.Found = true;
                            chunk.OldStart = i - chunk.Length;
                            chunks.Add(chunk);
                        }
                    }
                }
            }
            chunks.Sort((i1, i2) => i1.NewStart.CompareTo(i2.NewStart));
            return chunks;
        }
        private static void UniteCloseChunks(List<Chunk> chunks)
        {
            Chunk prevChunk = null;
            foreach (var chunk in chunks)
            {
                if (prevChunk != null && chunk.OldStart == prevChunk.OldStart + prevChunk.Length && chunk.NewStart == prevChunk.NewStart + prevChunk.Length)
                {
                    chunk.Length += prevChunk.Length;
                    chunk.NewStart = prevChunk.NewStart;
                    chunk.OldStart = prevChunk.OldStart;
                    prevChunk.Length = 0;
                }
                prevChunk = chunk;
            }
            chunks.RemoveAll(i => i.Length == 0);
        }
        private static int AddChunksNotFoundInfo(List<Chunk> chunks)
        {
            var toAdd = new List<Chunk>();
            if (chunks.Count > 0)
            {
                var c = chunks[0];
                if (c.NewStart > 0)
                    toAdd.Add(new Chunk { Found = false, Length = c.NewStart, NewStart = 0 });
            }
            Chunk prevChunk = null;
            foreach (var chunk in chunks)
            {
                if (prevChunk != null && chunk.NewStart > prevChunk.NewStart + prevChunk.Length)
                    toAdd.Add(new Chunk { Found = false, Length = chunk.NewStart - (prevChunk.NewStart + prevChunk.Length), NewStart = prevChunk.NewStart + prevChunk.Length });
                prevChunk = chunk;
            }
            chunks.AddRange(toAdd);
            return toAdd.Count;
        }
        private static int AddChunkNotFoundTail(List<Chunk> chunks, int fileSizeNew)
        {
            int restStart = 0;
            foreach (var chunk in chunks)
                if (chunk.NewStart + chunk.Length > restStart)
                    restStart = chunk.NewStart + chunk.Length;
            if (restStart < fileSizeNew)
            {
                chunks.Add(new Chunk { Found = false, Length = fileSizeNew - restStart, NewStart = restStart });
                return 1;
            }
            return 0;
        }
        private static void SendAndReceiveChunksAndRebuildFile(Stream stream, List<Chunk> chunks, Stream fileStreamNew, Stream fileStreamOld, int chunksToRequest, byte[] buffer)
        {
            chunksToRequest.WriteTo(stream);
            chunks.Sort((i1, i2) => i1.NewStart.CompareTo(i2.NewStart));
            foreach (var chunk in chunks)
            {
                if (chunk.Found) // From old file
                {
                    fileStreamOld.Seek(chunk.OldStart, 0);
                    fileStreamNew.Seek(chunk.NewStart, 0);
                    fileStreamOld.ReadWrite(fileStreamNew, chunk.Length, buffer);
                }
                else // From remote file
                {
                    chunk.NewStart.WriteTo(stream);
                    chunk.Length.WriteTo(stream);
                    fileStreamNew.Seek(chunk.NewStart, 0);
                    stream.ReadWrite(fileStreamNew, chunk.Length, buffer);
                }
            }
        }
    }
}
