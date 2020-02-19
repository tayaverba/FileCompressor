﻿using System.IO.Compression;
using System.IO;
using System.Threading;
using System;

namespace VeeamTestTask
{
    public class GZipArchiver : IFileArchiver
    {
        private int _maxSize = 100;
        private ItemsCollection<byte[]> _bytesToProcess;
        private ItemsCollection<byte[]> _bytesToWrite;

        private int _blocksCount = 0;
        private static object _lockObj = new object();
        public int BlockSize { get; }
        private int ThreadCount => Environment.ProcessorCount - 2 > 0 ? Environment.ProcessorCount - 2 : 1;
        public GZipArchiver(int blockSize = 1048576) //default block size 1 MB
        {
            BlockSize = blockSize;
            _bytesToProcess = new ItemsCollection<byte[]>(_maxSize);
            _bytesToWrite = new ItemsCollection<byte[]>(_maxSize);
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
                        int entrySize = ReadBlockSize(fsin);
                        if (entrySize > 0)
                        {
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
                        else { break; }
                    }
                    while (readBytesCount > 0);
                }
            }
        }
        public void Compress(string inputPath, string outputPath)
        {
            var t = DateTime.Now;
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    _blocksCount = (int)Math.Floor((double)(fsin.Length / BlockSize));
                    _blocksCount = _blocksCount > 0 ? _blocksCount : 1;
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
            int readIndex = 0;
            var fsin = inputStream as FileStream;
            byte[] buffer = new byte[BlockSize];
            int position = 0;
            int readLength;
            do
            {
                readLength = fsin.Read(buffer, 0, BlockSize);
                position += readLength;
                _bytesToProcess.Add(readIndex, buffer);
                if (_bytesToProcess.Count > _maxSize)
                    _bytesToProcess.WaitUntilCountBecomeLess();
                //Console.WriteLine("Read: " + readIndex);
                readIndex++;
            } while (readLength >= BlockSize);
        }
        private void CompressBlocks()
        {
            var current = _bytesToProcess.EntryCount;
            while (current < _blocksCount)
            {                
                if (_bytesToProcess.Count > 0)
                {
                    byte[] entryToCompress;
                    bool success = false;
                    lock (_lockObj)
                    {
                        current = _bytesToProcess.EntryCount;
                        //Console.WriteLine("Compress: " + current);
                        success = _bytesToProcess.TryGet(current, out entryToCompress);
                    }
                    if (success)
                    {
                        byte[] bytes;
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var stream = new DeflateStream(memoryStream, CompressionMode.Compress, false))
                            {
                                stream.Write(entryToCompress, 0, entryToCompress.Length);
                                stream.FlushAsync();
                            }
                            //Console.WriteLine("Compress: " + current);
                            bytes = memoryStream.ToArray();
                            memoryStream.FlushAsync();                            
                        }
                        if (_bytesToWrite.Count > _maxSize)
                            _bytesToWrite.WaitUntilCountBecomeLess();
                        _bytesToWrite.Add(current, bytes);
                    }
                }
                current = _bytesToProcess.EntryCount;
            }
        }
        private void WriteBlocks(object outputStream)
        {
            var fsout = outputStream as FileStream;
            var current = _bytesToWrite.EntryCount;
            while (current < _blocksCount)
            {
                byte[] entryToWrite;
                if (_bytesToWrite.Count > 0)
                {
                    bool bytesGot = false;
                    lock (_lockObj)
                    {
                        current = _bytesToWrite.EntryCount;
                        bytesGot = _bytesToWrite.TryGet(current, out entryToWrite);
                    }
                    if (bytesGot)
                    {
                        //Console.WriteLine("Write: " + current);
                        WriteBlockSize(fsout, entryToWrite.Length);
                        fsout.Write(entryToWrite, 0, entryToWrite.Length);
                    }
                }
                current = _bytesToWrite.EntryCount;
            }
        }
        private void WriteBlockSize(FileStream stream, int length)
        {
            var sizeBytes = BitConverter.GetBytes(length);
            stream.Write(sizeBytes, 0, sizeBytes.Length);
        }

        private int ReadBlockSize(FileStream stream)
        {
            byte[] sizeBuf = new byte[4];
            var readBytesCount = stream.Read(sizeBuf, 0, 4);
            if (readBytesCount > 0)
            {
                return BitConverter.ToInt32(sizeBuf, 0);
            }
            return 0;
        }
    }
}
