using System;
using System.Collections.Generic;
using System.Text;

namespace DurableFuncForAzureContainerInstance
{
    public static class Configuration
    {
        public static Func<string> TenantId       = () => Environment.GetEnvironmentVariable("TenantId");

        public static Func<string> SubscriptionId = () => Environment.GetEnvironmentVariable("SubscriptionId");
    
    }
}
