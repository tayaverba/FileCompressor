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
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static object _lockObj = new object();
        
        private BlockContainer _bytesToProcess;
        private BlockContainer _bytesToWrite;
        private string _errorMessage = null;
        private int _blocksCount = 0;        
        private int ThreadCount => Environment.ProcessorCount;
        private List<Thread> ThreadPool;
        private int MaxBlocksToKeep { get; }
        /// <summary>
        /// Size of compressing block in bytes
        /// </summary>
        public int BlockSize { get; }
        /// <summary>
        /// Creates new instance of <see cref="FileArchiver"/> 
        /// </summary>
        /// <param name="blockSizeInBytes">Size of compressing block in bytes</param>
        /// <param name="maxBlocksToKeep">Count of blocks to keep in memory in queues to read/write</param>
        public FileArchiver(int blockSizeInBytes = 1048576, int maxBlocksToKeep = 10)
        {
            BlockSize = blockSizeInBytes;
            MaxBlocksToKeep = maxBlocksToKeep;
            ThreadPool = new List<Thread>();
            _bytesToProcess = new BlockContainer(MaxBlocksToKeep);
            _bytesToWrite = new BlockContainer(MaxBlocksToKeep);
        }
        private void ClearProcessingQueues()
        {
            ThreadPool.Clear();
            _bytesToProcess.Clear();
            _bytesToWrite.Clear();
            _errorMessage = null;
        }
        private void ParallelProcess(FileStream inputStream, FileStream outputStream, Action<object> readAction, Action processAction, Action<object> writeAction)
        {
            ClearProcessingQueues();
            var threadToRead = new Thread(new ParameterizedThreadStart(readAction));
            threadToRead.Start(inputStream);
            ThreadPool.Add(threadToRead);
            for (int i = 0; i < ThreadCount - 2; i++)
            {
                var threadToDecompress = new Thread(new ThreadStart(processAction));
                threadToDecompress.Start();
                ThreadPool.Add(threadToDecompress);
            }
            var threadToWrite = new Thread(new ParameterizedThreadStart(writeAction));
            threadToWrite.Start(outputStream);
            ThreadPool.Add(threadToWrite);
            foreach (var thread in ThreadPool)
            {
                thread.Join();
            }
            if (_errorMessage != null) { throw new InvalidOperationException(_errorMessage); };
        }
        private void HandleException(string errorMessage)
        {
            lock (_lockObj)
            {
                _errorMessage = errorMessage;
                _cancellationTokenSource.Cancel();
            }
        }
        public void Compress(string inputPath, string outputPath)
        {
            using (FileStream inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    _blocksCount = (int)Math.Ceiling(inputStream.Length / (double)BlockSize);
                    WriteSize(outputStream, _blocksCount);
                    ParallelProcess(inputStream, outputStream, ReadDecompressedBlocks, CompressBlocks, WriteCompressedBlocks);
                }
            }
        }
        private void ReadDecompressedBlocks(object stream)
        {
            try
            {
                int readIndex = 0;
                var inputStream = stream as FileStream;
                int readLength;
                do
                {
                    byte[] buffer = new byte[BlockSize];
                    readLength = inputStream.Read(buffer, 0, BlockSize);
                    if (readLength < BlockSize)
                    {
                        var smallBuffer = new byte[readLength];
                        for (int i = 0; i < smallBuffer.Length; i++)
                        {
                            smallBuffer[i] = buffer[i];
                        }
                        buffer = smallBuffer;
                    }
                    _bytesToProcess.Add(readIndex, buffer);
                    if (_bytesToProcess.Count > MaxBlocksToKeep)
                    {
                        _bytesToProcess.WaitUntilCountBecomeLess();
                    }
                    readIndex++;
                } while (readLength >= BlockSize && !_cancellationTokenSource.IsCancellationRequested);
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
                while (current < _blocksCount && !_cancellationTokenSource.IsCancellationRequested)
                {
                    if (_bytesToProcess.Count > 0)
                    {
                        byte[] blockToCompress;
                        bool success = false;
                        lock (_lockObj)
                        {
                            current = _bytesToProcess.WithdrawalsCount;
                            success = _bytesToProcess.TryGet(current, out blockToCompress);
                        }
                        if (success)
                        {
                            byte[] compressedBytes;
                            using (var memoryStream = new MemoryStream())
                            {
                                using (var stream = new DeflateStream(memoryStream, CompressionLevel.Optimal))
                                {
                                    stream.Write(blockToCompress, 0, blockToCompress.Length);
                                    stream.Close();
                                    compressedBytes = memoryStream.ToArray();
                                }
                            }
                            _bytesToWrite.Add(current, compressedBytes);
                            if (_bytesToWrite.Count > MaxBlocksToKeep)
                                _bytesToWrite.WaitUntilCountBecomeLess();
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
        private void WriteCompressedBlocks(object stream)
        {
            try
            {
                var outputStream = stream as FileStream;
                var current = _bytesToWrite.WithdrawalsCount;
                while (current < _blocksCount && !_cancellationTokenSource.IsCancellationRequested)
                {
                    byte[] blockToWrite;
                    if (_bytesToWrite.Count > 0)
                    {
                        bool bytesGot = false;
                        lock (_lockObj)
                        {
                            current = _bytesToWrite.WithdrawalsCount;
                            bytesGot = _bytesToWrite.TryGet(current, out blockToWrite);
                        }
                        if (bytesGot)
                        {
                            WriteSize(outputStream, blockToWrite.Length);
                            outputStream.Write(blockToWrite, 0, blockToWrite.Length);
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
        private void WriteSize(FileStream stream, int length)
        {
            var sizeBytes = BitConverter.GetBytes(length);
            stream.Write(sizeBytes, 0, sizeBytes.Length);
        }
        public void Decompress(string inputPath, string outputPath)
        {
            using (FileStream inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    _blocksCount = ReadSize(inputStream);
                    ParallelProcess(inputStream, outputStream, ReadCompressedBlocks, DecompressBlocks, WriteDecompressedBlocks);
                }
            }
        }
        private void ReadCompressedBlocks(object stream)
        {
            try
            {
                int readIndex = 0;
                var inputStream = stream as FileStream;
                int readLength;
                int compressedBlockSize;
                do
                {
                    readLength = 0;
                    compressedBlockSize = ReadSize(inputStream);
                    if (compressedBlockSize > 0)
                    {
                        byte[] buffer = new byte[compressedBlockSize];
                        readLength = inputStream.Read(buffer, 0, compressedBlockSize);
                        _bytesToProcess.Add(readIndex, buffer);
                        if (_bytesToProcess.Count > MaxBlocksToKeep)
                        {
                            _bytesToProcess.WaitUntilCountBecomeLess();
                        }
                        readIndex++;
                    }
                } while (readLength >= compressedBlockSize && compressedBlockSize > 0 && !_cancellationTokenSource.IsCancellationRequested);
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
                while (current < _blocksCount && !_cancellationTokenSource.IsCancellationRequested)
                {
                    if (_bytesToProcess.Count > 0)
                    {
                        byte[] blockToDecompress;
                        bool success = false;
                        lock (_lockObj)
                        {
                            current = _bytesToProcess.WithdrawalsCount;
                            success = _bytesToProcess.TryGet(current, out blockToDecompress);
                        }
                        if (success)
                        {
                            byte[] decompressedBytes;
                            using (var resultStream = new MemoryStream())
                            {
                                using (var memoryStream = new MemoryStream(blockToDecompress))
                                {
                                    using (var stream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                                    {
                                        stream.CopyTo(resultStream);
                                        decompressedBytes = resultStream.ToArray();
                                        stream.Close();
                                    }
                                }
                            }
                            _bytesToWrite.Add(current, decompressedBytes);
                            if (_bytesToWrite.Count > MaxBlocksToKeep)
                                _bytesToWrite.WaitUntilCountBecomeLess();
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
        private void WriteDecompressedBlocks(object stream)
        {
            try
            {
                var outputStream = stream as FileStream;
                var current = _bytesToWrite.WithdrawalsCount;
                while (current < _blocksCount && !_cancellationTokenSource.IsCancellationRequested)
                {
                    byte[] blockToWrite;
                    if (_bytesToWrite.Count > 0)
                    {
                        bool bytesGot = false;
                        lock (_lockObj)
                        {
                            current = _bytesToWrite.WithdrawalsCount;
                            bytesGot = _bytesToWrite.TryGet(current, out blockToWrite);
                        }
                        if (bytesGot)
                        {
                            outputStream.Write(blockToWrite, 0, blockToWrite.Length);
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
        private int ReadSize(FileStream stream)
        {
            int intTypeSizeInBytes = 4;
            byte[] sizeBuf = new byte[intTypeSizeInBytes];
            var readBytesCount = stream.Read(sizeBuf, 0, intTypeSizeInBytes);
            if (readBytesCount > 0)
            {
                return BitConverter.ToInt32(sizeBuf, 0);
            }
            return 0;
        }
    }
}
