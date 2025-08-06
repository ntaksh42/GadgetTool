using System;
using System.Collections.Generic;
using GadgetTools.Shared.Models;

namespace GadgetTools.TestData
{
    /// <summary>
    /// テスト用のサンプルワークアイテムを生成するクラス
    /// </summary>
    public static class SampleDataGenerator
    {
        private static readonly string[] Features = {
            "UserManagement", "AuthSystem", "ReportFeature", "DataSync", "UIImprovement",
            "Performance", "Security", "Backup", "NotificationSystem", "SearchFeature",
            "FileManagement", "Settings", "AuditLog", "Export", "Import"
        };

        private static readonly string[] Areas = {
            @"PersonalProject\Frontend\UI",
            @"PersonalProject\Frontend\Components",
            @"PersonalProject\Backend\API",
            @"PersonalProject\Backend\Database",
            @"PersonalProject\Backend\Services",
            @"PersonalProject\Infrastructure\Security",
            @"PersonalProject\Infrastructure\Monitoring",
            @"PersonalProject\Testing\Automation",
            @"PersonalProject\Testing\Manual",
            @"PersonalProject\Documentation"
        };

        private static readonly string[] TitleTemplates = {
            "[{0}] Login error occurs",
            "[{0}] Data not displayed correctly",
            "[{0}] Save button not responsive",
            "[{0}] UI layout broken",
            "[{0}] Performance degradation",
            "{0}: Memory leak detected",
            "{0}: Exception handling improper",
            "{0}: Validation error",
            "{0}: Timeout occurs",
            "{0} - UI not working correctly",
            "{0} - Data integrity error",
            "{0} - Permission check fails",
            "{0}_API call error",
            "{0}_Database connection failed",
            "{0}_File read error"
        };

        private static readonly string[] States = { "Active", "Active", "Active", "Resolved", "Closed" };
        private static readonly int[] Priorities = { 1, 1, 2, 2, 2, 3, 3, 3, 4 };

        /// <summary>
        /// 45件のダミーワークアイテムを生成
        /// </summary>
        public static List<WorkItem> GenerateSampleWorkItems()
        {
            var workItems = new List<WorkItem>();
            var random = new Random();

            for (int i = 1; i <= 45; i++)
            {
                var feature = Features[random.Next(Features.Length)];
                var area = Areas[random.Next(Areas.Length)];
                var titleTemplate = TitleTemplates[random.Next(TitleTemplates.Length)];
                var state = States[random.Next(States.Length)];
                var priority = Priorities[random.Next(Priorities.Length)];

                var title = string.Format(titleTemplate, feature);

                var workItem = new WorkItem
                {
                    Id = i,
                    Rev = 1,
                    Url = $"https://dev.azure.com/aksh0402/PersonalProject/_workitems/edit/{i}",
                    Fields = new WorkItemFields
                    {
                        SystemId = i,
                        Title = title,
                        Description = GenerateDescription(feature, priority),
                        WorkItemType = "Bug",
                        State = state,
                        AreaPath = area,
                        Priority = priority,
                        CreatedDate = DateTime.Now.AddDays(-random.Next(30)),
                        ChangedDate = DateTime.Now.AddDays(-random.Next(7)),
                        AssignedTo = GenerateAssignedPerson(),
                        CreatedBy = GenerateAssignedPerson(),
                        TeamProject = "PersonalProject",
                        Severity = GetSeverity(priority),
                        Tags = GenerateTags(feature)
                    }
                };

                workItems.Add(workItem);
            }

            return workItems;
        }

        private static string GenerateDescription(string feature, int priority)
        {
            var descriptions = new[]
            {
                $"Issue found in {feature} functionality. Reproduction steps: 1. Navigate to {feature} 2. Perform action 3. Error occurs. Priority level: {GetPriorityText(priority)}",
                $"Bug in {feature} component affects user experience. Impact: System response delayed, incorrect data display. Needs immediate attention.",
                $"Critical issue in {feature} module. Error occurs under specific conditions: high load, concurrent users, specific data state.",
                $"Performance degradation detected in {feature}. System becomes unresponsive during peak usage hours. Memory usage increases significantly."
            };

            var random = new Random();
            return descriptions[random.Next(descriptions.Length)];
        }

        private static string GetSeverity(int priority)
        {
            return priority switch
            {
                1 => "1 - Critical",
                2 => "2 - High", 
                3 => "3 - Medium",
                4 => "4 - Low",
                _ => "3 - Medium"
            };
        }

        private static string GetPriorityText(int priority)
        {
            return priority switch
            {
                1 => "Critical",
                2 => "High",
                3 => "Medium", 
                4 => "Low",
                _ => "Medium"
            };
        }

        private static AssignedPerson GenerateAssignedPerson()
        {
            var people = new[]
            {
                new AssignedPerson { DisplayName = "Alice Johnson" },
                new AssignedPerson { DisplayName = "Bob Smith" },
                new AssignedPerson { DisplayName = "Carol Davis" },
                new AssignedPerson { DisplayName = "David Wilson" },
                new AssignedPerson { DisplayName = "Eve Brown" },
                null // 未割当
            };

            var random = new Random();
            return people[random.Next(people.Length)];
        }

        private static string GenerateTags(string feature)
        {
            var allTags = new[] { "bug", "urgent", "ui", "backend", "security", "performance", feature.ToLower() };
            var random = new Random();
            var tagCount = random.Next(1, 4);
            var selectedTags = new List<string>();

            for (int i = 0; i < tagCount; i++)
            {
                var tag = allTags[random.Next(allTags.Length)];
                if (!selectedTags.Contains(tag))
                {
                    selectedTags.Add(tag);
                }
            }

            return string.Join("; ", selectedTags);
        }
    }
}