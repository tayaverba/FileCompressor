namespace VeeamTestTask
{
    /// <summary>
    /// Represents archiever that can compress or decompress files
    /// </summary>
    interface IFileArchiver
    {
        /// <summary>
        /// Compress file located in <paramref name="inputPath"/> and writes result to new file located in <paramref name="outputPath"/>
        /// </summary>
        /// <param name="inputPath">Input file path</param>
        /// <param name="outputPath">Result file path</param>
        void Compress(string inputPath, string outputPath);
        /// <summary>
        /// Decompress file located in <paramref name="inputPath"/> and writes result to new file located in <paramref name="outputPath"/>
        /// </summary>
        /// <param name="inputPath">Input file path</param>
        /// <param name="outputPath">Result file path</param>
        void Decompress(string inputPath, string outputPath);
    }
}
