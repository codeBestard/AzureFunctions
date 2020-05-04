using System;
using System.Collections.Generic;
using System.Text;

namespace DurableFuncForAzureContainerInstance
{
    public class ContainerGroupStatus
    {
        public ContainerInstanceStatus[] Containers { get; set; }
        public string State { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string ResourceGroupName { get; set; }
    }
}
