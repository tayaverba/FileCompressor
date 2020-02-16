using System.IO.Compression;
using System.IO;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace VeeamTestTask
{
    public class GZipArchiver : IFileArchiver
    {
        private int ThreadCount => Environment.ProcessorCount - 2 > 0 ? Environment.ProcessorCount - 2 : 2;

        private ConcurrentQueue<byte[]> bytesToCompress;

        public int BlockSize { get; }
        public GZipArchiver(int blockSize = 1048576) //default block size 1 MB
        {
            BlockSize = blockSize;
            bytesToCompress = new ConcurrentQueue<byte[]>();
        }
        private int entryCount = 0;
        private int entryCompressedCount = 0;
        public void Compress(string inputPath, string outputPath)
        {
            var t = DateTime.Now;
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (var cs = new GZipStream(fsout, CompressionMode.Compress, true))
                    {
                        entryCount = (int)Math.Floor((double)(fsin.Length / BlockSize));
                        var threadToRead = new Thread(ReadBlocks);
                        threadToRead.Start(fsin);
                        var threadToCompress = new Thread(CompressBlocks);
                        threadToCompress.Start(cs);
                        threadToRead.Join();
                        threadToCompress.Join();
                        cs.Flush();
                    }
                }
            }
            Console.WriteLine("Time: " + DateTime.Now.Subtract(t).TotalMilliseconds);
        }

        private void ReadBlocks(object inputStream)
        {
            var fsin = inputStream as FileStream;
            byte[] buffer = new byte[BlockSize];
            int pos = 0;
            int len;
            do
            {
                len = fsin.Read(buffer, 0, BlockSize);
                pos += len;
                byte[] p = new byte[BlockSize];
                buffer.CopyTo(p, 0);
                if (bytesToCompress.Count > 10)
                    Thread.Sleep(100);
                bytesToCompress.Enqueue(p);
            } while (len >= BlockSize);
        }
        private void CompressBlocks(object zipStream)
        {
            var cs = zipStream as GZipStream;
            var currentEntries = entryCompressedCount;
            while (currentEntries < entryCount)
            {
                    byte[] buf;
                    if (bytesToCompress.TryDequeue(out buf))
                    {
                        cs.Write(buf, 0, buf.Length);
                        entryCompressedCount++;
                        currentEntries = entryCompressedCount;
                    }
            }
        }
        public void Decompress(string inputPath, string outputPath)
        {
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (Stream csStream = new GZipStream(fsin, CompressionMode.Decompress))
                    {
                        byte[] buffer = new byte[1024];
                        int nRead;
                        while ((nRead = csStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fsout.Write(buffer, 0, nRead);
                        }
                    }
                }
            }
        }
    }
    }
