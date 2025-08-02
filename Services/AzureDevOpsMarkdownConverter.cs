using System.Text;
using System.Text.RegularExpressions;
using GadgetTools.Shared.Models;

namespace GadgetTools.Services
{
    public class AzureDevOpsMarkdownConverter
    {
        public string ConvertWorkItemsToMarkdown(List<WorkItem> workItems, string title = "Azure DevOps Work Items")
        {
            var sb = new StringBuilder();
            
            // ヘッダー
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total items: {workItems.Count}");
            sb.AppendLine();
            
            // サマリーテーブル
            sb.AppendLine("## Summary");
            sb.AppendLine();
            AppendSummaryTable(sb, workItems);
            sb.AppendLine();
            
            // 各ワークアイテムの詳細
            sb.AppendLine("## Work Items Details");
            sb.AppendLine();
            
            foreach (var workItem in workItems.OrderByDescending(w => w.Fields.ChangedDate))
            {
                AppendWorkItemDetail(sb, workItem);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        private void AppendSummaryTable(StringBuilder sb, List<WorkItem> workItems)
        {
            sb.AppendLine("| ID | Type | Title | State | Assigned To | Priority | Created | Updated |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            
            foreach (var item in workItems.OrderBy(w => w.Id))
            {
                var assignedTo = item.Fields.AssignedTo?.DisplayName ?? "Unassigned";
                var priority = item.Fields.Priority.ToString();
                var created = item.Fields.CreatedDate.ToString("yyyy-MM-dd");
                var updated = item.Fields.ChangedDate.ToString("yyyy-MM-dd");
                
                sb.AppendLine($"| [{item.Id}](#{item.Id.ToString().ToLower()}) | {EscapeMarkdown(item.Fields.WorkItemType)} | {EscapeMarkdown(item.Fields.Title)} | {EscapeMarkdown(item.Fields.State)} | {EscapeMarkdown(assignedTo)} | {priority} | {created} | {updated} |");
            }
        }

        private void AppendWorkItemDetail(StringBuilder sb, WorkItem workItem)
        {
            var fields = workItem.Fields;
            
            // タイトルとID
            sb.AppendLine($"## #{fields.SystemId}: {fields.Title}");
            sb.AppendLine();
            
            // 基本情報テーブル
            sb.AppendLine("### Basic Information");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| **ID** | {fields.SystemId} |");
            sb.AppendLine($"| **Type** | {EscapeMarkdown(fields.WorkItemType)} |");
            sb.AppendLine($"| **State** | {EscapeMarkdown(fields.State)} |");
            sb.AppendLine($"| **Priority** | {fields.Priority} |");
            
            if (!string.IsNullOrEmpty(fields.Severity))
            {
                sb.AppendLine($"| **Severity** | {EscapeMarkdown(fields.Severity)} |");
            }
            
            sb.AppendLine($"| **Created By** | {fields.CreatedBy?.DisplayName ?? "Unknown"} |");
            sb.AppendLine($"| **Assigned To** | {fields.AssignedTo?.DisplayName ?? "Unassigned"} |");
            sb.AppendLine($"| **Created Date** | {fields.CreatedDate:yyyy-MM-dd HH:mm:ss} |");
            sb.AppendLine($"| **Changed Date** | {fields.ChangedDate:yyyy-MM-dd HH:mm:ss} |");
            sb.AppendLine($"| **Area Path** | {EscapeMarkdown(fields.AreaPath)} |");
            sb.AppendLine($"| **Iteration Path** | {EscapeMarkdown(fields.IterationPath)} |");
            
            if (!string.IsNullOrEmpty(fields.Tags))
            {
                sb.AppendLine($"| **Tags** | {EscapeMarkdown(fields.Tags)} |");
            }
            
            sb.AppendLine();
            
            // 説明
            if (!string.IsNullOrEmpty(fields.Description))
            {
                sb.AppendLine("### Description");
                sb.AppendLine();
                sb.AppendLine(ConvertHtmlToMarkdown(fields.Description));
                sb.AppendLine();
            }
            
            // 再現手順（バグの場合）
            if (!string.IsNullOrEmpty(fields.ReproSteps))
            {
                sb.AppendLine("### Reproduction Steps");
                sb.AppendLine();
                sb.AppendLine(ConvertHtmlToMarkdown(fields.ReproSteps));
                sb.AppendLine();
            }
            
            // 受入条件
            if (!string.IsNullOrEmpty(fields.AcceptanceCriteria))
            {
                sb.AppendLine("### Acceptance Criteria");
                sb.AppendLine();
                sb.AppendLine(ConvertHtmlToMarkdown(fields.AcceptanceCriteria));
                sb.AppendLine();
            }
        }

        private string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            return text.Replace("|", "\\|")
                      .Replace("\n", " ")
                      .Replace("\r", "");
        }

        private string ConvertHtmlToMarkdown(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";
            
            // 簡単なHTML→Markdown変換
            var markdown = html;
            
            // HTMLタグを除去/変換
            markdown = Regex.Replace(markdown, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<p[^>]*>", "\n", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"</p>", "\n", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<strong[^>]*>(.*?)</strong>", "**$1**", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<b[^>]*>(.*?)</b>", "**$1**", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<em[^>]*>(.*?)</em>", "*$1*", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<i[^>]*>(.*?)</i>", "*$1*", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"</li>", "\n", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<ul[^>]*>|</ul>", "", RegexOptions.IgnoreCase);
            markdown = Regex.Replace(markdown, @"<ol[^>]*>|</ol>", "", RegexOptions.IgnoreCase);
            
            // 残りのHTMLタグを除去
            markdown = Regex.Replace(markdown, @"<[^>]+>", "", RegexOptions.IgnoreCase);
            
            // HTMLエンティティをデコード
            markdown = markdown.Replace("&lt;", "<")
                              .Replace("&gt;", ">")
                              .Replace("&amp;", "&")
                              .Replace("&quot;", "\"")
                              .Replace("&#39;", "'")
                              .Replace("&nbsp;", " ");
            
            // 連続する改行を整理
            markdown = Regex.Replace(markdown, @"\n\s*\n\s*\n+", "\n\n");
            
            return markdown.Trim();
        }

        public string ConvertToTable(List<WorkItem> workItems)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Azure DevOps Work Items");
            sb.AppendLine();
            sb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total items: {workItems.Count}");
            sb.AppendLine();
            
            AppendSummaryTable(sb, workItems);
            
            return sb.ToString();
        }
    }
}