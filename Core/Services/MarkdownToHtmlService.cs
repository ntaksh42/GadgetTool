using Markdig;
using System.Text;

namespace GadgetTools.Core.Services
{
    public static class MarkdownToHtmlService
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string ConvertToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return GetEmptyHtml();

            var htmlContent = Markdown.ToHtml(markdown, _pipeline);
            return WrapInHtmlDocument(htmlContent);
        }

        private static string WrapInHtmlDocument(string htmlContent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"utf-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.AppendLine("    <title>Ticket Preview</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCssStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine(htmlContent);
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private static string GetCssStyles()
        {
            return @"
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', 'Fira Sans', 'Droid Sans', 'Helvetica Neue', sans-serif;
                    line-height: 1.6;
                    color: #333;
                    background-color: #fff;
                    margin: 0;
                    padding: 0;
                }

                .container {
                    max-width: 100%;
                    margin: 0;
                    padding: 16px;
                }

                h1, h2, h3, h4, h5, h6 {
                    margin-top: 24px;
                    margin-bottom: 16px;
                    font-weight: 600;
                    line-height: 1.25;
                }

                h1 {
                    font-size: 1.5em;
                    color: #0366d6;
                    border-bottom: 1px solid #eaecef;
                    padding-bottom: 8px;
                }

                h2 {
                    font-size: 1.25em;
                    color: #0366d6;
                    border-bottom: 1px solid #eaecef;
                    padding-bottom: 6px;
                }

                h3 {
                    font-size: 1.1em;
                    color: #586069;
                }

                p {
                    margin-bottom: 16px;
                }

                a {
                    color: #0366d6;
                    text-decoration: none;
                }

                a:hover {
                    text-decoration: underline;
                }

                table {
                    border-collapse: collapse;
                    width: 100%;
                    margin-bottom: 16px;
                    font-size: 0.9em;
                }

                th, td {
                    border: 1px solid #d0d7de;
                    padding: 8px 12px;
                    text-align: left;
                }

                th {
                    background-color: #f6f8fa;
                    font-weight: 600;
                }

                tr:nth-child(even) {
                    background-color: #f6f8fa;
                }

                code {
                    background-color: #f6f8fa;
                    border-radius: 3px;
                    font-size: 85%;
                    margin: 0;
                    padding: 2px 4px;
                    font-family: 'SFMono-Regular', 'Consolas', 'Liberation Mono', 'Menlo', monospace;
                }

                pre {
                    background-color: #f6f8fa;
                    border-radius: 6px;
                    font-size: 85%;
                    line-height: 1.45;
                    margin-bottom: 16px;
                    padding: 16px;
                    overflow: auto;
                }

                pre code {
                    background-color: transparent;
                    border: 0;
                    display: inline;
                    line-height: inherit;
                    margin: 0;
                    max-width: auto;
                    padding: 0;
                    white-space: pre;
                    word-wrap: normal;
                }

                blockquote {
                    border-left: 4px solid #dfe2e5;
                    margin: 0 0 16px 0;
                    padding: 0 16px;
                    color: #6a737d;
                }

                ul, ol {
                    margin-bottom: 16px;
                    padding-left: 2em;
                }

                li {
                    margin-bottom: 4px;
                }

                hr {
                    border: 0;
                    border-top: 1px solid #eaecef;
                    margin: 24px 0;
                }

                .ticket-header {
                    background-color: #f1f8ff;
                    border: 1px solid #c8e1ff;
                    border-radius: 6px;
                    padding: 16px;
                    margin-bottom: 16px;
                }

                .ticket-id {
                    font-size: 1.2em;
                    font-weight: bold;
                    color: #0366d6;
                }

                .ticket-title {
                    font-size: 1.1em;
                    margin: 8px 0;
                }

                .ticket-meta {
                    font-size: 0.9em;
                    color: #586069;
                    margin-top: 8px;
                }

                .badge {
                    display: inline-block;
                    padding: 2px 6px;
                    font-size: 0.75em;
                    font-weight: 500;
                    line-height: 1;
                    border-radius: 3px;
                    margin-right: 4px;
                }

                .badge-state {
                    background-color: #28a745;
                    color: white;
                }

                .badge-type {
                    background-color: #6f42c1;
                    color: white;
                }

                .badge-priority {
                    background-color: #fd7e14;
                    color: white;
                }

                @media (max-width: 768px) {
                    .container {
                        padding: 8px;
                    }
                    
                    table {
                        font-size: 0.8em;
                    }
                    
                    th, td {
                        padding: 6px 8px;
                    }
                }
            ";
        }

        private static string GetEmptyHtml()
        {
            var emptyContent = @"
                <div style='text-align: center; color: #6a737d; margin-top: 40px;'>
                    <h3>🎯 チケットプレビュー</h3>
                    <p>チケットを選択すると、ここに詳細が表示されます。</p>
                    <p style='font-size: 0.9em;'>Select a ticket to view its details here.</p>
                </div>
            ";
            return WrapInHtmlDocument(emptyContent);
        }
    }
}