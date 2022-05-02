namespace Mutedac.Cicd.DeployDriver
{
    /// <summary>
    /// Context holder for ecs deployment info.
    /// </summary>
    public class EcsDeployContext
    {
        /// <summary>
        /// Gets or sets the name of the cluster where the service to deploy is located.
        /// </summary>
        public string ClusterName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the service to deploy.
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
    }
}
