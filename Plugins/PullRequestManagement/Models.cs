using System;
using System.Collections.Generic;
using System.Linq;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// Pull Request情報
    /// </summary>
    public class PullRequest
    {
        #region Basic Properties
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string SourceBranch { get; set; } = string.Empty;
        public string TargetBranch { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<string> ModifiedFiles { get; set; } = new();
        #endregion
        
        #region Extended Details
        public int CommentCount { get; set; }
        public int UnresolvedCommentCount { get; set; }
        public List<Reviewer> Reviewers { get; set; } = new();
        public bool IsDraft { get; set; }
        public bool HasMergeConflicts { get; set; }
        public string BuildStatus { get; set; } = string.Empty;
        public List<WorkItem> RelatedWorkItems { get; set; } = new();
        public int AddedLines { get; set; }
        public int DeletedLines { get; set; }
        public int ChangedFiles { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string ClosedBy { get; set; } = string.Empty;
        #endregion
        
        #region Display Properties
        public string ModifiedFilesDisplay => string.Join(", ", ModifiedFiles);
        public string ReviewersDisplay => string.Join(", ", Reviewers.Select(r => r.DisplayName));
        public string ApprovalStatus => GetApprovalStatusText();
        public string ApprovalStatusShort => $"{ApprovedCount}/{Reviewers.Count}";
        public string WorkItemsDisplay => string.Join(", ", RelatedWorkItems.Select(w => $"#{w.Id}"));
        public string ChangesSummary => $"+{AddedLines} -{DeletedLines} ({ChangedFiles} files)";
        #endregion
        
        #region Icon Properties
        public string StatusIcon => GetStatusIcon();
        public string ApprovalIcon => GetApprovalIcon();
        public string BuildStatusIcon => GetBuildStatusIcon();
        public string ConflictIcon => HasMergeConflicts ? "⚠️" : "✅";
        #endregion
        
        #region Helper Properties
        private int ApprovedCount => Reviewers.Count(r => r.Vote > 0);
        private int RejectedCount => Reviewers.Count(r => r.Vote < 0);
        #endregion
        
        #region Helper Methods
        private string GetStatusIcon() => Status.ToLower() switch
        {
            "active" => "🔵",
            "completed" => "✅",
            "abandoned" => "❌",
            "draft" => "📝",
            _ => "⚪"
        };
        
        private string GetApprovalIcon()
        {
            if (!Reviewers.Any()) return "👤";
            
            return RejectedCount > 0 ? "❌" :
                   ApprovedCount == Reviewers.Count ? "✅" :
                   ApprovedCount > 0 ? "🟡" : "⏳";
        }
        
        private string GetBuildStatusIcon() => BuildStatus.ToLower() switch
        {
            "succeeded" => "✅",
            "failed" => "❌",
            "partiallySucceeded" => "🟡",
            "inprogress" => "🔄",
            _ => "⚪"
        };
        
        private string GetApprovalStatusText() => $"{ApprovedCount}/{Reviewers.Count} approved";
        #endregion
    }

    /// <summary>
    /// Pull Requestレビューアー情報
    /// </summary>
    public class Reviewer
    {
        public string DisplayName { get; set; } = string.Empty;
        public string UniqueDisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// 投票状況: -10=rejected, -5=waiting, 0=no vote, 5=approved with suggestions, 10=approved
        /// </summary>
        public int Vote { get; set; }
        
        public bool IsRequired { get; set; }
        public bool HasDeclined { get; set; }
        
        public ReviewStatus Status => Vote switch
        {
            > 0 => ReviewStatus.Approved,
            < 0 => ReviewStatus.Rejected,
            _ => ReviewStatus.Waiting
        };
    }
    
    public enum ReviewStatus
    {
        Waiting,
        Approved,
        Rejected
    }

    /// <summary>
    /// 関連ワークアイテム情報
    /// </summary>
    public class WorkItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    /// <summary>
    /// Azure DevOps設定情報
    /// </summary>
    public class AzureDevOpsConfig
    {
        public string Organization { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
    }

    /// <summary>
    /// 保存済み検索条件
    /// </summary>
    public class SavedSearch
    {
        public string Name { get; set; } = string.Empty;
        public string AuthorFilter { get; set; } = string.Empty;
        public string TargetBranchFilter { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public int MinChanges { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// プラグイン設定情報
    /// </summary>
    public class PullRequestManagementSettings
    {
        #region Azure DevOps Configuration
        public string Project { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        #endregion
        
        #region Filter Settings
        public string AuthorFilter { get; set; } = string.Empty;
        public string TargetBranchFilter { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string FileExtensionFilter { get; set; } = string.Empty;
        public string MinChangesFilter { get; set; } = string.Empty;
        public string SelectedStatus { get; set; } = "All";
        public DateTime? FromDate { get; set; }
        
        // Multi-selection settings
        public List<string> Projects { get; set; } = new();
        public List<string> Repositories { get; set; } = new();
        public List<string> AllProjects { get; set; } = new();
        public List<string> AllRepositories { get; set; } = new();
        
        // Display settings
        public bool IsDetailPaneVisible { get; set; } = false;
        #endregion
        
        #region UI Settings
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 600;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public bool[] ColumnVisibility { get; set; } = Enumerable.Repeat(true, 9).ToArray();
        #endregion
        
        #region Saved Searches
        public List<SavedSearch> SavedSearches { get; set; } = new();
        #endregion
    }
}