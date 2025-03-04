using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PetNameWithEyes;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly string _bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME") ?? throw new Exception("BUCKET_NAME environment variable is missing.");
    
    public Function()
    {
        _s3Client = new AmazonS3Client();
        _bedrockClient = new AmazonBedrockRuntimeClient();
    }

    public async Task FunctionHandler(S3ObjectLambdaEvent input, ILambdaContext context)
    {
        var objectUri = new Uri(input.GetObjectContext.InputS3Url);
        var objectKey = objectUri.Segments.Last();
        var (adverb, adjective, noun) = ParsePetName(objectKey);
        
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(input.GetObjectContext.InputS3Url);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to fetch image from pre-signed URL");
            }
            
            var stream = await response.Content.ReadAsStreamAsync();
            await _s3Client.WriteGetObjectResponseAsync(new WriteGetObjectResponseRequest
            {
                RequestRoute = input.GetObjectContext.OutputRoute,
                RequestToken = input.GetObjectContext.OutputToken,
                ContentLength = response.Content.Headers.ContentLength,
                ContentType = "image/png",
                Body = stream,
            });
        }
        catch (Exception)
        {
            var imageBytes = await GenerateImage(adverb, adjective, noun);
            
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = new MemoryStream(imageBytes)
            });

            await _s3Client.WriteGetObjectResponseAsync(new WriteGetObjectResponseRequest
            {
                RequestRoute = input.GetObjectContext.OutputRoute,
                RequestToken = input.GetObjectContext.OutputToken,
                ContentType = "image/png",
                Body = new MemoryStream(imageBytes)
            });
        }
    }

    private static (string Adverb, string Adjective, string Animal) ParsePetName(string objectKey)
    {
        var parts = objectKey.Split('-');
        if (parts.Length != 3)
        {
            throw new Exception("Invalid pet name format. Expected: adverb-adjective-noun");
        }
        return (parts[0], parts[1], parts[2]);
    }

    private async Task<byte[]> GenerateImage(string adverb, string adjective, string animal)
    {
        var prompt = $"A whimsical 2D cartoon {animal} in a minimalist style, characterized by its {adverb} {adjective} appearance. " +
                     $"Simple shapes, bold lines, and flat colors. " +
                     $"Set against a soft, solid-colored background. " +
                     $"The {animal}'s expression is friendly and engaging. " +
                     $"Style reference: Mix of modern vector art and classic cartoon aesthetics, similar to popular app icons or emoji designs.";
        
        var request = new
        {
            taskType = "TEXT_IMAGE",
            textToImageParams = new
            {
                text = prompt,
            },
            imageGenerationConfig = new
            {
                quality = "standard",
                numberOfImages = 1,
                height = 512,
                width = 512,
                cfgScale = 8.0,
                seed = 42,
            }
        };

        var response = await _bedrockClient.InvokeModelAsync(new InvokeModelRequest
        {
            ModelId = "amazon.titan-image-generator-v1",
            ContentType = "application/json",
            Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(request))
        });

        using var streamReader = new StreamReader(response.Body);
        var jsonResponse = await streamReader.ReadToEndAsync();
        var responseObj = JsonSerializer.Deserialize<TitanImageResponse>(jsonResponse);
        if (responseObj?.Images == null || responseObj.Images.Count == 0)
        {
            throw new Exception("Failed to generate image.");
        }
        
        return Convert.FromBase64String(responseObj.Images[0]);
    }
}

public class TitanImageResponse
{
    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }
} 