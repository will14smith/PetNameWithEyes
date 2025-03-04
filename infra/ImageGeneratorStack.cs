using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3ObjectLambda;
using Constructs;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;

namespace Infrastructure
{
    public class ImageGeneratorStack : Stack
    {
        public ImageGeneratorStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
        {
            var organizationId = "o-orgidhere";

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

            function.GrantInvoke(new OrganizationPrincipal(organizationId));
            
            // Grant Lambda permissions to access S3
            bucket.GrantReadWrite(function);
            bucket.GrantReadWrite(new OrganizationPrincipal(organizationId));

            // Create S3 Object Lambda Access Point
            var s3AccessPoint = new Amazon.CDK.AWS.S3.CfnAccessPoint(this, "SupportingAccessPoint", new Amazon.CDK.AWS.S3.CfnAccessPointProps 
            {
                Name = "pet-name-image-generator",
                Bucket = bucket.BucketName,
            });
            s3AccessPoint.Policy = new PolicyDocument(new PolicyDocumentProps
            {
                Statements =
                [
                    new PolicyStatement(new PolicyStatementProps
                    {
                        Effect = Effect.ALLOW,
                        Principals = [new OrganizationPrincipal(organizationId)],
                        Actions = ["s3:GetObject", "s3:PutObject"],
                        Resources = [$"arn:{Aws.PARTITION}:s3:{Aws.REGION}:{Aws.ACCOUNT_ID}:accesspoint/{s3AccessPoint.Name}/object/*"]
                    })
                ]
            });

            var objectLambdaAccessPoint = new Amazon.CDK.AWS.S3ObjectLambda.CfnAccessPoint(this, "ObjectLambdaAccessPoint", new Amazon.CDK.AWS.S3ObjectLambda.CfnAccessPointProps
            {
                ObjectLambdaConfiguration = new Amazon.CDK.AWS.S3ObjectLambda.CfnAccessPoint.ObjectLambdaConfigurationProperty
                {
                    SupportingAccessPoint = s3AccessPoint.AttrArn,
                    TransformationConfigurations = new [] {
                        new Amazon.CDK.AWS.S3ObjectLambda.CfnAccessPoint.TransformationConfigurationProperty
                        {
                            Actions = ["GetObject"],
                            ContentTransformation = new Dictionary<string, object>
                            {
                                { 
                                    "AwsLambda",
                                    new Dictionary<string, object>
                                    {
                                        {"FunctionArn", function.FunctionArn}
                                    }
                                }
                            }
                        }
                    },
                    AllowedFeatures = [],
                    CloudWatchMetricsEnabled = false
                }
            });
            
            var objectLambdaAccessPointPolicy = new CfnAccessPointPolicy(this, "ObjectLambdaAccessPointPolicy", new CfnAccessPointPolicyProps
            {
                ObjectLambdaAccessPoint = objectLambdaAccessPoint.Ref,
                PolicyDocument = new PolicyDocument(new PolicyDocumentProps
                {
                    Statements = [
                        new PolicyStatement(new PolicyStatementProps
                        {
                            Effect = Effect.ALLOW,
                            Principals = [new OrganizationPrincipal(organizationId)],
                            Actions = ["s3-object-lambda:*"],
                            Resources = [objectLambdaAccessPoint.AttrArn]
                        })
                    ]
                })
            });

            function.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = ["*"],
                Actions = ["s3-object-lambda:WriteGetObjectResponse"],
            }));
        }
    }
} 