using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using System.Collections.Generic;

namespace DurableFuncForAzureContainerInstance
{
    public class ContainerInstanceStatus
    {
        public string Name { get; set; }               
        public ContainerState CurrentState { get; set; }
        public int? RestartCount { get; set; }
    }
}
