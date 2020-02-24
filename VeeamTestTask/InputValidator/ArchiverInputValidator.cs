using System.IO;

namespace VeeamTestTask
{
    /// <summary>
    /// Provides validation for console input arguments
    /// </summary>
    public static class ArchiverFilePathValidator
    {
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
        /// <returns> True if <paramref name="outputPath"/> does not exists and does not contains any invalid file name characters</returns>
        public static bool ValidateOutputPath(string outputPath)
        {
            var t = outputPath.IndexOfAny(Path.GetInvalidPathChars());
            return !string.IsNullOrEmpty(outputPath)
              && outputPath.IndexOfAny(Path.GetInvalidPathChars())<0
              && !File.Exists(outputPath);
        }
    }
}
