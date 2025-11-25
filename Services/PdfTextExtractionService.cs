using UglyToad.PdfPig;
using HealthCardApi.Mappers;
using System.Text;
using Amazon.Textract.Model;
using Amazon.Textract;

namespace HealthCardApi.Services
{
    public class PdfTextExtractionService
    {
        private readonly ILogger<PdfTextExtractionService> _logger;

        public PdfTextExtractionService(ILogger<PdfTextExtractionService> logger)
        {
            _logger = logger;
        }

        public async Task<AnalysisResult> ExtractFromPdf(Stream pdfStream)
        {
            try
            {
                _logger.LogInformation("Starting PDF text extraction");
                
                pdfStream.Position = 0;
                
                // Extract text from PDF
                var text = await ExtractTextFromPdf(pdfStream);
                
                _logger.LogInformation($"Extracted text length: {text.Length}");
                _logger.LogInformation($"Extracted text: {text}");
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new Exception("No text could be extracted from PDF");
                }

                // Convert to blocks format that your mapper expects
                var blocks = CreateBlocksFromText(text);
                
                _logger.LogInformation($"Created {blocks.Count} blocks from text");
                
                // Use your existing mapper
                var structuredData = HealthCardMapper.Map(blocks);
                
                _logger.LogInformation("PDF processing completed successfully");

                return new AnalysisResult
                {
                    RawText = text,
                    StructuredData = structuredData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"PDF text extraction failed: {ex.Message}");
                throw new Exception($"PDF text extraction failed: {ex.Message}", ex);
            }
        }

        private async Task<string> ExtractTextFromPdf(Stream pdfStream)
        {
            return await Task.Run(() =>
            {
                try
                {
                    pdfStream.Position = 0;
                    var textBuilder = new StringBuilder();
                    
                    using (var pdfDocument = PdfDocument.Open(pdfStream))
                    {
                        _logger.LogInformation($"PDF has {pdfDocument.NumberOfPages} pages");
                        
                        foreach (var page in pdfDocument.GetPages())
                        {
                            _logger.LogInformation($"Processing page {page.Number}");
                            
                            var words = page.GetWords().ToList();
                            _logger.LogInformation($"Found {words.Count} words on page {page.Number}");
                            
                            foreach (var word in words)
                            {
                                textBuilder.Append(word.Text + " ");
                            }
                            
                            // Add line break between pages
                            textBuilder.AppendLine();
                        }
                    }
                    
                    var result = textBuilder.ToString().Trim();
                    _logger.LogInformation($"Final extracted text: '{result}'");
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in ExtractTextFromPdf: {ex.Message}");
                    throw;
                }
            });
        }

        private List<Block> CreateBlocksFromText(string text)
        {
            var blocks = new List<Block>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            _logger.LogInformation($"Splitting text into {lines.Length} lines");
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    blocks.Add(new Block
                    {
                        BlockType = BlockType.LINE,
                        Text = line,
                        Id = (i + 1).ToString(),
                        Confidence = 95f
                    });
                    
                    _logger.LogInformation($"Block {i + 1}: '{line}'");
                }
            }
            
            return blocks;
        }
    }
}