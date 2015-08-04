namespace RDC
{
    class Chunk
    {
        public bool Found; // Chunk is found in old file
        public int NewStart; // Chunk position in new file
        public int OldStart; // Chunk positivon in old file (if Found = true)
        public int Length; // Length of chunk in bytes
        public byte[] StrongHash; // Strong hash of chunk
    }
}
