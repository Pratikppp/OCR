using Amazon.Textract;
using Amazon.Textract.Model;
using HealthCardApi.Mappers;
using Microsoft.AspNetCore.Http;

namespace HealthCardApi.Services
{
    public class FastTextractService
    {
        private readonly IAmazonTextract _textract;

        public FastTextractService(IAmazonTextract textract)
        {
            _textract = textract;
        }

        // Original method - for direct file processing (images)
        public async Task<AnalysisResult> AnalyzeDocumentSync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            return await AnalyzeDocumentSync(stream);
        }

        // NEW OVERLOAD: For stream processing (PDF converted to images)
        public async Task<AnalysisResult> AnalyzeDocumentSync(Stream fileStream)
        {
            if (fileStream == null || fileStream.Length == 0)
                throw new ArgumentException("File stream is empty.");

            // Ensure stream is at the beginning
            if (fileStream.CanSeek)
                fileStream.Position = 0;

            var request = new DetectDocumentTextRequest
            {
                Document = new Document
                {
                    Bytes = (MemoryStream)fileStream
                }
            };

            // Single API call - no polling needed!
            var response = await _textract.DetectDocumentTextAsync(request);
            
            // Extract raw text
            var rawText = ExtractTextFromBlocks(response.Blocks);
            
            // Map to structured data
            var structuredData = HealthCardMapper.Map(response.Blocks);

            return new AnalysisResult
            {
                RawText = rawText,
                StructuredData = structuredData
            };
        }

        // NEW OVERLOAD: For PDF processing with original file info
        public async Task<AnalysisResult> AnalyzeDocumentSync(IFormFile file, Stream fileStream)
        {
            // Use the stream version - ignore the IFormFile for processing
            // but we keep the parameter for interface consistency
            return await AnalyzeDocumentSync(fileStream);
        }

        private string ExtractTextFromBlocks(List<Block> blocks)
        {
            var lines = blocks
                .Where(b => b.BlockType == BlockType.LINE && !string.IsNullOrEmpty(b.Text))
                .Select(b => b.Text);

            return string.Join(Environment.NewLine, lines);
        }
    }

    public class AnalysisResult
    {
        public string RawText { get; set; } = string.Empty;
        public Dictionary<string, string> StructuredData { get; set; } = new Dictionary<string, string>();
    }
}