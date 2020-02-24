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
        class CompressOptions: Options {}
        [Verb("decompress", HelpText = "Decompress file")]
        class DecompressOptions : Options { }

        static int Main(string[] args)
        {
            string[] testArgs = { "compress", "C:\\Test\\test-big.jpg", "C:\\Test\\testZip.mycomp" };
            //string[] testArgs = { "decompress", "C:\\Test\\testZip.mycomp", "C:\\Test\\test-big1.jpg" };
            return Parser.Default.ParseArguments<CompressOptions, DecompressOptions>(testArgs)
                .MapResult(
                  (CompressOptions opts) => Compress(opts),
                  (DecompressOptions opts) => Decompress(opts),
                  errs => 1);
        }

        private static int Decompress(DecompressOptions opts) { 
            IFileArchiver archiver = new FileArchiver();
            try
            {
                Console.WriteLine("File {0} is decompressing, please wait...", opts.InputPath);
                if (ValidateFiles(opts))
                {
                    return 1;
                }
                archiver.Decompress(opts.InputPath, opts.OutputPath);
                Console.WriteLine("File {0} was processed successfully. Result file located in {1}", opts.InputPath, opts.OutputPath);
                Console.ReadLine();
                return 0;
            } catch(Exception ex)
            {
                Console.WriteLine("Unexpected error during decompressing file: " + ex.Message);
                Console.WriteLine("Execution aborted");
                Console.ReadLine();
                return 1;
            }
    }

        private static int Compress(CompressOptions opts)
        {
            IFileArchiver archiver = new FileArchiver();
            try
            {
                Console.WriteLine("File {0} is compressing, please wait...", opts.InputPath);
                if (ValidateFiles(opts))
                {
                    return 1;
                }
                archiver.Compress(opts.InputPath, opts.OutputPath);
                Console.WriteLine("File {0} was processed successfully. Result file located in {1}", opts.InputPath, opts.OutputPath);
                Console.ReadLine();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error during compressing file: " + ex.Message);
                Console.WriteLine("Execution aborted");
                Console.ReadLine();
                return 1;
            }
        }
        private static bool ValidateFiles(Options opts)
        {
            if (!ArchiverFilePathValidator.ValidateInputPath(opts.InputPath))
            {
                Console.WriteLine("Error: input file path is incorrect: file does not exists. Try again.");
                return false;
            }
            if (!ArchiverFilePathValidator.ValidateOutputPath(opts.OutputPath))
            {
                Console.WriteLine("Error: output file path is incorrect or output file already exists. Try again.");
                return false;
            }
            return true;
        }
        private int Test()
        {
            var inputFilePath = "C:\\Test\\test-big.dat";
            var archivedFilePath = "C:\\Test\\testZip.mycomp";
            var unarchivedFilePath = "C:\\Test\\test2.dat";

            IFileArchiver archiver = new FileArchiver();
            try
            {
                Console.WriteLine($"File {inputFilePath} is compressed, please wait...");
                archiver.Compress(inputFilePath, archivedFilePath);

                Console.WriteLine($"File {archivedFilePath} is decompressd, please wait...");
                archiver.Decompress(archivedFilePath, unarchivedFilePath);

                Console.WriteLine($"File {inputFilePath} was processed successfully. Result file located in {archivedFilePath} and {unarchivedFilePath}");

                Console.WriteLine(CheсkEqualsTwoFiles(inputFilePath, unarchivedFilePath));
                PrintMd5(inputFilePath);
                PrintMd5(unarchivedFilePath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error during processing file: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Execution aborted");

                return 1;
            }
            finally
            {
                Console.ReadLine();
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

        private static bool CheсkEqualsTwoFiles(string firsFilePath, string secondFilePath)
        {
            var firstFileHash = CalculateMD5(firsFilePath);
            var secondFileHash = CalculateMD5(secondFilePath);
            return firstFileHash.SequenceEqual(secondFileHash);
        }
    }
}
