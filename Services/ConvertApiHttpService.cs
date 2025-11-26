using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HealthCardApi.Services
{
    public class ConvertApiHttpService
    {
        private readonly string _apiSecret;
        private readonly ILogger<ConvertApiHttpService> _logger;
        private readonly HttpClient _httpClient;

        public ConvertApiHttpService(ILogger<ConvertApiHttpService> logger, HttpClient httpClient)
        {
            _apiSecret = "wF8jvQKuSScRnLpDERq82CsZ2lXQF81P";
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<Stream> ConvertPdfToJpg(Stream pdfStream, string fileName)
        {
            try
            {
                _logger.LogInformation("Converting PDF to JPG using ConvertAPI");

                // Reset stream position and create a copy
                if (pdfStream.CanSeek)
                    pdfStream.Position = 0;

                var pdfCopy = new MemoryStream();
                await pdfStream.CopyToAsync(pdfCopy);
                pdfCopy.Position = 0;

                // Create multipart form data
                using var formData = new MultipartFormDataContent();
                using var fileContent = new StreamContent(pdfCopy);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                formData.Add(fileContent, "File", fileName);

                // Convert to JPG
                var url = $"https://v2.convertapi.com/convert/pdf/to/jpg?secret={_apiSecret}";
                var response = await _httpClient.PostAsync(url, formData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ConvertAPI HTTP error: {response.StatusCode} - {errorContent}");
                }

                // Parse the JSON response and extract base64 image
                var jsonContent = await response.Content.ReadAsStringAsync();
                var imageStream = await ExtractImageFromJsonResponse(jsonContent, "JPG");
                
                _logger.LogInformation($"PDF successfully converted to JPG, size: {imageStream.Length} bytes");
                return imageStream;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ConvertAPI JPG conversion failed: {ex.Message}");
                throw new Exception($"PDF to image conversion failed: {ex.Message}", ex);
            }
        }

        public async Task<Stream> ConvertPdfToPng(Stream pdfStream, string fileName)
        {
            try
            {
                _logger.LogInformation("Converting PDF to PNG using ConvertAPI");

                // Reset stream position and create a copy
                if (pdfStream.CanSeek)
                    pdfStream.Position = 0;

                var pdfCopy = new MemoryStream();
                await pdfStream.CopyToAsync(pdfCopy);
                pdfCopy.Position = 0;

                // Create multipart form data
                using var formData = new MultipartFormDataContent();
                using var fileContent = new StreamContent(pdfCopy);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                formData.Add(fileContent, "File", fileName);

                // Convert to PNG
                var url = $"https://v2.convertapi.com/convert/pdf/to/png?secret={_apiSecret}";
                var response = await _httpClient.PostAsync(url, formData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ConvertAPI HTTP error: {response.StatusCode} - {errorContent}");
                }

                // Parse the JSON response and extract base64 image
                var jsonContent = await response.Content.ReadAsStringAsync();
                var imageStream = await ExtractImageFromJsonResponse(jsonContent, "PNG");
                
                _logger.LogInformation($"PDF successfully converted to PNG, size: {imageStream.Length} bytes");
                return imageStream;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ConvertAPI PNG conversion failed: {ex.Message}");
                throw new Exception($"PDF to image conversion failed: {ex.Message}", ex);
            }
        }

        private async Task<Stream> ExtractImageFromJsonResponse(string jsonContent, string format)
        {
            try
            {
                _logger.LogInformation($"Parsing ConvertAPI JSON response for {format}");

                using var jsonDoc = JsonDocument.Parse(jsonContent);
                var root = jsonDoc.RootElement;

                // Check if conversion was successful
                if (root.TryGetProperty("Files", out var filesArray) && filesArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in filesArray.EnumerateArray())
                    {
                        if (file.TryGetProperty("FileData", out var fileData) && fileData.ValueKind == JsonValueKind.String)
                        {
                            var base64Data = fileData.GetString();
                            if (!string.IsNullOrEmpty(base64Data))
                            {
                                // Convert base64 to stream
                                var imageBytes = Convert.FromBase64String(base64Data);
                                var imageStream = new MemoryStream(imageBytes);
                                imageStream.Position = 0;

                                _logger.LogInformation($"Successfully extracted {format} image from JSON response");
                                return imageStream;
                            }
                        }
                    }
                }

                // If we can't find FileData, check for error
                if (root.TryGetProperty("Code", out var errorCode) && root.TryGetProperty("Message", out var errorMessage))
                {
                    throw new Exception($"ConvertAPI error {errorCode}: {errorMessage.GetString()}");
                }

                throw new Exception("No image data found in ConvertAPI response");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to extract image from JSON response: {ex.Message}");
                throw new Exception($"Failed to process ConvertAPI response: {ex.Message}");
            }
        }

        public async Task<bool> IsApiKeyValid()
        {
            try
            {
                var url = $"https://v2.convertapi.com/user?secret={_apiSecret}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("ConvertAPI token is valid");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"ConvertAPI token validation failed: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ConvertAPI key validation failed: {ex.Message}");
                return false;
            }
        }

        // Updated debug method
        public async Task<string> DebugConvertApiResponse(Stream pdfStream, string fileName)
        {
            try
            {
                if (pdfStream.CanSeek)
                    pdfStream.Position = 0;

                var pdfCopy = new MemoryStream();
                await pdfStream.CopyToAsync(pdfCopy);
                pdfCopy.Position = 0;

                using var formData = new MultipartFormDataContent();
                using var fileContent = new StreamContent(pdfCopy);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                formData.Add(fileContent, "File", fileName);

                var url = $"https://v2.convertapi.com/convert/pdf/to/jpg?secret={_apiSecret}";
                var response = await _httpClient.PostAsync(url, formData);

                var content = await response.Content.ReadAsStringAsync();
                
                // Try to parse the response to see what we got
                try
                {
                    using var jsonDoc = JsonDocument.Parse(content);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("Files", out var files))
                    {
                        return $"Status: {response.StatusCode}, Files: {files.GetArrayLength()} files returned";
                    }
                    else if (root.TryGetProperty("Message", out var message))
                    {
                        return $"Status: {response.StatusCode}, Error: {message.GetString()}";
                    }
                }
                catch (JsonException)
                {
                    // Not JSON
                }
                
                return $"Status: {response.StatusCode}, Content-Type: {response.Content.Headers.ContentType}, Content length: {content.Length}";
            }
            catch (Exception ex)
            {
                return $"Debug failed: {ex.Message}";
            }
        }
    }
}