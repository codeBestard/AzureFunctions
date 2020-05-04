using System;

namespace DurableFuncForAzureContainerInstance.MyContainer
{
    public static class MyAppACIRequirements
    {
        private static readonly Lazy<ACIRequirements> _lazyMyAppContainerGroupInfo = new Lazy<ACIRequirements>(
            () =>

            new ACIRequirements
            {
                ContainerGroupInfo =
                                 new ContainerGroupInfo
                                 {
                                     ResourceGroupName = Environment.GetEnvironmentVariable("ResourceGroup"),
                                     ContainerGroupName = Environment.GetEnvironmentVariable("ACIGroup")
                                 },

                PollingInterval = Int32.Parse(Environment.GetEnvironmentVariable("PollingIntervalInMinutes")),
                MaxProcessingTime = Int32.Parse(Environment.GetEnvironmentVariable("MaxProcessingTimeInMinutes"))
            }

            );


        public static ACIRequirements Instance => _lazyMyAppContainerGroupInfo.Value;
    }
}
