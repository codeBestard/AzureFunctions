namespace DurableFuncForAzureContainerInstance
{
    public class ACIRequirements
    {
        public ContainerGroupInfo ContainerGroupInfo { get; set; }

        public int PollingInterval { get; set; }
        public int MaxProcessingTime { get; set; }

        public void Deconstruct(out ContainerGroupInfo containerGroupInfo, out int pollingInterval, out int maxProcessingTime)
        {
            containerGroupInfo = ContainerGroupInfo;
            pollingInterval    = PollingInterval;
            maxProcessingTime  = MaxProcessingTime;
        }
    }
}
