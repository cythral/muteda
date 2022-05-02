using Amazon.CDK;

using Mutedac.Artifacts;

#pragma warning disable SA1516

var app = new App();
_ = new ArtifactsStack(app, "mutedac-cicd", new StackProps
{
    Synthesizer = new BootstraplessSynthesizer(new BootstraplessSynthesizerProps()),
});

app.Synth();
