using System.IO;
using System.Threading;

namespace RDC
{
    /// <summary>
    /// Shared utility functions
    /// </summary>
    public static class Shared
    {
        private static int _curr;
        /// <summary>
        /// Chooses a name for a new temporary file
        /// </summary>
        /// <returns>Full path</returns>
        public static string TempFile()
        {
            while (true)
            {
                int curr = Interlocked.Increment(ref _curr);
                string s = Path.GetTempPath() + "rdc" + curr + ".tmp";
                if (!File.Exists(s))
                    return s;
            }
        }
        /// <summary>
        /// Compares the content of two byte arrays; returns false
        /// (not equal) if null
        /// </summary>
        /// <param name="self">First array</param>
        /// <param name="b">Second array</param>
        /// <returns>True if same content</returns>
        public static bool AreEqual(this byte[] self, byte[] b)
        {
            if (b == null || self.Length != b.Length)
                return false;
            for (int i = 0; i < self.Length; ++i)
                if (self[i] != b[i])
                    return false;
            return true;
        }
        /// <summary>
        /// Blocking synchronous read, if less than the amount of bytes required is available then
        /// an exception is thrown; avoid using for big amounts of data
        /// </summary>
        /// <param name="stream">Open readable stream</param>
        /// <param name="buffer">Data buffer</param>
        /// <param name="offset">Offset in buffer</param>
        /// <param name="count">Number of bytes to read</param>
        public static void ForceRead(this Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int r = stream.Read(buffer, offset, count);
                if (r < 0)
                    throw new EndOfStreamException();
                offset += r;
                count -= r;
            }
        }
        /// <summary>
        /// Reads an integer from the stream
        /// </summary>
        /// <param name="stream">Open stream</param>
        /// <returns>Integer</returns>
        public static int ReadInt(this Stream stream)
        {
            var bytes = new byte[4];
            stream.ForceRead(bytes, 0, 4);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
        /// <summary>
        /// Writes an integer to the stream
        /// </summary>
        /// <param name="stream">Open stream</param>
        /// <param name="value">Integer</param>
        public static void WriteTo(this int value, Stream stream)
        {
            stream.WriteByte((byte)((value >> 24) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)((value >> 0) & 0xff));
        }
        /// <summary>
        /// Reads from a stream and writes to another a total of size bytes, reutilizing a buffer
        /// </summary>
        /// <param name="origin">Origin stream</param>
        /// <param name="dest">Destination stream</param>
        /// <param name="size">Total size to transfer</param>
        /// <param name="buffer">Buffer to be used</param>
        public static void ReadWrite(this Stream origin, Stream dest, long size, byte[] buffer)
        {
            while (size > 0)
            {
                long ss = size > buffer.Length ? buffer.Length : size;
                var si = (int)ss;
                si = origin.Read(buffer, 0, si);
                dest.Write(buffer, 0, si);
                size -= si;
            }
        }
    }
}
