using System;
using System.IO;
using System.Text.RegularExpressions;

namespace VeeamTestTask
{
    public static class ArchiverInputValidator
    {
        private const string compressCommandString = "compress";
        private const string decompressCommandString = "decompress";
        public static InputValidatorResult ValidateArgs(string[] args)
        {
            if (args.Length != 3)
            {
                return new InputValidatorResult(false, "Error: wrong input args. Example: compress/decompress [input file path] [output file path]");
            }
            var command = args[0];
            var inputPath = args[1];
            var outputPath = args[2];
            if (!ValidateCommand(command))
            {
                return new InputValidatorResult(false, "Error: command was not recognized. Use key \"compress\" or \"decompress\".");

            }
            if (!ValidateInputPath(inputPath))
            {
                return new InputValidatorResult(false, "Error: input file does not exist. Try again.");

            }
            if (!ValidateOutputPath(outputPath))
            {
                return new InputValidatorResult(false, "Error: output file path is incorrect. Try again.");

            }
            return new InputValidatorResult(true);
        }

        public static bool ValidateCommand(string command)
        {
            return command.ToLower().Equals(compressCommandString) || command.ToLower().Equals(decompressCommandString);
        }
        public static bool ValidateInputPath(string inputPath)
        {
            return File.Exists(inputPath);
        }
        public static bool ValidateOutputPath(string outputPath)
        {
            string fileNamePattern = @"^(?!^(PRN|AUX|CLOCK\$|NUL|CON|COM\d|LPT\d|\..*)(\..+)?$)[^\x00-\x1f\\?*:\"";|/]+$";
            return Regex.IsMatch(outputPath, fileNamePattern, RegexOptions.CultureInvariant);
        }

        public static bool IsCompressCommand(string command)
        {
            return command.Equals(compressCommandString);
        }

        public static bool IsDecompressCommand(string command)
        {
            return command.Equals(decompressCommandString);
        }
    }
}
