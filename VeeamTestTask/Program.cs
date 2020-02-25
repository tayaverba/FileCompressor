using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CommandLine;

namespace VeeamTestTask
{
    class Program
    {
        class Options
        {
            [Value(0, MetaName = "inputFilePath", HelpText = "Input file path")]
            public string InputPath { get; set; }
            [Value(1, MetaName = "outputFilePath", HelpText = "Output file path")]
            public string OutputPath { get; set; }
        }
        [Verb("compress", HelpText = "Compress file")]
        class CompressOptions : Options { }
        [Verb("decompress", HelpText = "Decompress file")]
        class DecompressOptions : Options { }

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CompressOptions, DecompressOptions>(args)
                .MapResult(
                  (CompressOptions opts) => Compress(opts.InputPath, opts.OutputPath),
                  (DecompressOptions opts) => Decompress(opts.InputPath, opts.OutputPath),
                  errs => 1);
        }

        private static int Decompress(string inputPath, string outputPath)
        {
            IFileArchiver archiver = new FileArchiver();
            try
            {
                Console.WriteLine("File {0} is decompressing, please wait...", inputPath);
                if (!ValidateFiles(inputPath, outputPath))
                {
                    return 1;
                }
                archiver.Decompress(inputPath, outputPath);
                Console.WriteLine("File {0} was decompressed successfully. Result file located in {1}", inputPath, outputPath);
                Console.ReadLine();
                return 0;
            }
            catch (Exception ex)
            {
                CancelOperation(outputPath, ex.Message);
                return 1;
            }
        }

        private static int Compress(string inputPath, string outputPath)
        {
            IFileArchiver archiver = new FileArchiver();
            try
            {
                Console.WriteLine("File {0} is compressing, please wait...", inputPath);
                if (!ValidateFiles(inputPath, outputPath))
                {
                    return 1;
                }
                archiver.Compress(inputPath, outputPath);
                Console.WriteLine("File {0} was compressed successfully. Result file located in {1}", inputPath, outputPath);
                Console.ReadLine();
                return 0;
            }
            catch (Exception ex)
            {
                CancelOperation(outputPath, ex.Message);
                return 1;
            }
        }
        private static bool ValidateFiles(string inputPath, string outputPath)//отдать пути
        {
            if (!ArchiverFilePathValidator.ValidateInputPath(inputPath))
            {
                Console.WriteLine("Error: input file path is incorrect: file does not exists. Try again.");
                Console.ReadLine();
                return false;
            }
            if (!ArchiverFilePathValidator.ValidateOutputPath(outputPath))
            {
                Console.WriteLine("Error: output file path is incorrect or output file already exists. Try again.");
                Console.ReadLine();
                return false;
            }
            return true;
        }

        private static void CancelOperation(string outputPath, string errorMessage)//отдать пути
        {
            Console.WriteLine("Unexpected error during processing file: \n" + errorMessage);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            Console.WriteLine("Execution aborted");
            Console.ReadLine();
        }

        #region Debug functions
        /// <summary>
        /// Use this function to debug and test work.
        /// It compresses and decomresses file and then compares results by md5 hash
        /// </summary>
        /// <returns></returns>
        private static int Test(string inputFilePath, string compressedFilePath, string decompressedFilePath)
        {
            IFileArchiver archiver = new FileArchiver();
            try
            {
                var startTime = DateTime.Now;
                Console.WriteLine($"File {inputFilePath} is compressing, please wait...");
                archiver.Compress(inputFilePath, compressedFilePath);
                Console.WriteLine("Compressing time: " + DateTime.Now.Subtract(startTime).TotalMilliseconds);
                startTime = DateTime.Now;
                Console.WriteLine($"File {compressedFilePath} is decompressing, please wait...");
                archiver.Decompress(compressedFilePath, decompressedFilePath);
                Console.WriteLine("Decompressing time: " + DateTime.Now.Subtract(startTime).TotalMilliseconds);

                Console.WriteLine($"File {inputFilePath} was processed successfully. Result files located in {compressedFilePath} and {decompressedFilePath}");

                Console.WriteLine("Files equality check: " + CheckFilesEqual(inputFilePath, decompressedFilePath));
                File.Delete(compressedFilePath);
                File.Delete(decompressedFilePath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error during processing file: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Execution aborted");

                return 1;
            }
        }
        private static byte[] CalculateMD5(string path)
        {
            var hash = new byte[0];
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    hash = md5.ComputeHash(stream);
                }
            }
            return hash;
        }

        private static string Md5toString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void PrintMd5(string path)
        {
            Console.WriteLine(Md5toString(CalculateMD5(path)));
        }

        private static bool CheckFilesEqual(string firsFilePath, string secondFilePath)
        {
            var firstFileHash = CalculateMD5(firsFilePath);
            var secondFileHash = CalculateMD5(secondFilePath);
            return firstFileHash.SequenceEqual(secondFileHash);
        }
    }
    #endregion
}
