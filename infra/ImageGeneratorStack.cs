using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3ObjectLambda.Alpha;
using Constructs;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;

namespace Infrastructure
{
    public class ImageGeneratorStack : Stack
    {
        public ImageGeneratorStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            // Create S3 bucket
            var bucket = new Bucket(this, "PetImageBucket", new BucketProps
            {
                RemovalPolicy = RemovalPolicy.DESTROY,
                AutoDeleteObjects = true,
                PublicReadAccess = false,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL
            });

            // Create Lambda function
            var function = new Function(this, "PetNameWithEyesFunction", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "PetNameWithEyes::PetNameWithEyes.Function::FunctionHandler",
                Code = Code.FromAsset("../src/PetNameWithEyes", new AssetOptions
                {
                    Bundling = new BundlingOptions
                    {
                        Image = Runtime.DOTNET_8.BundlingImage,
                        OutputType = BundlingOutput.ARCHIVED,
                        User = "root",
                        Command =
                        [
                            "/bin/sh",
                            "-c",
                            "dotnet tool install -g Amazon.Lambda.Tools && dotnet build && dotnet lambda package --output-package /asset-output/function.zip"
                        ],
                    }
                }),
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Environment = new Dictionary<string, string>
                {
                    { "BUCKET_NAME", bucket.BucketName }
                }
            });

            // Grant Lambda permissions to use Bedrock
            function.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = ["bedrock:InvokeModel"],
                Resources = ["*"]
            }));

            // Grant Lambda permissions to access S3
            bucket.GrantReadWrite(function);

            // Create S3 Object Lambda Access Point
            var objectLambdaAccessPoint = new AccessPoint(this, "ObjectLambdaAccessPoint", new AccessPointProps
            {
                Bucket = bucket,
                Handler = function,
            });
        }
    }
} 