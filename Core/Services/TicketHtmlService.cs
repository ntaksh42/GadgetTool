using System;
using System.Net;
using System.Text;
using GadgetTools.Shared.Models;

namespace GadgetTools.Core.Services
{
    public static class TicketHtmlService
    {
        private const int MAX_CONTENT_SIZE = 1024 * 1024; // 1MB limit

        public static string GenerateWorkItemHtml(WorkItem workItem, List<WorkItemComment>? comments = null, string? organization = null)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            try
            {
                var html = GenerateWorkItemHtmlInternal(workItem, comments, organization);
                
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

        private static string GenerateWorkItemHtmlInternal(WorkItem workItem, List<WorkItemComment>? comments = null, string? organization = null)
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
            
            // Header section with improved visual hierarchy
            contentBuilder.AppendLine($@"
                <div class='ticket-container'>
                    <!-- Main Header -->
                    <div class='ticket-header'>
                        <div class='header-left'>
                            <div class='ticket-id'>#{workItem.Id}</div>
                            <div class='ticket-type-indicator'>{WebUtility.HtmlEncode(workItem.Fields.WorkItemType ?? "Unknown")}</div>
                        </div>
                        <div class='header-right'>
                            <div class='ticket-badges'>
                                <span class='badge badge-state status-{GetStateClass(workItem.Fields.State)}'>{WebUtility.HtmlEncode(workItem.Fields.State ?? "Unknown")}</span>
                                {(workItem.Fields.Priority == 0 ? "" : $"<span class='badge badge-priority priority-{workItem.Fields.Priority}'>P{workItem.Fields.Priority}</span>")}
                                {GenerateWorkItemLink(workItem, organization)}
                            </div>
                        </div>
                    </div>
                    
                    <!-- Title Section -->
                    <div class='ticket-title-section'>
                        <h1 class='ticket-title'>{WebUtility.HtmlEncode(workItem.Fields.Title ?? "No Title")}</h1>
                    </div>
                    
                    <!-- Quick Info Cards -->
                    <div class='quick-info'>
                        <div class='info-card'>
                            <div class='info-icon'>👤</div>
                            <div class='info-content'>
                                <div class='info-label'>Assigned To</div>
                                <div class='info-value assignee'>{WebUtility.HtmlEncode(workItem.Fields.AssignedTo?.DisplayName ?? "Unassigned")}</div>
                            </div>
                        </div>
                        <div class='info-card'>
                            <div class='info-icon'>📅</div>
                            <div class='info-content'>
                                <div class='info-label'>Created</div>
                                <div class='info-value date'>{workItem.Fields.CreatedDate:MMM dd, yyyy}</div>
                            </div>
                        </div>
                        <div class='info-card'>
                            <div class='info-icon'>🕒</div>
                            <div class='info-content'>
                                <div class='info-label'>Last Updated</div>
                                <div class='info-value date'>{workItem.Fields.ChangedDate:MMM dd, yyyy}</div>
                            </div>
                        </div>
                        {(!string.IsNullOrEmpty(workItem.Fields.Tags) ? $@"
                        <div class='info-card tags-card'>
                            <div class='info-icon'>🏷️</div>
                            <div class='info-content'>
                                <div class='info-label'>Tags</div>
                                <div class='info-value tags'>{GetFormattedTags(workItem.Fields.Tags)}</div>
                            </div>
                        </div>" : "")}
                    </div>
            ");

            // Description section with improved styling
            if (!string.IsNullOrEmpty(workItem.Fields.Description))
            {
                var safeDescription = WebUtility.HtmlEncode(workItem.Fields.Description);
                contentBuilder.AppendLine($@"
                    <div class='content-section description-section'>
                        <div class='section-header'>
                            <div class='section-icon'>📝</div>
                            <h2 class='section-title'>Description</h2>
                        </div>
                        <div class='section-content'>
                            <div class='description-text'>{FormatText(safeDescription)}</div>
                        </div>
                    </div>
                ");
            }

            // Acceptance Criteria section with improved styling
            if (!string.IsNullOrEmpty(workItem.Fields.AcceptanceCriteria))
            {
                var safeCriteria = WebUtility.HtmlEncode(workItem.Fields.AcceptanceCriteria);
                contentBuilder.AppendLine($@"
                    <div class='content-section criteria-section'>
                        <div class='section-header'>
                            <div class='section-icon'>✅</div>
                            <h2 class='section-title'>Acceptance Criteria</h2>
                        </div>
                        <div class='section-content'>
                            <div class='criteria-text'>{FormatText(safeCriteria)}</div>
                        </div>
                    </div>
                ");
            }

            // Discussion section with improved styling
            if (comments != null && comments.Count > 0)
            {
                contentBuilder.AppendLine($@"
                    <div class='content-section discussion-section'>
                        <div class='section-header'>
                            <div class='section-icon'>📖</div>
                            <h2 class='section-title'>Discussion</h2>
                            <div class='comment-count'>{comments.Count} comment{(comments.Count == 1 ? "" : "s")}</div>
                        </div>
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
                            <div class='comment-avatar'>
                                <div class='avatar-circle'>{GetInitials(authorName)}</div>
                            </div>
                            <div class='comment-content'>
                                <div class='comment-header'>
                                    <div class='comment-author'>{authorName}</div>
                                    <div class='comment-meta'>
                                        <span class='comment-date' title='{comment.CreatedDate:yyyy-MM-dd HH:mm:ss}'>{GetRelativeTime(comment.CreatedDate)}</span>
                                        {(isModified ? $"<span class='comment-modified' title='Modified: {comment.ModifiedDate:yyyy-MM-dd HH:mm:ss}'>edited</span>" : "")}
                                    </div>
                                </div>
                                <div class='comment-body'>
                                    <div class='comment-text'>{FormatText(safeCommentText)}</div>
                                </div>
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
                    <div class='content-section discussion-section'>
                        <div class='section-header'>
                            <div class='section-icon'>📖</div>
                            <h2 class='section-title'>Discussion</h2>
                            <div class='comment-count'>0 comments</div>
                        </div>
                        <div class='discussion-content'>
                            <div class='no-comments'>
                                <div class='no-comments-icon'>🗨️</div>
                                <div class='no-comments-text'>No comments yet</div>
                                <div class='no-comments-subtitle'>Start the conversation about this work item</div>
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

        private static string GetStateClass(string? state)
        {
            return state?.ToLower() switch
            {
                "new" => "new",
                "active" => "active",
                "committed" => "committed",
                "done" => "done",
                "completed" => "completed",
                "closed" => "closed",
                "resolved" => "resolved",
                "removed" => "removed",
                _ => "default"
            };
        }
        
        private static string GetFormattedTags(string tags)
        {
            if (string.IsNullOrEmpty(tags)) return "";
            
            var tagList = tags.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var formattedTags = tagList.Select(tag => $"<span class='tag'>{WebUtility.HtmlEncode(tag.Trim())}</span>");
            return string.Join(" ", formattedTags);
        }
        
        private static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            return parts[0].Length > 0 ? parts[0][0].ToString().ToUpper() : "?";
        }
        
        private static string GetRelativeTime(DateTime dateTime)
        {
            var now = DateTime.UtcNow;
            var timeSpan = now - dateTime;
            
            if (timeSpan.TotalMinutes < 1) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";
            if (timeSpan.TotalDays < 30) return $"{(int)(timeSpan.TotalDays / 7)}w ago";
            
            return dateTime.ToString("MMM dd, yyyy");
        }
        
        private static string GenerateWorkItemLink(WorkItem workItem, string? organization)
        {
            if (string.IsNullOrEmpty(organization) || string.IsNullOrEmpty(workItem.Fields.TeamProject))
            {
                return "";
            }
            
            var url = $"https://dev.azure.com/{organization}/{workItem.Fields.TeamProject}/_workitems/edit/{workItem.Id}";
            return $"<a href='{url}' target='_blank' class='open-link-badge' title='Open in Azure DevOps'>🔗 Open</a>";
        }
        
        private static string FormatText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Basic text formatting - convert line breaks to HTML
            var formatted = text.Replace("\r\n", "\n").Replace("\r", "\n");
            
            // Split into paragraphs
            var paragraphs = formatted.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            if (paragraphs.Length <= 1)
            {
                // Single paragraph or simple text
                return $"<p>{formatted.Replace("\n", "<br>")}</p>";
            }
            
            // Multiple paragraphs
            var result = new StringBuilder();
            foreach (var paragraph in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    result.AppendLine($"<p>{paragraph.Replace("\n", "<br>")}</p>");
                }
            }
            
            return result.ToString();
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
                    --primary-color: #2563eb;
                    --primary-light: #dbeafe;
                    --secondary-color: #64748b;
                    --success-color: #059669;
                    --warning-color: #d97706;
                    --danger-color: #dc2626;
                    --light-gray: #f8fafc;
                    --border-color: #e2e8f0;
                    --text-color: #1e293b;
                    --text-light: #64748b;
                    --bg-color: #ffffff;
                    --card-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
                    --card-shadow-hover: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
                }

                * {
                    box-sizing: border-box;
                }

                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Inter', 'Helvetica Neue', sans-serif;
                    line-height: 1.6;
                    color: var(--text-color);
                    background: linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%);
                    margin: 0;
                    padding: 0;
                    font-size: 14px;
                    -webkit-font-smoothing: antialiased;
                    -moz-osx-font-smoothing: grayscale;
                }

                .container {
                    max-width: 100%;
                    margin: 0;
                    padding: 20px;
                }

                /* Modern Ticket Container */
                .ticket-container {
                    background: var(--bg-color);
                    border-radius: 16px;
                    box-shadow: var(--card-shadow);
                    overflow: hidden;
                    border: 1px solid var(--border-color);
                }

                /* Improved Header */
                .ticket-header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 24px 28px 20px;
                    background: linear-gradient(135deg, var(--primary-color) 0%, #1d4ed8 100%);
                    color: white;
                    position: relative;
                }

                .ticket-header::after {
                    content: '';
                    position: absolute;
                    bottom: 0;
                    left: 0;
                    right: 0;
                    height: 4px;
                    background: linear-gradient(90deg, rgba(255,255,255,0.3) 0%, rgba(255,255,255,0.1) 100%);
                }

                .header-left {
                    display: flex;
                    align-items: center;
                    gap: 16px;
                }

                .header-right {
                    display: flex;
                    align-items: center;
                }

                .ticket-id {
                    font-size: 28px;
                    font-weight: 700;
                    color: white;
                    text-shadow: 0 1px 2px rgba(0,0,0,0.1);
                }

                .ticket-type-indicator {
                    background: rgba(255,255,255,0.2);
                    padding: 6px 12px;
                    border-radius: 20px;
                    font-size: 12px;
                    font-weight: 500;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    backdrop-filter: blur(10px);
                }

                .ticket-badges {
                    display: flex;
                    gap: 8px;
                    flex-wrap: wrap;
                }

                .ticket-title-section {
                    padding: 28px;
                    background: var(--bg-color);
                    border-bottom: 1px solid var(--border-color);
                }

                .ticket-title {
                    font-size: 24px;
                    font-weight: 600;
                    color: var(--text-color);
                    margin: 0;
                    line-height: 1.4;
                    letter-spacing: -0.025em;
                }

                /* Quick Info Cards */
                .quick-info {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                    gap: 16px;
                    padding: 24px 28px;
                    background: var(--light-gray);
                    border-bottom: 1px solid var(--border-color);
                }

                .info-card {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    padding: 16px;
                    background: var(--bg-color);
                    border-radius: 12px;
                    box-shadow: var(--card-shadow);
                    transition: all 0.2s ease;
                    border: 1px solid var(--border-color);
                }

                .info-card:hover {
                    box-shadow: var(--card-shadow-hover);
                    transform: translateY(-1px);
                }

                .info-icon {
                    font-size: 20px;
                    width: 32px;
                    height: 32px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background: var(--primary-light);
                    border-radius: 8px;
                    flex-shrink: 0;
                }

                .info-content {
                    flex: 1;
                    min-width: 0;
                }

                .info-label {
                    font-size: 11px;
                    font-weight: 600;
                    color: var(--text-light);
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    margin-bottom: 4px;
                }

                .info-value {
                    font-size: 14px;
                    color: var(--text-color);
                    font-weight: 500;
                    word-break: break-word;
                }

                .info-value.assignee {
                    color: var(--primary-color);
                    font-weight: 600;
                }

                .info-value.date {
                    color: var(--text-color);
                    font-variant-numeric: tabular-nums;
                }

                .tags-card .info-value {
                    line-height: 1.4;
                }

                .tag {
                    display: inline-block;
                    background: var(--primary-light);
                    color: var(--primary-color);
                    padding: 2px 8px;
                    border-radius: 12px;
                    font-size: 11px;
                    font-weight: 500;
                    margin: 2px 4px 2px 0;
                }

                /* Content Sections */
                .content-section {
                    margin: 0;
                    background: var(--bg-color);
                    border-bottom: 1px solid var(--border-color);
                }

                .section-header {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    padding: 24px 28px 16px;
                    background: var(--bg-color);
                    border-bottom: 1px solid var(--border-color);
                }

                .section-icon {
                    font-size: 18px;
                    width: 32px;
                    height: 32px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background: var(--light-gray);
                    border-radius: 8px;
                    flex-shrink: 0;
                }

                .section-title {
                    font-size: 18px;
                    font-weight: 600;
                    color: var(--text-color);
                    margin: 0;
                    flex: 1;
                }

                .comment-count {
                    background: var(--primary-light);
                    color: var(--primary-color);
                    padding: 4px 12px;
                    border-radius: 12px;
                    font-size: 12px;
                    font-weight: 500;
                }

                .section-content {
                    padding: 24px 28px;
                }

                .description-text, .criteria-text {
                    font-size: 15px;
                    line-height: 1.7;
                    color: var(--text-color);
                    margin: 0;
                }

                .description-text p, .criteria-text p {
                    margin: 0 0 16px 0;
                }

                .description-text p:last-child, .criteria-text p:last-child {
                    margin-bottom: 0;
                }

                /* Modern Badge Styles */
                .badge {
                    display: inline-block;
                    padding: 6px 12px;
                    font-size: 11px;
                    font-weight: 600;
                    line-height: 1;
                    border-radius: 16px;
                    text-align: center;
                    white-space: nowrap;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    backdrop-filter: blur(10px);
                }

                .badge-state {
                    color: white;
                    text-shadow: 0 1px 2px rgba(0,0,0,0.1);
                }

                .badge-state.status-new { background: linear-gradient(135deg, #64748b, #475569); }
                .badge-state.status-active { background: linear-gradient(135deg, #059669, #047857); }
                .badge-state.status-committed { background: linear-gradient(135deg, #2563eb, #1d4ed8); }
                .badge-state.status-done { background: linear-gradient(135deg, #059669, #047857); }
                .badge-state.status-completed { background: linear-gradient(135deg, #059669, #047857); }
                .badge-state.status-closed { background: linear-gradient(135deg, #6b7280, #4b5563); }
                .badge-state.status-resolved { background: linear-gradient(135deg, #059669, #047857); }
                .badge-state.status-removed { background: linear-gradient(135deg, #dc2626, #b91c1c); }
                .badge-state.status-default { background: linear-gradient(135deg, #64748b, #475569); }

                .badge-priority {
                    color: white;
                    text-shadow: 0 1px 2px rgba(0,0,0,0.1);
                }

                .badge-priority.priority-1 { background: linear-gradient(135deg, #dc2626, #b91c1c); }
                .badge-priority.priority-2 { background: linear-gradient(135deg, #ea580c, #c2410c); }
                .badge-priority.priority-3 { background: linear-gradient(135deg, #d97706, #b45309); }
                .badge-priority.priority-4 { background: linear-gradient(135deg, #65a30d, #4d7c0f); }
                
                .open-link-badge {
                    display: inline-block;
                    background: linear-gradient(135deg, #059669, #047857);
                    color: white;
                    padding: 6px 12px;
                    border-radius: 16px;
                    text-decoration: none;
                    font-size: 11px;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    transition: all 0.2s ease;
                    text-shadow: 0 1px 2px rgba(0,0,0,0.1);
                    margin-left: 8px;
                }
                
                .open-link-badge:hover {
                    background: linear-gradient(135deg, #047857, #065f46);
                    transform: translateY(-1px);
                    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
                    text-decoration: none;
                    color: white;
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

                /* Modern Discussion Styles */
                .discussion-content {
                    background: var(--bg-color);
                    padding: 0;
                }

                .comment-item {
                    display: flex;
                    gap: 16px;
                    padding: 20px 28px;
                    border-bottom: 1px solid var(--border-color);
                    transition: all 0.2s ease;
                }

                .comment-item:last-child {
                    border-bottom: none;
                }

                .comment-item:hover {
                    background: var(--light-gray);
                }

                .comment-avatar {
                    flex-shrink: 0;
                }

                .avatar-circle {
                    width: 40px;
                    height: 40px;
                    background: linear-gradient(135deg, var(--primary-color), #1d4ed8);
                    color: white;
                    border-radius: 50%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 14px;
                    font-weight: 600;
                    text-transform: uppercase;
                    box-shadow: var(--card-shadow);
                }

                .comment-content {
                    flex: 1;
                    min-width: 0;
                }

                .comment-header {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    margin-bottom: 8px;
                    flex-wrap: wrap;
                }

                .comment-author {
                    font-weight: 600;
                    color: var(--text-color);
                    font-size: 15px;
                }

                .comment-meta {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    font-size: 12px;
                    color: var(--text-light);
                }

                .comment-date {
                    color: var(--text-light);
                    font-variant-numeric: tabular-nums;
                }

                .comment-modified {
                    background: var(--warning-color);
                    color: white;
                    padding: 2px 6px;
                    border-radius: 10px;
                    font-size: 10px;
                    font-weight: 500;
                    text-transform: uppercase;
                }

                .comment-body {
                    margin-top: 4px;
                }

                .comment-text {
                    font-size: 14px;
                    line-height: 1.6;
                    color: var(--text-color);
                    margin: 0;
                }

                .comment-text p {
                    margin: 0 0 12px 0;
                }

                .comment-text p:last-child {
                    margin-bottom: 0;
                }

                .no-comments {
                    text-align: center;
                    padding: 48px 28px;
                    background: var(--light-gray);
                    border: 2px dashed var(--border-color);
                    border-radius: 12px;
                    margin: 20px;
                }

                .no-comments-icon {
                    font-size: 48px;
                    margin-bottom: 16px;
                    opacity: 0.7;
                    filter: grayscale(0.3);
                }

                .no-comments-text {
                    font-size: 18px;
                    font-weight: 600;
                    margin-bottom: 8px;
                    color: var(--text-color);
                }

                .no-comments-subtitle {
                    font-size: 14px;
                    color: var(--text-light);
                    line-height: 1.5;
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

                /* Enhanced Responsive Design */
                @media (max-width: 768px) {
                    .container {
                        padding: 16px;
                    }
                    
                    .ticket-header {
                        flex-direction: column;
                        align-items: flex-start;
                        gap: 16px;
                        padding: 20px 24px;
                    }
                    
                    .header-left {
                        gap: 12px;
                    }
                    
                    .ticket-id {
                        font-size: 24px;
                    }
                    
                    .ticket-title-section {
                        padding: 20px 24px;
                    }
                    
                    .ticket-title {
                        font-size: 20px;
                    }
                    
                    .quick-info {
                        grid-template-columns: 1fr;
                        padding: 20px 24px;
                        gap: 12px;
                    }
                    
                    .section-header {
                        padding: 20px 24px 12px;
                    }
                    
                    .section-content {
                        padding: 20px 24px;
                    }
                    
                    .comment-item {
                        padding: 16px 24px;
                        gap: 12px;
                    }
                    
                    .avatar-circle {
                        width: 36px;
                        height: 36px;
                        font-size: 13px;
                    }
                    
                    .no-comments {
                        margin: 16px;
                        padding: 32px 20px;
                    }
                }
                
                @media (max-width: 480px) {
                    .container {
                        padding: 12px;
                    }
                    
                    .ticket-header {
                        padding: 16px 20px;
                    }
                    
                    .ticket-id {
                        font-size: 20px;
                    }
                    
                    .ticket-title-section {
                        padding: 16px 20px;
                    }
                    
                    .ticket-title {
                        font-size: 18px;
                    }
                    
                    .quick-info {
                        padding: 16px 20px;
                    }
                    
                    .info-card {
                        padding: 12px;
                    }
                    
                    .section-header {
                        padding: 16px 20px 12px;
                    }
                    
                    .section-content {
                        padding: 16px 20px;
                    }
                    
                    .comment-item {
                        padding: 12px 20px;
                        gap: 10px;
                    }
                    
                    .avatar-circle {
                        width: 32px;
                        height: 32px;
                        font-size: 12px;
                    }
                    
                    .comment-text {
                        font-size: 13px;
                    }
                    
                    .no-comments {
                        margin: 12px;
                        padding: 24px 16px;
                    }
                    
                    .no-comments-icon {
                        font-size: 36px;
                    }
                    
                    .no-comments-text {
                        font-size: 16px;
                    }
                }
            ";
        }
    }
}