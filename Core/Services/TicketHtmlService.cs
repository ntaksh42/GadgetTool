using System;
using System.Net;
using System.Text;
using GadgetTools.Shared.Models;

namespace GadgetTools.Core.Services
{
    public static class TicketHtmlService
    {
        private const int MAX_CONTENT_SIZE = 1024 * 1024; // 1MB limit

        public static string GenerateWorkItemHtml(WorkItem workItem, List<WorkItemComment>? comments = null)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            try
            {
                var html = GenerateWorkItemHtmlInternal(workItem, comments);
                
                if (!ValidateHtmlContent(html))
                {
                    return GenerateErrorHtml("Generated content validation failed");
                }
                
                return html;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating work item HTML: {ex.Message}");
                return GenerateErrorHtml($"Failed to generate preview: {ex.Message}");
            }
        }

        public static string GenerateEmptyStateHtml()
        {
            return GenerateHtmlDocument(@"
                <div class='empty-state'>
                    <div class='empty-icon'>🎯</div>
                    <div class='empty-title'>チケットプレビュー</div>
                    <p>WebView2が正常に動作しています。</p>
                    <p>ワークアイテムをクエリしてチケットを選択してください。</p>
                    <div class='instructions'>
                        <h4>使い方:</h4>
                        <ol>
                            <li>Azure DevOpsの設定を行う</li>
                            <li>プロジェクト名を入力</li>
                            <li>「Query Work Items」を実行</li>
                            <li>表示されたチケットを選択</li>
                        </ol>
                    </div>
                </div>
            ");
        }

        public static string GenerateListViewHtml(System.Collections.Generic.List<WorkItem> workItems, string title)
        {
            if (workItems == null || workItems.Count == 0)
                return GenerateEmptyStateHtml();

            try
            {
                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine($"<div class='list-header'>");
                contentBuilder.AppendLine($"    <h1>{WebUtility.HtmlEncode(title)}</h1>");
                contentBuilder.AppendLine($"    <p class='item-count'>{workItems.Count} items found</p>");
                contentBuilder.AppendLine($"</div>");

                contentBuilder.AppendLine("<div class='work-items-grid'>");
                
                foreach (var item in workItems)
                {
                    contentBuilder.AppendLine($@"
                        <div class='work-item-card'>
                            <div class='work-item-header'>
                                <span class='work-item-id'>#{item.Id}</span>
                                <span class='work-item-type badge badge-type'>{WebUtility.HtmlEncode(item.Fields.WorkItemType ?? "")}</span>
                            </div>
                            <div class='work-item-title'>{WebUtility.HtmlEncode(item.Fields.Title ?? "")}</div>
                            <div class='work-item-meta'>
                                <span class='work-item-state badge badge-state'>{WebUtility.HtmlEncode(item.Fields.State ?? "")}</span>
                                <span class='work-item-assignee'>{WebUtility.HtmlEncode(item.Fields.AssignedTo?.DisplayName ?? "Unassigned")}</span>
                            </div>
                        </div>
                    ");
                }

                contentBuilder.AppendLine("</div>");
                
                return GenerateHtmlDocument(contentBuilder.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating list view HTML: {ex.Message}");
                return GenerateErrorHtml($"Failed to generate list view: {ex.Message}");
            }
        }

        private static string GenerateWorkItemHtmlInternal(WorkItem workItem, List<WorkItemComment>? comments = null)
        {
            // Debug logging for comments
            System.Diagnostics.Debug.WriteLine($"TicketHtmlService: Generating HTML for Work Item #{workItem.Id}");
            System.Diagnostics.Debug.WriteLine($"TicketHtmlService: Comments count: {comments?.Count ?? 0}");
            if (comments != null && comments.Count > 0)
            {
                for (int i = 0; i < Math.Min(3, comments.Count); i++)
                {
                    var comment = comments[i];
                    System.Diagnostics.Debug.WriteLine($"TicketHtmlService: Comment {i + 1} - ID: {comment.Id}, Text: {comment.Text?.Substring(0, Math.Min(50, comment.Text?.Length ?? 0))}..., Author: {comment.CreatedBy?.DisplayName}");
                }
            }

            var contentBuilder = new StringBuilder();
            
            // Header section
            contentBuilder.AppendLine($@"
                <div class='ticket-container'>
                    <div class='ticket-header'>
                        <div class='ticket-id'>Work Item #{workItem.Id}</div>
                        <div class='ticket-badges'>
                            <span class='badge badge-type'>{WebUtility.HtmlEncode(workItem.Fields.WorkItemType ?? "")}</span>
                            <span class='badge badge-state'>{WebUtility.HtmlEncode(workItem.Fields.State ?? "")}</span>
                            {(workItem.Fields.Priority == 0 ? "" : $"<span class='badge badge-priority'>{workItem.Fields.Priority}</span>")}
                        </div>
                    </div>
                    
                    <div class='ticket-title'>{WebUtility.HtmlEncode(workItem.Fields.Title ?? "")}</div>
                    
                    <div class='ticket-metadata'>
                        <div class='metadata-grid'>
                            <div class='metadata-item'>
                                <span class='metadata-label'>Assigned To:</span>
                                <span class='metadata-value'>{WebUtility.HtmlEncode(workItem.Fields.AssignedTo?.DisplayName ?? "Unassigned")}</span>
                            </div>
                            <div class='metadata-item'>
                                <span class='metadata-label'>Created:</span>
                                <span class='metadata-value'>{workItem.Fields.CreatedDate:yyyy-MM-dd HH:mm}</span>
                            </div>
                            <div class='metadata-item'>
                                <span class='metadata-label'>Last Updated:</span>
                                <span class='metadata-value'>{workItem.Fields.ChangedDate:yyyy-MM-dd HH:mm}</span>
                            </div>
                            {(!string.IsNullOrEmpty(workItem.Fields.Tags) ? $@"
                            <div class='metadata-item'>
                                <span class='metadata-label'>Tags:</span>
                                <span class='metadata-value'>{WebUtility.HtmlEncode(workItem.Fields.Tags)}</span>
                            </div>" : "")}
                        </div>
                    </div>
            ");

            // Description section
            if (!string.IsNullOrEmpty(workItem.Fields.Description))
            {
                var safeDescription = WebUtility.HtmlEncode(workItem.Fields.Description);
                contentBuilder.AppendLine($@"
                    <div class='ticket-section'>
                        <h3 class='section-title'>Description</h3>
                        <div class='description-content'>
                            <pre class='description-text'>{safeDescription}</pre>
                        </div>
                    </div>
                ");
            }

            // Acceptance Criteria section (if available)
            if (!string.IsNullOrEmpty(workItem.Fields.AcceptanceCriteria))
            {
                var safeCriteria = WebUtility.HtmlEncode(workItem.Fields.AcceptanceCriteria);
                contentBuilder.AppendLine($@"
                    <div class='ticket-section'>
                        <h3 class='section-title'>Acceptance Criteria</h3>
                        <div class='criteria-content'>
                            <pre class='criteria-text'>{safeCriteria}</pre>
                        </div>
                    </div>
                ");
            }

            // Discussion section
            if (comments != null && comments.Count > 0)
            {
                contentBuilder.AppendLine($@"
                    <div class='ticket-section discussion-section'>
                        <h3 class='section-title'>
                            💬 Discussion ({comments.Count} comments)
                        </h3>
                        <div class='discussion-content'>
                ");

                // Sort comments by created date (oldest first)
                var sortedComments = comments.OrderBy(c => c.CreatedDate).ToList();
                
                foreach (var comment in sortedComments)
                {
                    var safeCommentText = WebUtility.HtmlEncode(comment.Text);
                    var authorName = WebUtility.HtmlEncode(comment.CreatedBy?.DisplayName ?? "Unknown");
                    var createdDate = comment.CreatedDate.ToString("yyyy-MM-dd HH:mm");
                    var isModified = comment.ModifiedDate > comment.CreatedDate.AddMinutes(1);
                    var modifiedInfo = isModified ? $" (edited {comment.ModifiedDate:yyyy-MM-dd HH:mm})" : "";

                    contentBuilder.AppendLine($@"
                        <div class='comment-item'>
                            <div class='comment-header'>
                                <div class='comment-author'>{authorName}</div>
                                <div class='comment-meta'>
                                    <span class='comment-date'>{createdDate}</span>
                                    {(isModified ? $"<span class='comment-modified'>{modifiedInfo}</span>" : "")}
                                </div>
                            </div>
                            <div class='comment-body'>
                                <pre class='comment-text'>{safeCommentText}</pre>
                            </div>
                        </div>
                    ");
                }

                contentBuilder.AppendLine(@"
                        </div>
                    </div>
                ");
            }
            else if (comments != null && comments.Count == 0)
            {
                contentBuilder.AppendLine($@"
                    <div class='ticket-section discussion-section'>
                        <h3 class='section-title'>💬 Discussion</h3>
                        <div class='discussion-content'>
                            <div class='no-comments'>
                                <div class='no-comments-icon'>💭</div>
                                <div class='no-comments-text'>No comments yet</div>
                                <div class='no-comments-subtitle'>Be the first to comment on this work item</div>
                            </div>
                        </div>
                    </div>
                ");
            }

            contentBuilder.AppendLine("</div>");

            return GenerateHtmlDocument(contentBuilder.ToString());
        }

        private static string GenerateHtmlDocument(string content)
        {
            return $@"<!DOCTYPE html>
<html lang='ja'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <meta http-equiv='Content-Security-Policy' content=""default-src 'self' 'unsafe-inline'"">
    <title>Ticket Preview</title>
    <style>
        {GetCssStyles()}
    </style>
</head>
<body>
    <div class='container'>
        {content}
    </div>
</body>
</html>";
        }

        private static string GenerateErrorHtml(string errorMessage)
        {
            var safeMessage = WebUtility.HtmlEncode(errorMessage);
            return GenerateHtmlDocument($@"
                <div class='error-state'>
                    <div class='error-icon'>⚠️</div>
                    <div class='error-title'>プレビューエラー</div>
                    <p class='error-message'>{safeMessage}</p>
                    <div class='error-suggestion'>
                        <p>以下をお試しください:</p>
                        <ul>
                            <li>別のチケットを選択する</li>
                            <li>ワークアイテムを再取得する</li>
                            <li>アプリケーションを再起動する</li>
                        </ul>
                    </div>
                </div>
            ");
        }

        private static bool ValidateHtmlContent(string html)
        {
            if (string.IsNullOrEmpty(html))
                return false;

            if (html.Length > MAX_CONTENT_SIZE)
            {
                System.Diagnostics.Debug.WriteLine($"HTML content too large: {html.Length} bytes");
                return false;
            }

            if (!html.Contains("<!DOCTYPE html>"))
            {
                System.Diagnostics.Debug.WriteLine("Invalid HTML: Missing DOCTYPE");
                return false;
            }

            return true;
        }

        private static string GetCssStyles()
        {
            return @"
                :root {
                    --primary-color: #0366d6;
                    --secondary-color: #586069;
                    --success-color: #28a745;
                    --warning-color: #ffc107;
                    --danger-color: #dc3545;
                    --light-gray: #f6f8fa;
                    --border-color: #d0d7de;
                    --text-color: #24292f;
                    --bg-color: #ffffff;
                }

                * {
                    box-sizing: border-box;
                }

                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif;
                    line-height: 1.6;
                    color: var(--text-color);
                    background-color: var(--bg-color);
                    margin: 0;
                    padding: 0;
                    font-size: 14px;
                }

                .container {
                    max-width: 100%;
                    margin: 0;
                    padding: 16px;
                }

                /* Ticket Styles */
                .ticket-container {
                    background: var(--bg-color);
                    border-radius: 8px;
                    overflow: hidden;
                }

                .ticket-header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 16px 0;
                    border-bottom: 2px solid var(--primary-color);
                    margin-bottom: 16px;
                }

                .ticket-id {
                    font-size: 24px;
                    font-weight: bold;
                    color: var(--primary-color);
                }

                .ticket-badges {
                    display: flex;
                    gap: 8px;
                    flex-wrap: wrap;
                }

                .ticket-title {
                    font-size: 18px;
                    font-weight: 600;
                    color: var(--text-color);
                    margin-bottom: 20px;
                    line-height: 1.4;
                }

                .ticket-metadata {
                    margin-bottom: 24px;
                }

                .metadata-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
                    gap: 12px;
                }

                .metadata-item {
                    display: flex;
                    flex-direction: column;
                    gap: 4px;
                }

                .metadata-label {
                    font-size: 12px;
                    font-weight: 600;
                    color: var(--secondary-color);
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }

                .metadata-value {
                    font-size: 14px;
                    color: var(--text-color);
                }

                .ticket-section {
                    margin-bottom: 24px;
                    padding: 16px;
                    background: var(--light-gray);
                    border-radius: 6px;
                    border: 1px solid var(--border-color);
                }

                .section-title {
                    font-size: 16px;
                    font-weight: 600;
                    color: var(--text-color);
                    margin: 0 0 12px 0;
                    padding-bottom: 8px;
                    border-bottom: 1px solid var(--border-color);
                }

                .description-content, .criteria-content {
                    margin-top: 12px;
                }

                .description-text, .criteria-text {
                    background: var(--bg-color);
                    border: 1px solid var(--border-color);
                    border-radius: 4px;
                    padding: 12px;
                    margin: 0;
                    white-space: pre-wrap;
                    word-wrap: break-word;
                    font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', monospace;
                    font-size: 13px;
                    line-height: 1.5;
                    max-height: 300px;
                    overflow-y: auto;
                }

                /* Badge Styles */
                .badge {
                    display: inline-block;
                    padding: 4px 8px;
                    font-size: 11px;
                    font-weight: 500;
                    line-height: 1;
                    border-radius: 12px;
                    text-align: center;
                    white-space: nowrap;
                }

                .badge-type {
                    background-color: #6f42c1;
                    color: white;
                }

                .badge-state {
                    background-color: var(--success-color);
                    color: white;
                }

                .badge-priority {
                    background-color: var(--warning-color);
                    color: #212529;
                }

                /* Empty State */
                .empty-state {
                    text-align: center;
                    padding: 40px 20px;
                    color: var(--secondary-color);
                }

                .empty-icon {
                    font-size: 48px;
                    margin-bottom: 16px;
                }

                .empty-title {
                    font-size: 24px;
                    font-weight: 600;
                    color: var(--primary-color);
                    margin-bottom: 16px;
                }

                .instructions {
                    max-width: 400px;
                    margin: 24px auto 0;
                    text-align: left;
                    background: var(--light-gray);
                    padding: 20px;
                    border-radius: 8px;
                    border: 1px solid var(--border-color);
                }

                .instructions h4 {
                    margin: 0 0 12px 0;
                    color: var(--text-color);
                }

                .instructions ol {
                    margin: 0;
                    padding-left: 20px;
                }

                .instructions li {
                    margin-bottom: 8px;
                }

                /* Error State */
                .error-state {
                    text-align: center;
                    padding: 40px 20px;
                    color: var(--danger-color);
                }

                .error-icon {
                    font-size: 48px;
                    margin-bottom: 16px;
                }

                .error-title {
                    font-size: 20px;
                    font-weight: 600;
                    margin-bottom: 12px;
                }

                .error-message {
                    font-size: 14px;
                    margin-bottom: 20px;
                    padding: 12px;
                    background: #f8d7da;
                    border: 1px solid #f5c6cb;
                    border-radius: 4px;
                    color: #721c24;
                }

                .error-suggestion {
                    max-width: 400px;
                    margin: 0 auto;
                    text-align: left;
                    background: var(--light-gray);
                    padding: 16px;
                    border-radius: 6px;
                    border: 1px solid var(--border-color);
                }

                .error-suggestion p {
                    margin: 0 0 8px 0;
                    font-weight: 600;
                    color: var(--text-color);
                }

                .error-suggestion ul {
                    margin: 0;
                    padding-left: 20px;
                    color: var(--secondary-color);
                }

                .error-suggestion li {
                    margin-bottom: 4px;
                }

                /* Discussion Styles */
                .discussion-section {
                    margin-top: 24px;
                    border: 1px solid var(--border-color);
                    border-radius: 8px;
                    overflow: hidden;
                }

                .discussion-content {
                    background: var(--bg-color);
                    padding: 0;
                }

                .comment-item {
                    border-bottom: 1px solid var(--border-color);
                    padding: 16px;
                    transition: background-color 0.2s ease;
                }

                .comment-item:last-child {
                    border-bottom: none;
                }

                .comment-item:hover {
                    background-color: #f8f9fa;
                }

                .comment-header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    margin-bottom: 8px;
                }

                .comment-author {
                    font-weight: 600;
                    color: var(--primary-color);
                    font-size: 14px;
                }

                .comment-meta {
                    display: flex;
                    gap: 8px;
                    font-size: 12px;
                    color: var(--secondary-color);
                }

                .comment-date {
                    color: var(--secondary-color);
                }

                .comment-modified {
                    color: #6a737d;
                    font-style: italic;
                }

                .comment-body {
                    margin-top: 8px;
                }

                .comment-text {
                    background: transparent;
                    border: none;
                    padding: 0;
                    margin: 0;
                    white-space: pre-wrap;
                    word-wrap: break-word;
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
                    font-size: 14px;
                    line-height: 1.5;
                    color: var(--text-color);
                }

                .no-comments {
                    text-align: center;
                    padding: 40px 20px;
                    color: var(--secondary-color);
                }

                .no-comments-icon {
                    font-size: 32px;
                    margin-bottom: 12px;
                    opacity: 0.6;
                }

                .no-comments-text {
                    font-size: 16px;
                    font-weight: 500;
                    margin-bottom: 8px;
                    color: var(--text-color);
                }

                .no-comments-subtitle {
                    font-size: 14px;
                    color: var(--secondary-color);
                }

                /* List View Styles */
                .list-header {
                    margin-bottom: 24px;
                    padding-bottom: 16px;
                    border-bottom: 2px solid var(--primary-color);
                }

                .list-header h1 {
                    margin: 0 0 8px 0;
                    font-size: 28px;
                    color: var(--primary-color);
                }

                .item-count {
                    margin: 0;
                    color: var(--secondary-color);
                    font-size: 14px;
                }

                .work-items-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
                    gap: 16px;
                }

                .work-item-card {
                    background: var(--bg-color);
                    border: 1px solid var(--border-color);
                    border-radius: 8px;
                    padding: 16px;
                    transition: box-shadow 0.2s ease;
                }

                .work-item-card:hover {
                    box-shadow: 0 4px 8px rgba(0,0,0,0.1);
                }

                .work-item-header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    margin-bottom: 8px;
                }

                .work-item-id {
                    font-weight: bold;
                    color: var(--primary-color);
                    font-size: 14px;
                }

                .work-item-title {
                    font-weight: 600;
                    margin-bottom: 12px;
                    line-height: 1.4;
                    color: var(--text-color);
                }

                .work-item-meta {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    gap: 8px;
                    flex-wrap: wrap;
                }

                .work-item-assignee {
                    font-size: 12px;
                    color: var(--secondary-color);
                }

                /* Responsive Design */
                @media (max-width: 768px) {
                    .container {
                        padding: 12px;
                    }
                    
                    .ticket-header {
                        flex-direction: column;
                        align-items: flex-start;
                        gap: 12px;
                    }
                    
                    .metadata-grid {
                        grid-template-columns: 1fr;
                    }
                    
                    .work-items-grid {
                        grid-template-columns: 1fr;
                    }
                    
                    .work-item-meta {
                        flex-direction: column;
                        align-items: flex-start;
                    }
                    
                    .comment-header {
                        flex-direction: column;
                        align-items: flex-start;
                        gap: 4px;
                    }
                    
                    .comment-item {
                        padding: 12px;
                    }
                }
                
                @media (max-width: 480px) {
                    .ticket-id {
                        font-size: 20px;
                    }
                    
                    .ticket-title {
                        font-size: 16px;
                    }
                    
                    .instructions {
                        padding: 16px;
                    }
                    
                    .comment-text {
                        font-size: 13px;
                    }
                    
                    .no-comments {
                        padding: 24px 16px;
                    }
                }
            ";
        }
    }
}