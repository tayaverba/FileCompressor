namespace VeeamTestTask
{
    interface IFileArchiver
    {
        void Compress(string inputPath, string outputPath);
        void Decompress(string inputPath, string outputPath);
    }
}
