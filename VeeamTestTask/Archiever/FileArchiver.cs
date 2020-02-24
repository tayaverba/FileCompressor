using System.IO.Compression;
using System.IO;
using System.Threading;
using System;
using System.Collections.Generic;

namespace VeeamTestTask
{
    /// <summary>
    /// Represents archiver that can compress files dividing it to blocks of fixed size 
    /// and compressing them by Deflate algorithm in parallel
    /// and decompress files compressed with this format
    /// </summary>
    public class FileArchiver : IFileArchiver
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private static object _lockObj = new object();
        
        private EntryContainer _bytesToProcess;
        private EntryContainer _bytesToWrite;
        private string _errorMessage = null;
        private int _blocksCount = 0;        
        private int ThreadCount => Environment.ProcessorCount;
        private List<Thread> ThreadPool = new List<Thread>();
        private int MaxEntriesToKeep { get; }
        /// <summary>
        /// Size of compressing block in bytes
        /// </summary>
        public int BlockSize { get; }
        /// <summary>
        /// Creates new instance of <see cref="FileArchiver"/> 
        /// </summary>
        /// <param name="blockSizeInBytes">Size of compressing block in bytes</param>
        /// <param name="maxEntriesToKeep">Count of entries to keep in memory in queues to read/write</param>
        public FileArchiver(int blockSizeInBytes = 1048576, int maxEntriesToKeep = 50)
        {
            BlockSize = blockSizeInBytes;
            MaxEntriesToKeep = maxEntriesToKeep;
            _bytesToProcess = new EntryContainer(MaxEntriesToKeep);
            _bytesToWrite = new EntryContainer(MaxEntriesToKeep);
        }
        private void Refresh()
        {
            ThreadPool.Clear();
            _bytesToProcess = new EntryContainer(MaxEntriesToKeep);
            _bytesToWrite = new EntryContainer(MaxEntriesToKeep);
        }
        private void HandleException(string errorMessage)
        {
            lock (_lockObj)
            {
                _errorMessage = errorMessage;
                cts.Cancel();
            }
        }
        public void Compress(string inputPath, string outputPath)
        {
            var t = DateTime.Now;
            Refresh();
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    _blocksCount = (int)Math.Ceiling(fsin.Length / (double)BlockSize);
                    WriteBlockSize(fsout, _blocksCount);
                    var threadToRead = new Thread(ReadDecompressedBlocks);
                    threadToRead.Start(fsin);
                    ThreadPool.Add(threadToRead);
                    for (int i = 0; i < ThreadCount - 2; i++)
                    {
                        var threadToCompress = new Thread(CompressBlocks);
                        threadToCompress.Start();
                        ThreadPool.Add(threadToCompress);
                    }
                    var threadToWrite = new Thread(WriteCompressedBlocks);
                    threadToWrite.Start(fsout);
                    ThreadPool.Add(threadToWrite);
                    foreach (var thread in ThreadPool)
                    {
                        thread.Join();
                    }
                    if (_errorMessage != null) { throw new InvalidOperationException(_errorMessage); };
                    fsout.Close();
                }
                fsin.Close();
            }
            Console.WriteLine("Time: " + DateTime.Now.Subtract(t).TotalMilliseconds);
        }
        private void ReadDecompressedBlocks(object inputStream)
        {
            try
            {
                int readIndex = 0;
                var fsin = inputStream as FileStream;
                int readLength;
                do
                {
                    byte[] buffer = new byte[BlockSize];
                    readLength = fsin.Read(buffer, 0, BlockSize);
                    if (readLength < BlockSize)
                    {
                        var buf = new byte[readLength];
                        for (int i = 0; i < buf.Length; i++)
                        {
                            buf[i] = buffer[i];
                        }
                        buffer = buf;
                    }
                    _bytesToProcess.Add(readIndex, buffer);
                    if (_bytesToProcess.Count > MaxEntriesToKeep)
                        _bytesToProcess.WaitUntilCountBecomeLess();
                    readIndex++;
                } while (readLength >= BlockSize && !cts.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                HandleException("Error while reading input file: " + ex.Message);
            }
        }
        private void CompressBlocks()
        {
            try
            {
                var current = _bytesToProcess.WithdrawalsCount;
                while (current < _blocksCount && !cts.IsCancellationRequested)
                {
                    if (_bytesToProcess.Count > 0)
                    {
                        byte[] entryToCompress;
                        bool success = false;
                        lock (_lockObj)
                        {
                            current = _bytesToProcess.WithdrawalsCount;
                            success = _bytesToProcess.TryGet(current, out entryToCompress);
                        }
                        if (success)
                        {
                            byte[] compressedBytes;
                            using (var memoryStream = new MemoryStream())
                            {
                                using (var stream = new DeflateStream(memoryStream, CompressionLevel.Optimal))
                                {
                                    stream.Write(entryToCompress, 0, entryToCompress.Length);
                                    stream.Close();
                                    compressedBytes = memoryStream.ToArray();
                                }
                            }
                            if (_bytesToWrite.Count > MaxEntriesToKeep)
                                _bytesToWrite.WaitUntilCountBecomeLess();
                            _bytesToWrite.Add(current, compressedBytes);
                        }
                    }
                    current = _bytesToProcess.WithdrawalsCount;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error while compressing input file: \n" + ex.Message);
            }
        }
        private void WriteCompressedBlocks(object outputStream)
        {
            try
            {
                var fsout = outputStream as FileStream;
                var current = _bytesToWrite.WithdrawalsCount;
                while (current < _blocksCount && !cts.IsCancellationRequested)
                {
                    byte[] entryToWrite;
                    if (_bytesToWrite.Count > 0)
                    {
                        bool bytesGot = false;
                        lock (_lockObj)
                        {
                            current = _bytesToWrite.WithdrawalsCount;
                            bytesGot = _bytesToWrite.TryGet(current, out entryToWrite);
                        }
                        if (bytesGot)
                        {
                            WriteBlockSize(fsout, entryToWrite.Length);
                            fsout.Write(entryToWrite, 0, entryToWrite.Length);
                        }
                    }
                    current = _bytesToWrite.WithdrawalsCount;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error while writing compressed file: " + ex.Message);
            }
        }
        private void WriteBlockSize(FileStream stream, int length)
        {
            var sizeBytes = BitConverter.GetBytes(length);
            stream.Write(sizeBytes, 0, sizeBytes.Length);
        }
        public void Decompress(string inputPath, string outputPath)
        {
            var t = DateTime.Now;
            Refresh();
            using (FileStream fsin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    _blocksCount = ReadBlockSize(fsin);
                    var threadToRead = new Thread(ReadCompressedBlocks);
                    threadToRead.Start(fsin);
                    ThreadPool.Add(threadToRead);
                    for (int i = 0; i < ThreadCount - 2; i++)
                    {
                        var threadToDecompress = new Thread(DecompressBlocks);
                        threadToDecompress.Start();
                        ThreadPool.Add(threadToDecompress);
                    }
                    var threadToWrite = new Thread(WriteDecompressedBlocks);
                    threadToWrite.Start(fsout);
                    ThreadPool.Add(threadToWrite);
                    foreach(var thread in ThreadPool)
                    {
                        thread.Join();
                    }
                    if (_errorMessage != null) { throw new InvalidOperationException(_errorMessage); };
                    fsout.Close();
                }
                fsin.Close();
            }
            Console.WriteLine("Time: " + DateTime.Now.Subtract(t).TotalMilliseconds);
        }    
        private void ReadCompressedBlocks(object inputStream)
        {
            try
            {
                int readIndex = 0;
                var fsin = inputStream as FileStream;
                int readLength;
                int entrySize;
                do
                {
                    readLength = 0;
                    entrySize = ReadBlockSize(fsin);
                    if (entrySize > 0)
                    {
                        byte[] buffer = new byte[entrySize];
                        readLength = fsin.Read(buffer, 0, entrySize);
                        _bytesToProcess.Add(readIndex, buffer);
                        if (_bytesToProcess.Count > MaxEntriesToKeep)
                            _bytesToProcess.WaitUntilCountBecomeLess();
                        readIndex++;
                    }
                } while (readLength >= entrySize && entrySize > 0 && !cts.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                HandleException("Error while reading input file: " + ex.Message);
            }
        }
        private void DecompressBlocks()
        {
            try
            {
                var current = _bytesToProcess.WithdrawalsCount;
                while (current < _blocksCount && !cts.IsCancellationRequested)
                {
                    if (_bytesToProcess.Count > 0)
                    {
                        byte[] entryToDecompress;
                        bool success = false;
                        lock (_lockObj)
                        {
                            current = _bytesToProcess.WithdrawalsCount;
                            success = _bytesToProcess.TryGet(current, out entryToDecompress);
                        }
                        if (success)
                        {
                            byte[] decompressedBytes;
                            using (var resultStream = new MemoryStream())
                            {
                                using (var memoryStream = new MemoryStream(entryToDecompress))
                                {
                                    using (var stream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                                    {
                                        stream.CopyTo(resultStream);
                                        decompressedBytes = resultStream.ToArray();
                                        stream.Close();
                                    }
                                }
                            }
                            if (_bytesToWrite.Count > MaxEntriesToKeep)
                                _bytesToWrite.WaitUntilCountBecomeLess();
                            _bytesToWrite.Add(current, decompressedBytes);
                        }
                    }
                    current = _bytesToProcess.WithdrawalsCount;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error while compressing input file: \n" + ex.Message);
            }
        }
        private void WriteDecompressedBlocks(object outputStream)
        {
            try
            {
                var fsout = outputStream as FileStream;
                var current = _bytesToWrite.WithdrawalsCount;
                while (current < _blocksCount && !cts.IsCancellationRequested)
                {
                    byte[] entryToWrite;
                    if (_bytesToWrite.Count > 0)
                    {
                        bool bytesGot = false;
                        lock (_lockObj)
                        {
                            current = _bytesToWrite.WithdrawalsCount;
                            bytesGot = _bytesToWrite.TryGet(current, out entryToWrite);
                        }
                        if (bytesGot)
                        {
                            fsout.Write(entryToWrite, 0, entryToWrite.Length);
                        }
                    }
                    current = _bytesToWrite.WithdrawalsCount;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error while writing compressed file: " + ex.Message);
            }
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
