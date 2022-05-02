namespace Mutedac.Cicd.BuildDriver
{
    /// <summary>
    /// CloudFormation outputs of the Artifacts Stack.
    /// </summary>
    public class Outputs
    {
        /// <summary>
        /// Gets or sets the uri of the repository containing Brighid Discord Adapter Container Images.
        /// </summary>
        public string ImageRepositoryUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the artifact bucket.
        /// </summary>
        public string BucketName { get; set; } = string.Empty;
    }
}
