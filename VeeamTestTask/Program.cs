using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VeeamTestTask
{
    class Program
    {
        //TODO: 1. Parallel decompressing
        //2. Reset progress if something goes wrong?
        static int Main(string[] args)        {
            var inputFilePath = "H:\\image1.jpg";
            var archivedFilePath = "H:\\arcived.zip";
            var unarchivedFilePath = "H:\\unarchived.jpg";

            IFileArchiver archiver = new FileArchiver();
            try {
                Console.WriteLine($"File {inputFilePath} is compressed, please wait...");
                archiver.Compress(inputFilePath, archivedFilePath);

                Console.WriteLine($"File {archivedFilePath} is decompressd, please wait...");
                archiver.Decompress(archivedFilePath, unarchivedFilePath);
                
                Console.WriteLine($"File {inputFilePath} was processed successfully. Result file located in {archivedFilePath} and {unarchivedFilePath}");
                
                Console.WriteLine(chekEqalsTwoFiles(inputFilePath, unarchivedFilePath));
                printMd5(inputFilePath);
                printMd5(unarchivedFilePath);
                return 0;
            } catch(Exception ex) {
                Console.WriteLine("Unexpected error during processing file: "+ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Execution aborted");
                
                return 1;
            } finally {
                Console.ReadLine();
            }
        }

        private static byte[] calculateMD5(string path) {
            var hash = new byte[0];
            using (var md5 = MD5.Create()){
                using (var stream = File.OpenRead(path)) {
                    hash = md5.ComputeHash(stream);
                }
            }
            return hash;
        }

        private static string md5toString(byte[] hash) {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void printMd5(string path){
            Console.WriteLine(md5toString(calculateMD5(path)));
        }

        private static bool chekEqalsTwoFiles(string firsFilePath, string secondFilePath) {
            var firstFileHash = calculateMD5(firsFilePath);
            var secondFileHash = calculateMD5(secondFilePath);
            return firstFileHash.SequenceEqual(secondFileHash);
        }
    }
}
