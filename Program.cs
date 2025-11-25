using Amazon.Textract;
using HealthCardApi.Services;
using HealthCardApi.Mappers;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ALL SERVICE REGISTRATIONS MUST HAPPEN HERE - BEFORE builder.Build()
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonTextract>();
builder.Services.AddSingleton<FastTextractService>(); 
builder.Services.AddSingleton<PdfToImageService>();
builder.Services.AddLogging();

// NOW build the app
var app = builder.Build();

// Use CORS - MUST be called before other middleware
app.UseCors("AllowAll");

// Add port configuration for production
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

app.MapPost("/extract", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var file = form.Files["file"];
    
    if (file == null || file.Length == 0)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("No file uploaded.");
        return;
    }

    try
    {
        var fastTextractService = context.RequestServices.GetRequiredService<FastTextractService>();
        var pdfToImageService = context.RequestServices.GetRequiredService<PdfToImageService>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        
        AnalysisResult result;
        
        // Check if file is PDF
        if (pdfToImageService.IsPdfFile(file))
        {
            logger.LogInformation("Processing PDF file...");
            
            // Convert PDF to image and process with Textract
            using var pdfStream = file.OpenReadStream();
            using var imageStream = await pdfToImageService.ConvertFirstPageToImage(pdfStream);
            
            result = await fastTextractService.AnalyzeDocumentSync(file, imageStream);
        }
        else
        {
            logger.LogInformation("Processing image file...");
            // Process image file directly
            result = await fastTextractService.AnalyzeDocumentSync(file);
        }

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync($$"""
    {
    "rawText": "{{System.Text.Json.JsonEncodedText.Encode(result.RawText)}}",
    "structuredData": {
        "holder_first_name": "{{result.StructuredData["holder_first_name"]}}",
        "holder_surname": "{{result.StructuredData["holder_surname"]}}",
        "holder_name": "{{result.StructuredData["holder_name"]}}",
        "holder_address": "{{result.StructuredData["holder_address"]}}",
        "postal_code": "{{result.StructuredData["postal_code"]}}",
        "city": "{{result.StructuredData["city"]}}",
        "holder_postal_city": "{{result.StructuredData["holder_postal_city"]}}",
        "cpr": "{{result.StructuredData["cpr"]}}",
        "doctor_name": "{{result.StructuredData["doctor_name"]}}",
        "doctor_address": "{{result.StructuredData["doctor_address"]}}",
        "doctor_phone": "{{result.StructuredData["doctor_phone"]}}",
        "municipality": "{{result.StructuredData["municipality"]}}",
        "region": "{{result.StructuredData["region"]}}",
        "valid_from": "{{result.StructuredData["valid_from"]}}",
        "date_of_birth": "{{result.StructuredData["date_of_birth"]}}",
        "age": "{{result.StructuredData["age"]}}",
        "gender": "{{result.StructuredData["gender"]}}"
                    },
        "message": "Processing completed successfully.",
        "processingTime": "{{(pdfToImageService.IsPdfFile(file) ? "pdf_via_image" : "fast_sync")}}"
        }
""");   
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error processing document: {ex.Message}");
    }
});

app.MapGet("/travel-info", async (HttpContext context) =>
{
    var response = new
    {
        weather = new
        {
            conditions = "Partially cloudy",
            description = "Partly cloudy throughout the day",
            icon = "partly-cloudy-day"
        },
        delays = new
        {
            departures = "50%",
            arrivals = "30%"
        },
        safety = new
        {
            level = "HighRisk"
        },
        health = new
        {
            vaccinations = new[] 
            {
                "Routine",
                "Hepatitis A & B",
                "Typhoid",
                "Rabies",
                "Polio"
            },
            diseases = new[]
            {
                "Malaria (high risk)",
                "Cholera",
                "Tuberculosis",
                "Crimean-Congo hemorrhagic fever"
            },
            malariaRisk = "High, especially rural areas",
            precautions = "Avoid animal bites"
        }
    };
    
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
});

app.Run();