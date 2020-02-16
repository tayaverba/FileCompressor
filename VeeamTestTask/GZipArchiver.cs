using System.IO.Compression;
using System.IO;
using System.Threading;
using System;

namespace VeeamTestTask
{
    public class GZipArchiver : IFileArchiver
    {
        private int ThreadCount => Environment.ProcessorCount - 2 > 0 ? Environment.ProcessorCount - 2 : 2;

        private ItemsCollection<byte[]> bytesToCompress;
        private ItemsCollection<byte[]> bytesToWrite;

        private int readIndex = 0;
        private int entryCount = 0;
        public int BlockSize { get; }
        public GZipArchiver(int blockSize = 1048576) //default block size 1 MB
        {
            BlockSize = blockSize;
            bytesToCompress = new ItemsCollection<byte[]>();
            bytesToWrite = new ItemsCollection<byte[]>();
        }
        
        public void Compress(string inputPath, string outputPath)
        {
            var t = DateTime.Now;
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    entryCount = (int)Math.Floor((double)(fsin.Length / BlockSize));
                    var threadToRead = new Thread(ReadBlocks);
                    threadToRead.Start(fsin);
                    Thread[] threadsToCompress = new Thread[ThreadCount];
                    for (int i = 0; i < ThreadCount; i++)
                    {
                        threadsToCompress[i] = new Thread(CompressBlocks);
                        threadsToCompress[i].Start();
                    }
                    var threadToWrite = new Thread(WriteBlocks);
                    threadToWrite.Start(fsout);
                    threadToRead.Join();
                    for (int i = 0; i < ThreadCount; i++)
                    {
                        threadsToCompress[i].Join();
                    }
                    threadToWrite.Join();
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
                byte[] currentBytes = new byte[BlockSize];
                buffer.CopyTo(currentBytes, 0);
                bytesToCompress.Add(readIndex, currentBytes);
                //Console.WriteLine("Read: " + readIndex);
                readIndex++;
            } while (len >= BlockSize);
        }
        private object lockObj = new object();
        private void CompressBlocks()
        {
            while (bytesToCompress.EntryCount < entryCount)
            {
                byte[] entryToCompress;
                if (bytesToCompress.Count > 0)
                {
                    bool success = false;
                    int current = 0;
                    lock (lockObj)
                    {
                        current = bytesToCompress.EntryCount;
                        success = bytesToCompress.TryGet(current, out entryToCompress);
                    }
                    if (success)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var stream = new DeflateStream(memoryStream, CompressionMode.Compress))
                            {
                                stream.Write(entryToCompress, 0, entryToCompress.Length);
                                stream.Flush();
                            }
                            //Console.WriteLine("Compress: " + current);
                            bytesToWrite.Add(current, memoryStream.ToArray());
                        }
                    }
                }
            }
        }
        private void WriteBlocks(object outputStream)
        {
            var fsout = outputStream as FileStream;
            while (bytesToWrite.EntryCount < entryCount)
            {
                byte[] entryToWrite;
                if (bytesToWrite.Count > 0)
                {
                    bool success = false;
                    int current = 0;
                    lock (lockObj)
                    {
                        current = bytesToWrite.EntryCount;
                        success = bytesToWrite.TryGet(current, out entryToWrite);
                    }
                    if (success)
                    {
                        //Console.WriteLine("Write: " + current);
                        var size = entryToWrite.Length;
                        var sizeBytes = BitConverter.GetBytes(size);
                        fsout.Write(sizeBytes, 0, sizeBytes.Length);
                        fsout.Write(entryToWrite, 0, entryToWrite.Length);
                    }
                }                
            }
        }
        public void Decompress(string inputPath, string outputPath)
        {
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    int readBytesCount = 0;
                    do
                    {
                        byte[] sizeBuf = new byte[4];
                        readBytesCount = fsin.Read(sizeBuf, 0, 4);
                        if (readBytesCount > 0)
                        {
                            var entrySize = BitConverter.ToInt32(sizeBuf, 0);
                            byte[] buf = new byte[entrySize];
                            readBytesCount = fsin.Read(buf, 0, entrySize);
                            using (var memoryStream = new MemoryStream(buf))
                            {
                                using (var stream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                                {
                                    int nRead = 0;
                                    do
                                    {
                                        var readBytes = new byte[1024];
                                        nRead = stream.Read(readBytes, 0, 1024);
                                        fsout.Write(readBytes, 0, nRead);
                                    }
                                    while (nRead > 0);
                                    stream.Flush();
                                }
                            }
                        }
                    }
                    while (readBytesCount > 0);
                }
            }
        }
    }
    }
