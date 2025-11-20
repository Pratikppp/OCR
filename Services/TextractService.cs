using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace HealthCardApi.Services
{
    public class TextractService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonTextract _textract;

        public TextractService(IAmazonS3 s3Client, IAmazonTextract textract)
        {
            _s3Client = s3Client;
            _textract = textract;
        }

        
        public async Task UploadToS3(string bucket, string key, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            using var stream = file.OpenReadStream();
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = bucket,
                ContentType = file.ContentType
            };

            var fileTransferUtility = new TransferUtility(_s3Client);
            await fileTransferUtility.UploadAsync(uploadRequest);
        }

        
        public async Task<string> StartAnalysis(string bucket, string key)
        {
            var request = new StartDocumentTextDetectionRequest
            {
                DocumentLocation = new DocumentLocation
                {
                    S3Object = new S3Object
                    {
                        Bucket = bucket,
                        Name = key
                    }
                }
            };

            var response = await _textract.StartDocumentTextDetectionAsync(request);
            return response.JobId;
        }

        // Optimized polling with exponential backoff
        public async Task WaitForJobToComplete(string jobId)
        {
            string status = null;
            int delay = 1000; // Start with 1 second
            int maxDelay = 10000; // Max 10 seconds
            int attempts = 0;
            int maxAttempts = 30; // ~5 minutes max

            do
            {
                await Task.Delay(delay);
                
                var response = await _textract.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
                {
                    JobId = jobId
                });

                status = response.JobStatus;

                if (status == "SUCCEEDED") break;
                if (status == "FAILED") throw new Exception("Textract job failed.");

                // Exponential backoff
                delay = Math.Min(delay * 2, maxDelay);
                attempts++;

                if (attempts >= maxAttempts)
                    throw new Exception("Textract job timed out.");

            } while (status == "IN_PROGRESS");
        }

        
        public async Task<List<Block>> GetAnalysisResults(string jobId)
        {
            var blocks = new List<Block>();
            string nextToken = null;

            do
            {
                var response = await _textract.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
                {
                    JobId = jobId,
                    NextToken = nextToken
                });

                blocks.AddRange(response.Blocks);
                nextToken = response.NextToken;

            } while (!string.IsNullOrEmpty(nextToken));

            return blocks;
        }

       
        public string ExtractText(List<Block> blocks)
        {
            var sb = new StringBuilder();

            foreach (var block in blocks)
            {
                if (block.BlockType == BlockType.LINE && !string.IsNullOrEmpty(block.Text))
                {
                    sb.AppendLine(block.Text);
                }
            }

            return sb.ToString();
        }
    }
}