using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;


namespace DurableFuncForAzureContainerInstance.MyContainer
{
    public class HttpStart_MyApp_ACI
    {
        [FunctionName(nameof(HttpStart_MyApp_ACI))]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "get")]HttpRequestMessage req,
            [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(Orchestrator_Start_MyApp_ACI), MyAppACIRequirements.Instance);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            var response =  starter.CreateCheckStatusResponse(req, instanceId);
            
            return response;
        }
    }

    //sub workflow
    public static class Orchestrator_Start_MyApp_ACI
    {
        [FunctionName(nameof(Orchestrator_Start_MyApp_ACI))]
        public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var aciRequirements = context.GetInput<ACIRequirements>(); ;
            var (containerGroupInfo, _, maxProcessingTime) = aciRequirements;

            if (!context.IsReplaying)
            {
                log.LogInformation(containerGroupInfo.ResourceGroupName);
                log.LogInformation(containerGroupInfo.ContainerGroupName);
            }

            // start ACI
            await context.CallActivityAsync(nameof(Activity_StartACI), containerGroupInfo);

            var subOrchestrationId = $"{context.InstanceId}-1";
            var maximumRunDuration = context.CurrentUtcDateTime.AddMinutes(maxProcessingTime);

            // sub workflow,  wait for ACI exist (polling should be replaced when ACI Event feature is available)
            await context.CallSubOrchestratorAsync<object>(nameof(Orchestrator_AwaitForExiting_MyApp_ACI), subOrchestrationId, (maximumRunDuration, aciRequirements));
        }
    }

    public static class Orchestrator_AwaitForExiting_MyApp_ACI
    {
        [FunctionName(nameof(Orchestrator_AwaitForExiting_MyApp_ACI))]
        public static async Task<object> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {            
            var ( maximumRunDuration, aciRequirements )= context.GetInput<(DateTime, ACIRequirements)>();
            var ( containerGroupInfo, pollingInterval, maxProcessingTime) = aciRequirements;
            var ( resourceGroupName, containerGroupName ) = (containerGroupInfo.ResourceGroupName, containerGroupInfo.ContainerGroupName);
            
            var containerGroupStatus = 
                await context.CallActivityWithRetryAsync<ContainerGroupStatus>(
                    nameof(Activity_GetACIStatus),
                    new RetryOptions(TimeSpan.FromSeconds(30), 3)
                    { 
                        RetryTimeout = TimeSpan.FromSeconds(30)
                    },
                    containerGroupInfo);

            // if the container group has finished, return success status
            if (containerGroupStatus.Containers[0]?.CurrentState?.State == "Terminated")
            {
                var logContent = await context.CallActivityAsync<string>(nameof(Activity_GetACILogs), containerGroupInfo);
                bool isSuccess = CheckSuccessCode(logContent);
                if (isSuccess)
                {
                    return new { Success = true };
                }

                if (!context.IsReplaying)
                {
                    log.LogError($"logs: {logContent}");
                }
                //return new { Success = false, Message = logContent };
                throw new ApplicationException($"logs: {logContent}");
            }

            // the container group has not finished - sleep for N duration
            using (var cts = new CancellationTokenSource())
            {
                await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes( pollingInterval ), cts.Token);
            }

            // end workflow if we've been waiting too long
            if (context.CurrentUtcDateTime > maximumRunDuration)
            {
                if (!context.IsReplaying)
                {
                    log.LogWarning($"Exceeded processing time { maxProcessingTime } minutes.");
                }

                //return new { Success = false, Message = "Exceeded processing time." } ;
                throw new TimeoutException($"Exceeded processing time { maxProcessingTime } minutes.");
            }

            // container group is still working, restart this sub-orchestration with
            // the same input data
            context.ContinueAsNew((maximumRunDuration, aciRequirements));

            
            // return some values if needed
            return new { };
        }

        private static bool CheckSuccessCode(string logContent)
        {
            return !string.IsNullOrWhiteSpace(logContent) && logContent.Contains("EXIT_CODE_SUCCESS", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class Activity_StartACI
    {
        [FunctionName(nameof(Activity_StartACI))]
        public static async Task Run(
            [ActivityTrigger] ContainerGroupInfo containerGroupInfo,
            ILogger log)
        {
            var (resourceGroupName, containerGroupName) = (containerGroupInfo.ResourceGroupName, containerGroupInfo.ContainerGroupName);
            
            var azure = await AzHelpers.GetAzure();

            IContainerGroup containerGroup =
                await azure.ContainerGroups.GetByResourceGroupAsync(resourceGroupName, containerGroupName);

            if (containerGroup == null)
            {
                throw new Exception($"Container Instance is NOT found for {resourceGroupName}/{containerGroupName}");
            }

            await containerGroup.StopAsync();

            await Task.Delay(5000);

            log.LogInformation($"starting container: {resourceGroupName}/{containerGroupName} ....");

            await azure.ContainerGroups.StartAsync(resourceGroupName, containerGroupName);
        }
    }

    public static class Activity_GetACIStatus
    {
        [FunctionName(nameof(Activity_GetACIStatus))]
        public static async Task<ContainerGroupStatus> Run(
            [ActivityTrigger] ContainerGroupInfo containerGroupInfo,
            ILogger log)
        {
            var (resourceGroupName, containerGroupName) = (containerGroupInfo.ResourceGroupName, containerGroupInfo.ContainerGroupName);

            log.LogInformation($"Checking status {resourceGroupName}/{containerGroupName}");

            var azure = await AzHelpers.GetAzure();

            IContainerGroup containerGroup =
                await azure.ContainerGroups.GetByResourceGroupAsync(resourceGroupName, containerGroupName);

            if (containerGroup == null)
            {
                throw new Exception($"Container Instance is NOT found for { containerGroupName } in { resourceGroupName }");
            }

            var containerGroupStatus = new ContainerGroupStatus()
            {
                State = containerGroup.State,
                Id = containerGroup.Id,
                Name = containerGroup.Name,
                ResourceGroupName = containerGroup.ResourceGroupName,
                Containers = containerGroup.Containers.Values.Select(c => new ContainerInstanceStatus()
                {
                    Name = c.Name,
                    CurrentState = c.InstanceView?.CurrentState,
                    RestartCount = c.InstanceView?.RestartCount,
                }).ToArray()
            };

            return containerGroupStatus;
        }
    }

    public static class Activity_GetACILogs
    {
        [FunctionName(nameof(Activity_GetACILogs))]
        public static async Task<string> Run(
            [ActivityTrigger] ContainerGroupInfo containerGroupInfo,
            ILogger log)
        {
            var (resourceGroupName, containerGroupName) = (containerGroupInfo.ResourceGroupName, containerGroupInfo.ContainerGroupName);

            log.LogInformation($"Fetching logs {resourceGroupName}/{containerGroupName}");

            var azure = await AzHelpers.GetAzure();

            var logContents =
                await azure.ContainerGroups.GetLogContentAsync(resourceGroupName, containerGroupName, containerGroupName);
            
            return logContents;
        }
    }
}