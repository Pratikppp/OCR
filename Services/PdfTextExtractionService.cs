using UglyToad.PdfPig;
using HealthCardApi.Mappers;
using System.Text;
using Amazon.Textract;

namespace HealthCardApi.Services
{
    public class PdfTextExtractionService
    {
        public async Task<AnalysisResult> ExtractFromPdf(Stream pdfStream)
        {
            try
            {
                pdfStream.Position = 0;
                
                // Extract text from PDF
                var text = await ExtractTextFromPdf(pdfStream);
                
                // Convert to blocks format that your mapper expects
                var blocks = CreateBlocksFromText(text);
                
                // Use your existing mapper
                var structuredData = HealthCardMapper.Map(blocks);
                
                return new AnalysisResult
                {
                    RawText = text,
                    StructuredData = structuredData
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF text extraction failed: {ex.Message}", ex);
            }
        }

        private async Task<string> ExtractTextFromPdf(Stream pdfStream)
        {
            return await Task.Run(() =>
            {
                pdfStream.Position = 0;
                var textBuilder = new StringBuilder();
                
                using (var pdfDocument = PdfDocument.Open(pdfStream))
                {
                    foreach (var page in pdfDocument.GetPages())
                    {
                        foreach (var word in page.GetWords())
                        {
                            textBuilder.AppendLine(word.Text);
                        }
                    }
                }
                
                return textBuilder.ToString();
            });
        }

        private List<Amazon.Textract.Model.Block> CreateBlocksFromText(string text)
        {
            var blocks = new List<Amazon.Textract.Model.Block>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < lines.Length; i++)
            {
                blocks.Add(new Amazon.Textract.Model.Block
                {
                    BlockType = BlockType.LINE,
                    Text = lines[i].Trim(),
                    Id = (i + 1).ToString(),
                    Confidence = 95f
                });
            }
            
            return blocks;
        }
    }
}