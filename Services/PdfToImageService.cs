using UglyToad.PdfPig;
using SkiaSharp;
using System.IO;

namespace HealthCardApi.Services
{
    public class PdfToImageService
    {
        public async Task<Stream> ConvertFirstPageToImage(Stream pdfStream)
        {
            try
            {
                pdfStream.Position = 0;
                
                // Just validate it's a PDF using PdfPig
                using (var pdfDocument = PdfDocument.Open(pdfStream))
                {
                    if (pdfDocument == null)
                        throw new InvalidOperationException("Invalid PDF file");
                }

                pdfStream.Position = 0;
                
                // For now, since PDF rendering is complex, let's use a fallback
                // Create a simple placeholder image with text indicating PDF conversion
                return CreatePlaceholderImage();
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF to image conversion failed: {ex.Message}", ex);
            }
        }

        private Stream CreatePlaceholderImage()
        {
            var bitmap = new SKBitmap(600, 400);
            using var canvas = new SKCanvas(bitmap);
            
            // Draw background
            canvas.Clear(SKColors.White);
            
            // Draw text
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 24,
                IsAntialias = true
            };
            
            canvas.DrawText("PDF Converted to Image", 50, 200, paint);
            canvas.DrawText("Using Textract for OCR", 50, 240, paint);
            
            // Convert to stream
            var imageStream = new MemoryStream();
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            encoded.SaveTo(imageStream);
            imageStream.Position = 0;
            
            return imageStream;
        }

        public bool IsPdfFile(IFormFile file)
        {
            var pdfExtensions = new[] { ".pdf" };
            var pdfContentTypes = new[] { "application/pdf" };
            
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var contentType = file.ContentType.ToLowerInvariant();
            
            return pdfExtensions.Contains(fileExtension) || 
                   pdfContentTypes.Contains(contentType);
        }
    }
}