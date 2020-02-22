using System;
using System.IO;
using System.Text.RegularExpressions;

namespace VeeamTestTask
{
    /// <summary>
    /// Provides validation for console input arguments
    /// </summary>
    public static class ArchiverInputValidator
    {
        private const string CompressCommandString = "compress";
        private const string DecompressCommandString = "decompress";
        /// <summary>
        /// Validates all string arguments <paramref name="args"/> using mask "compress/decompress [input file path] [output file path]"
        /// </summary>
        /// <param name="args">Arguments: command, input file path, output file path</param>
        /// <returns></returns>
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
        /// <summary>
        /// Validates command string
        /// </summary>
        /// <param name="command">Command string: compresss or decompress</param>
        /// <returns></returns>
        public static bool ValidateCommand(string command)
        {
            return command.ToLower().Equals(CompressCommandString) || command.ToLower().Equals(DecompressCommandString);
        }
        /// <summary>
        /// Validates input file path
        /// </summary>
        /// <param name="inputPath">Input file path</param>
        /// <returns>True if <paramref name="inputPath"/> exists</returns>
        public static bool ValidateInputPath(string inputPath)
        {
            return File.Exists(inputPath);
        }
        /// <summary>
        /// Validates output file path
        /// </summary>
        /// <param name="outputPath">Output file path</param>
        /// <returns> True if <paramref name="outputPath"/> was parsed as file path</returns>
        public static bool ValidateOutputPath(string outputPath)
        {
            string fileNamePattern = @"^(?!^(PRN|AUX|CLOCK\$|NUL|CON|COM\d|LPT\d|\..*)(\..+)?$)[^\x00-\x1f\\?*:\"";|/]+$";
            return Regex.IsMatch(outputPath, fileNamePattern, RegexOptions.CultureInvariant);
        }
        /// <summary>
        /// Checks if it is compress command or not
        /// </summary>
        /// <param name="command">Command string</param>
        /// <returns>True if <paramref name="command"/> was recognizes as compress command; otherwise false</returns>
        public static bool IsCompressCommand(string command)
        {
            return command.Equals(CompressCommandString);
        }
        /// <summary>
        /// Checks if it is decompress command or not
        /// </summary>
        /// <param name="command">Command string</param>
        /// <returns>True if <paramref name="command"/> was recognizes as decompress command; otherwise false</returns>
        public static bool IsDecompressCommand(string command)
        {
            return command.Equals(DecompressCommandString);
        }
    }
}
