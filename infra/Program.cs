using Amazon.CDK;

namespace Infrastructure;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();
        new ImageGeneratorStack(app, "ImageGeneratorStack", new StackProps());
        app.Synth();
    }
}