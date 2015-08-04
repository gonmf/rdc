using System;
using System.IO;
namespace RDC
{
    /// <summary>
    /// Alternative rolling checksum algorithm suggested by Richard Taylor, Rittwik Jana and
    /// Mark Grigg in (1998) "Checksum Testing of Remote Synchronisation Tool",
    /// DSTO Electronics and Surveillance Research Laboratory.
    /// </summary>
    class C3C4TaylorsRollingChecksum
    {
        private int _in; // Oldest byte in window
        private int _c3; // C3 16 bit cheksum
        private int _c4; // C4 16 bit checksum
        private readonly Stream _stream; // Readable stream
        private readonly byte[] _window; // Window of L size
        /// <summary>
        /// Rolling checksum function constructor, needs an underlying open, readable stream
        /// </summary>
        /// <param name="stream">Open stream</param>
        /// <param name="windowSize">Window size to roll</param>
        public C3C4TaylorsRollingChecksum(Stream stream, int windowSize)
        {
            if (stream == null || windowSize < 1)
                throw new ArgumentException();
            _stream = stream;
            _window = new byte[windowSize];
            Array.Clear(_window, 0, _window.Length);
        }
        /// <summary>
        /// Rolling hash function that generates the hash of the underlying data source
        /// advancing one byte. Does not <i>seek</i>.
        /// </summary>
        /// <returns>32-bit hash</returns>
        public int Slide()
        {
            int rd = _stream.ReadByte();
            if (rd == -1)
                throw new EndOfStreamException();
            _c3 = ((_c3 << 5) + rd - _window[_in]) & 0xffff;
            _c4 = ((_c4 << 7) + rd - _window[_in]) & 0xffff;
            _window[_in++] = (byte)rd;
            if (_in == _window.Length)
                _in = 0;
            return _c3 | (_c4 << 16);
        }
    }
}
