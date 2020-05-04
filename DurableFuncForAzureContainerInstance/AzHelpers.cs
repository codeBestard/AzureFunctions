using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using static Microsoft.Azure.Management.Fluent.Azure;

namespace DurableFuncForAzureContainerInstance
{
    public static class AzHelpers
    {
        public static async Task<IAzure> GetAzure()
        {
            var tenantId = Configuration.TenantId();

            IAuthenticated authenicatedAzure;

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // local auth
                authenicatedAzure = await GetAzureByTokenProvider();
            }
            else
            {
                // cloud auth
                authenicatedAzure = await GetAzureFromMSI();
            }

            var subscriptionId = Configuration.SubscriptionId();
            if (!string.IsNullOrEmpty(subscriptionId))
                return authenicatedAzure.WithSubscription(subscriptionId);

            return await authenicatedAzure.WithDefaultSubscriptionAsync();
        }

        private static async Task<IAuthenticated> GetAzureFromMSI()
        {            
            var credentials = SdkContext
                .AzureCredentialsFactory
                .FromMSI(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud);

            var authenticatedAzure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials);

            return await Task.FromResult<IAuthenticated>(authenticatedAzure);

        }

        private static async Task<IAuthenticated> GetAzureByTokenProvider()
        {
            var tenantId = Configuration.TenantId();
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var token = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com", tenantId);
            var tokenCredentials = new TokenCredentials(token);
            var authenticatedAzure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(new AzureCredentials(
                    tokenCredentials,
                    tokenCredentials,
                    tenantId,
                    AzureEnvironment.AzureGlobalCloud));
            
            return authenticatedAzure;
        }


    }
}
