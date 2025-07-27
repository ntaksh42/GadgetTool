using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// Azure DevOps Pull Request サービスのインターフェース
    /// </summary>
    public interface IAzureDevOpsPullRequestService : IDisposable
    {
        Task<IEnumerable<PullRequest>> GetPullRequestsAsync();
        Task<IEnumerable<PullRequest>> GetPullRequestsAsync(string project, string repository);
        Task<IEnumerable<PullRequest>> GetPullRequestsAsync(string project, string repository, PullRequestSearchOptions? options = null);
    }
    
    /// <summary>
    /// Pull Request検索オプション
    /// </summary>
    public class PullRequestSearchOptions
    {
        public DateTime? FromDate { get; set; }
        public string? AuthorFilter { get; set; }
        public string? TargetBranchFilter { get; set; }
        
        public static PullRequestSearchOptions Empty => new();
    }

    /// <summary>
    /// Azure DevOps Pull Request サービス
    /// </summary>
    public class AzureDevOpsPullRequestService : IAzureDevOpsPullRequestService, IDisposable
    {
        private readonly AzureDevOpsConfig _config;
        private readonly VssConnection _connection;

        public AzureDevOpsPullRequestService(AzureDevOpsConfig config)
        {
            _config = config;
            var credentials = new VssBasicCredential(string.Empty, _config.PersonalAccessToken);
            _connection = new VssConnection(new Uri($"https://dev.azure.com/{_config.Organization}"), credentials);
        }

        public async Task<IEnumerable<PullRequest>> GetPullRequestsAsync()
        {
            return await GetPullRequestsAsync(_config.Project, _config.Repository);
        }

        public async Task<IEnumerable<PullRequest>> GetPullRequestsAsync(string project, string repository)
        {
            return await GetPullRequestsAsync(project, repository, PullRequestSearchOptions.Empty);
        }

        public async Task<IEnumerable<PullRequest>> GetPullRequestsAsync(string project, string repository, PullRequestSearchOptions? options = null)
        {
            using var gitClient = _connection.GetClient<GitHttpClient>();
            
            var pullRequests = await gitClient.GetPullRequestsAsync(
                project: project,
                repositoryId: repository,
                searchCriteria: new GitPullRequestSearchCriteria()
            );

            var results = new List<PullRequest>();
            var detailTasks = new List<Task<PullRequest>>();
            
            // 並列処理でPR詳細を取得
            foreach (var pr in pullRequests)
            {
                detailTasks.Add(GetPullRequestDetailAsync(gitClient, project, repository, pr));
            }
            
            var detailedPullRequests = await Task.WhenAll(detailTasks);
            results.AddRange(detailedPullRequests);

            return ApplyFilters(results, options ?? PullRequestSearchOptions.Empty);
        }
        
        #region Private Methods
        
        private static IEnumerable<PullRequest> ApplyFilters(IEnumerable<PullRequest> pullRequests, PullRequestSearchOptions options)
        {
            var filtered = pullRequests.AsEnumerable();
            
            if (options.FromDate.HasValue)
            {
                filtered = filtered.Where(pr => pr.CreatedDate >= options.FromDate.Value);
            }
            
            if (!string.IsNullOrWhiteSpace(options.AuthorFilter))
            {
                var authorFilter = options.AuthorFilter.ToLowerInvariant();
                filtered = filtered.Where(pr => pr.CreatedBy.ToLowerInvariant().Contains(authorFilter));
            }
            
            if (!string.IsNullOrWhiteSpace(options.TargetBranchFilter))
            {
                var targetFilter = options.TargetBranchFilter.ToLowerInvariant();
                filtered = filtered.Where(pr => pr.TargetBranch.ToLowerInvariant().Contains(targetFilter));
            }
            
            return filtered;
        }
        
        private async Task<PullRequest> GetPullRequestDetailAsync(GitHttpClient gitClient, string project, string repository, Microsoft.TeamFoundation.SourceControl.WebApi.GitPullRequest pr)
        {
            var modifiedFiles = await GetPullRequestFilesAsync(gitClient, project, repository, pr.PullRequestId);
            var reviewers = await GetPullRequestReviewersAsync(gitClient, project, repository, pr.PullRequestId);
            var workItems = await GetPullRequestWorkItemsAsync(gitClient, project, repository, pr.PullRequestId);
            var (addedLines, deletedLines) = await GetPullRequestStatsAsync(gitClient, project, repository, pr.PullRequestId);
            
            return new PullRequest
            {
                Id = pr.PullRequestId,
                Title = pr.Title ?? string.Empty,
                Description = pr.Description ?? string.Empty,
                Status = pr.Status.ToString(),
                CreatedBy = pr.CreatedBy?.DisplayName ?? string.Empty,
                CreatedDate = pr.CreationDate,
                SourceBranch = pr.SourceRefName ?? string.Empty,
                TargetBranch = pr.TargetRefName ?? string.Empty,
                Repository = repository,
                Project = project,
                Url = GeneratePullRequestUrl(pr.PullRequestId, project, repository),
                ModifiedFiles = modifiedFiles,
                IsDraft = pr.IsDraft ?? false,
                Reviewers = reviewers,
                RelatedWorkItems = workItems,
                AddedLines = addedLines,
                DeletedLines = deletedLines,
                ChangedFiles = modifiedFiles.Count,
                CompletedDate = pr.ClosedDate,
                ClosedBy = pr.ClosedBy?.DisplayName ?? string.Empty
            };
        }

        private async Task<List<string>> GetPullRequestFilesAsync(GitHttpClient gitClient, string project, string repository, int pullRequestId)
        {
            try
            {
                var iterations = await gitClient.GetPullRequestIterationsAsync(project, repository, pullRequestId);
                if (iterations.Any())
                {
                    var latestIteration = iterations.Last();
                    var changes = await gitClient.GetPullRequestIterationChangesAsync(project, repository, pullRequestId, latestIteration.Id ?? 0);
                    
                    return changes.ChangeEntries
                        .Where(change => change.Item != null && !string.IsNullOrEmpty(change.Item.Path))
                        .Select(change => change.Item.Path.TrimStart('/'))
                        .ToList();
                }
            }
            catch
            {
                // If we can't get files, return empty list
            }
            
            return new List<string>();
        }

        private async Task<List<Reviewer>> GetPullRequestReviewersAsync(GitHttpClient gitClient, string project, string repository, int pullRequestId)
        {
            try
            {
                var reviewers = await gitClient.GetPullRequestReviewersAsync(project, repository, pullRequestId);
                return reviewers.Select(r => new Reviewer
                {
                    DisplayName = r.DisplayName ?? string.Empty,
                    UniqueDisplayName = r.UniqueName ?? string.Empty,
                    Vote = r.Vote,
                    IsRequired = r.IsRequired,
                    HasDeclined = r.HasDeclined.GetValueOrDefault()
                }).ToList();
            }
            catch
            {
                return new List<Reviewer>();
            }
        }

        private async Task<List<WorkItem>> GetPullRequestWorkItemsAsync(GitHttpClient gitClient, string project, string repository, int pullRequestId)
        {
            try
            {
                var workItemRefs = await gitClient.GetPullRequestWorkItemRefsAsync(project, repository, pullRequestId);
                var workItems = new List<WorkItem>();
                
                foreach (var workItemRef in workItemRefs)
                {
                    if (int.TryParse(workItemRef.Id, out int workItemId))
                    {
                        workItems.Add(new WorkItem
                        {
                            Id = workItemId,
                            Title = $"Work Item {workItemId}", // ResourceRef doesn't have Title property
                            Type = "WorkItem", // Default type
                            State = "Active" // Default state
                        });
                    }
                }
                
                return workItems;
            }
            catch
            {
                return new List<WorkItem>();
            }
        }

        private async Task<(int addedLines, int deletedLines)> GetPullRequestStatsAsync(GitHttpClient gitClient, string project, string repository, int pullRequestId)
        {
            try
            {
                var iterations = await gitClient.GetPullRequestIterationsAsync(project, repository, pullRequestId);
                if (iterations.Any())
                {
                    var latestIteration = iterations.Last();
                    var changes = await gitClient.GetPullRequestIterationChangesAsync(project, repository, pullRequestId, latestIteration.Id ?? 0);
                    
                    int addedLines = 0;
                    int deletedLines = 0;
                    
                    foreach (var change in changes.ChangeEntries)
                    {
                        if (change.Item != null && change.Item.GitObjectType == Microsoft.TeamFoundation.SourceControl.WebApi.GitObjectType.Blob)
                        {
                            // For simplicity, we'll estimate based on the number of changes
                            // In a real implementation, you'd need to get the actual diff
                            addedLines += 10; // Placeholder
                            deletedLines += 5; // Placeholder
                        }
                    }
                    
                    return (addedLines, deletedLines);
                }
            }
            catch
            {
                // If we can't get stats, return zeros
            }
            
            return (0, 0);
        }

        private string GeneratePullRequestUrl(int pullRequestId, string project, string repository)
        {
            return $"https://dev.azure.com/{_config.Organization}/{project}/_git/{repository}/pullrequest/{pullRequestId}";
        }

        #endregion
        
        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}