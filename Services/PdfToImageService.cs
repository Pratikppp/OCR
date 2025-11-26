using System.IO;

namespace HealthCardApi.Services
{
    public class PdfToImageService
    {
        private readonly ILogger<PdfToImageService> _logger;
        private readonly ConvertApiHttpService _convertApiService;

        public PdfToImageService(ILogger<PdfToImageService> logger, ConvertApiHttpService convertApiService)
        {
            _logger = logger;
            _convertApiService = convertApiService;
        }

        public async Task<Stream> ConvertFirstPageToImage(Stream pdfStream, string fileName)
        {
            try
            {
                _logger.LogInformation($"Converting PDF to image: {fileName}");
                
                // Ensure stream is at beginning
                if (pdfStream.CanSeek)
                    pdfStream.Position = 0;
                
                // Use JPG conversion (PNG will work similarly)
                var imageStream = await _convertApiService.ConvertPdfToJpg(pdfStream, fileName);
                
                _logger.LogInformation("PDF successfully converted to JPG via ConvertAPI");
                return imageStream;
            }
            catch (Exception ex)
            {
                _logger.LogError($"PDF to image conversion failed: {ex.Message}");
                throw new Exception($"PDF processing failed: {ex.Message}. Please try uploading an image file instead.", ex);
            }
        }

        public bool IsPdfFile(IFormFile file)
        {
            if (file == null) return false;
            
            var pdfExtensions = new[] { ".pdf" };
            var pdfContentTypes = new[] { "application/pdf" };
            
            var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
            var contentType = file.ContentType?.ToLowerInvariant() ?? "";
            
            return pdfExtensions.Contains(fileExtension) || 
                   pdfContentTypes.Contains(contentType);
        }
    }
}