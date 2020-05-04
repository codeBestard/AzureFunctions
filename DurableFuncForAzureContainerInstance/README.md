# Trigger Azure Container Instance From Azure durable function

An example of managing a stateful and long running Azure container instance with Durable function.

To Launch the project locally, we'll also need to add a `local.settings.json` to the project and enter configuration values.
```json
{
    "IsEncrypted": false,
    "Values": {
      "AzureWebJobsStorage": "UseDevelopmentStorage=true",
      "FUNCTIONS_WORKER_RUNTIME": "dotnet",

      "ResourceGroup": "durablefunctionsdemo",
      "ACIGroup": "mycontainer",
      "PollingIntervalInMinutes": 1,
      "MaxProcessingTimeInMinutes": 10,
      "TenantId": "fill-in",
      "SubscriptionId": "fill-in"
  }
}
```