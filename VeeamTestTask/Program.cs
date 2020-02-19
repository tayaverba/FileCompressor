using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamTestTask
{
    class Program
    {
        static int Main(string[] args)
        {
            //var validationResult = InputValidator.ValidateArgs(args);
            //if (!validationResult.IsValid)
            //{
            //    Console.WriteLine(validationResult.ErrorMessage);
            //    return 1;
            //}

            //var command = args[0];
            //var inputPath = args[1];
            //var outputPath = args[2];

            var command = "compress";
            var inputPath = "C:\\Test\\test.jpg";
            var outputPath = "C:\\Test\\testZip.mycomp";
            command = "decompress";
            outputPath = "C:\\Test\\test-big1.jpg";
            inputPath = "C:\\Test\\testZip.mycomp";

            IFileArchiver archiver = new GZipArchiver();
            try
            {
                Console.WriteLine("File {0} is processing, please wait...", inputPath);
                if (ArchiverInputValidator.IsCompressCommand(command))
                {
                    archiver.Compress(inputPath, outputPath);
                }
                if (ArchiverInputValidator.IsDecompressCommand(command))
                {
                    archiver.Decompress(inputPath, outputPath);
                }
                Console.WriteLine("File {0} was processed successfully. Result file located in {1}", inputPath, outputPath);
                Console.ReadLine();
                return 0;
            } catch(Exception ex)
            {
                Console.WriteLine("Unexpected error: "+ex.Message);
                Console.ReadLine();
                return 1;
            }
        }
    }
}
