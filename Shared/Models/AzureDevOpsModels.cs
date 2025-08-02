using Newtonsoft.Json;

namespace GadgetTools.Shared.Models
{
    /// <summary>
    /// Azure DevOps configuration model
    /// </summary>
    public class AzureDevOpsConfig
    {
        public string Organization { get; set; } = "";
        public string Project { get; set; } = "";
        public string PersonalAccessToken { get; set; } = "";
        public string BaseUrl => $"https://dev.azure.com/{Organization}";
    }

    /// <summary>
    /// Response model for Azure DevOps Work Items API
    /// </summary>
    public class WorkItemResponse
    {
        [JsonProperty("value")]
        public List<WorkItem> Value { get; set; } = new List<WorkItem>();

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    /// <summary>
    /// Azure DevOps Work Item model
    /// </summary>
    public class WorkItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("rev")]
        public int Rev { get; set; }

        [JsonProperty("fields")]
        public WorkItemFields Fields { get; set; } = new WorkItemFields();

        [JsonProperty("url")]
        public string Url { get; set; } = "";
    }

    /// <summary>
    /// Work Item fields collection
    /// </summary>
    public class WorkItemFields
    {
        [JsonProperty("System.Id")]
        public int SystemId { get; set; }

        [JsonProperty("System.WorkItemType")]
        public string WorkItemType { get; set; } = "";

        [JsonProperty("System.State")]
        public string State { get; set; } = "";

        [JsonProperty("System.Title")]
        public string Title { get; set; } = "";

        [JsonProperty("System.Description")]
        public string Description { get; set; } = "";

        [JsonProperty("System.AssignedTo")]
        public AssignedPerson? AssignedTo { get; set; }

        [JsonProperty("System.CreatedBy")]
        public AssignedPerson? CreatedBy { get; set; }

        [JsonProperty("System.CreatedDate")]
        public DateTime CreatedDate { get; set; }

        [JsonProperty("System.ChangedDate")]
        public DateTime ChangedDate { get; set; }

        [JsonProperty("System.AreaPath")]
        public string AreaPath { get; set; } = "";

        [JsonProperty("System.IterationPath")]
        public string IterationPath { get; set; } = "";

        [JsonProperty("System.Tags")]
        public string Tags { get; set; } = "";

        [JsonProperty("Microsoft.VSTS.Common.Priority")]
        public int Priority { get; set; } = 2;

        [JsonProperty("Microsoft.VSTS.Common.Severity")]
        public string Severity { get; set; } = "";

        [JsonProperty("Microsoft.VSTS.TCM.ReproSteps")]
        public string ReproSteps { get; set; } = "";

        [JsonProperty("Microsoft.VSTS.Common.AcceptanceCriteria")]
        public string AcceptanceCriteria { get; set; } = "";
    }

    /// <summary>
    /// Assigned person information
    /// </summary>
    public class AssignedPerson
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonProperty("uniqueName")]
        public string UniqueName { get; set; } = "";

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; } = "";
    }

    /// <summary>
    /// Work Item comments response
    /// </summary>
    public class WorkItemCommentsResponse
    {
        [JsonProperty("comments")]
        public List<WorkItemComment> Comments { get; set; } = new List<WorkItemComment>();

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Individual work item comment
    /// </summary>
    public class WorkItemComment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("workItemId")]
        public int WorkItemId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; } = "";

        [JsonProperty("createdBy")]
        public AssignedPerson? CreatedBy { get; set; }

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; }

        [JsonProperty("modifiedBy")]
        public AssignedPerson? ModifiedBy { get; set; }

        [JsonProperty("modifiedDate")]
        public DateTime ModifiedDate { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; } = "";
    }

    /// <summary>
    /// Work Item query request parameters
    /// </summary>
    public class WorkItemQueryRequest
    {
        public string Organization { get; set; } = "";
        public string Project { get; set; } = "";
        public string WorkItemType { get; set; } = ""; // Bug, Task, User Story, etc. (empty = all types)
        public string State { get; set; } = ""; // Active, Resolved, Closed, etc.
        public string AssignedTo { get; set; } = "";
        public int MaxResults { get; set; } = 100;
        public string AreaPath { get; set; } = "";
        public string IterationPath { get; set; } = "";
        public string Area { get; set; } = ""; // UI用のエイリアス
        public string Iteration { get; set; } = ""; // UI用のエイリアス
    }
}