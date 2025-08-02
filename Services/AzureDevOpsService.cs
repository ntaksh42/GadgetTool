using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using GadgetTools.Models;

namespace GadgetTools.Services
{
    public class AzureDevOpsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AzureDevOpsConfig _config;

        public AzureDevOpsService(AzureDevOpsConfig config)
        {
            _config = config;
            _httpClient = new HttpClient();
            
            // Basic認証のヘッダーを設定
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{config.PersonalAccessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<WorkItem>> GetWorkItemsAsync(WorkItemQueryRequest request)
        {
            try
            {
                // WIQL (Work Item Query Language) クエリを構築
                var wiqlQuery = BuildWiqlQuery(request);
                
                // まずクエリを実行してワークアイテムIDを取得
                var queryUrl = $"{_config.BaseUrl}/{request.Project}/_apis/wit/wiql?api-version=7.0";
                var queryPayload = new { query = wiqlQuery };
                var queryJson = JsonConvert.SerializeObject(queryPayload);
                var queryContent = new StringContent(queryJson, Encoding.UTF8, "application/json");
                
                var queryResponse = await _httpClient.PostAsync(queryUrl, queryContent);
                queryResponse.EnsureSuccessStatusCode();
                
                var queryResult = await queryResponse.Content.ReadAsStringAsync();
                var queryData = JsonConvert.DeserializeObject<dynamic>(queryResult);
                
                if (queryData?.workItems == null || !((System.Collections.IEnumerable)queryData.workItems).Cast<object>().Any())
                {
                    return new List<WorkItem>();
                }

                // ワークアイテムIDを抽出
                var workItemIds = new List<int>();
                foreach (var item in ((System.Collections.IEnumerable)queryData.workItems).Cast<dynamic>())
                {
                    workItemIds.Add((int)item.id);
                }

                // ワークアイテムの詳細を取得
                var idsString = string.Join(",", workItemIds.Take(request.MaxResults));
                var detailsUrl = $"{_config.BaseUrl}/{request.Project}/_apis/wit/workitems?ids={idsString}&$expand=all&api-version=7.0";
                
                var detailsResponse = await _httpClient.GetAsync(detailsUrl);
                detailsResponse.EnsureSuccessStatusCode();
                
                var detailsResult = await detailsResponse.Content.ReadAsStringAsync();
                var workItemResponse = JsonConvert.DeserializeObject<WorkItemResponse>(detailsResult);
                
                return workItemResponse?.Value ?? new List<WorkItem>();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Azure DevOpsからワークアイテムの取得に失敗しました: {ex.Message}", ex);
            }
        }

        private string BuildWiqlQuery(WorkItemQueryRequest request)
        {
            var conditions = new List<string>();
            
            // プロジェクト条件
            conditions.Add($"[System.TeamProject] = '{request.Project}'");
            
            // ワークアイテムタイプ条件
            if (!string.IsNullOrEmpty(request.WorkItemType))
            {
                conditions.Add($"[System.WorkItemType] = '{request.WorkItemType}'");
            }
            
            // 状態条件
            if (!string.IsNullOrEmpty(request.State))
            {
                conditions.Add($"[System.State] = '{request.State}'");
            }
            
            // 担当者条件
            if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                conditions.Add($"[System.AssignedTo] = '{request.AssignedTo}'");
            }
            
            // エリアパス条件
            if (!string.IsNullOrEmpty(request.AreaPath))
            {
                conditions.Add($"[System.AreaPath] UNDER '{request.AreaPath}'");
            }
            
            // イテレーションパス条件
            if (!string.IsNullOrEmpty(request.IterationPath))
            {
                conditions.Add($"[System.IterationPath] UNDER '{request.IterationPath}'");
            }
            
            var whereClause = string.Join(" AND ", conditions);
            
            return $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], 
                       [System.AssignedTo], [System.CreatedDate], [System.ChangedDate]
                FROM WorkItems 
                WHERE {whereClause}
                ORDER BY [System.ChangedDate] DESC";
        }

        public async Task<IEnumerable<string>> GetIterationsAsync(string project)
        {
            try
            {
                var url = $"{_config.BaseUrl}/{project}/_apis/work/teamsettings/iterations?api-version=7.0";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResult = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(jsonResult);

                var iterations = new List<string>();
                if (data?.value != null)
                {
                    foreach (var iteration in data.value)
                    {
                        if (iteration?.name != null)
                        {
                            iterations.Add(iteration.name.ToString());
                        }
                    }
                }

                return iterations.OrderBy(x => x);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get iterations: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<IEnumerable<string>> GetAreasAsync(string project)
        {
            try
            {
                var url = $"{_config.BaseUrl}/{project}/_apis/wit/classificationnodes/Areas?$depth=10&api-version=7.0";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResult = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(jsonResult);

                var areas = new List<string>();
                if (data != null)
                {
                    ExtractAreaPaths(data, project, areas);
                }

                return areas.OrderBy(x => x);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get areas: {ex.Message}");
                return new List<string>();
            }
        }

        private void ExtractAreaPaths(dynamic node, string basePath, List<string> areas)
        {
            if (node?.name != null)
            {
                var currentPath = basePath;
                if (node.name.ToString() != basePath) // プロジェクト名と同じ場合はスキップ
                {
                    currentPath = $"{basePath}\\{node.name}";
                }
                
                areas.Add(currentPath);

                // 子ノードを再帰的に処理
                if (node.children != null)
                {
                    foreach (var child in node.children)
                    {
                        ExtractAreaPaths(child, currentPath, areas);
                    }
                }
            }
        }

        public async Task<List<WorkItemComment>> GetWorkItemCommentsAsync(int workItemId, string project)
        {
            try
            {
                // Try multiple possible endpoints for comments/discussions
                var endpoints = new[]
                {
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/comments?api-version=7.0",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/updates?api-version=7.0",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/comments?api-version=7.0"
                };

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying Comments API URL: {endpoint}");
                        
                        var response = await _httpClient.GetAsync(endpoint);
                        System.Diagnostics.Debug.WriteLine($"Comments API Response Status: {response.StatusCode}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Comments API Raw Response: {result.Substring(0, Math.Min(500, result.Length))}...");
                            
                            var comments = await ParseCommentsFromResponse(result, endpoint.Contains("updates"));
                            if (comments.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully got {comments.Count} comments from {endpoint}");
                                return comments;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed with status: {response.StatusCode}, trying next endpoint");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Endpoint {endpoint} failed: {ex.Message}");
                        continue;
                    }
                }
                
                return new List<WorkItemComment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get work item comments for Work Item {workItemId}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<WorkItemComment>();
            }
        }

        private async Task<List<WorkItemComment>> ParseCommentsFromResponse(string result, bool isUpdatesEndpoint)
        {
            try
            {
                // Try parsing as dynamic first to understand the structure
                dynamic dynamicResponse = JsonConvert.DeserializeObject(result)!;
                System.Diagnostics.Debug.WriteLine($"Dynamic response type: {dynamicResponse?.GetType()}");
                
                var comments = new List<WorkItemComment>();
                
                if (dynamicResponse != null)
                {
                    if (isUpdatesEndpoint)
                    {
                        // For updates endpoint, look for comments in the updates
                        if (dynamicResponse.value != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Processing updates endpoint response");
                            foreach (var update in dynamicResponse.value)
                            {
                                try
                                {
                                    // Look for comment-like text in updates
                                    if (update.fields != null && update.fields["System.History"] != null)
                                    {
                                        var historyText = update.fields["System.History"]?.newValue?.ToString();
                                        if (!string.IsNullOrEmpty(historyText))
                                        {
                                            var comment = new WorkItemComment
                                            {
                                                Id = update.id ?? 0,
                                                Text = historyText,
                                                CreatedDate = update.revisedDate ?? DateTime.Now,
                                                ModifiedDate = update.revisedDate ?? DateTime.Now,
                                                CreatedBy = new AssignedPerson
                                                {
                                                    DisplayName = update.revisedBy?.displayName?.ToString() ?? "Unknown"
                                                }
                                            };
                                            comments.Add(comment);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to parse update as comment: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // For comments endpoint, try different structures
                        if (dynamicResponse is Newtonsoft.Json.Linq.JArray directArray)
                        {
                            System.Diagnostics.Debug.WriteLine("Response is direct array");
                            foreach (var item in directArray)
                            {
                                try
                                {
                                    var comment = item.ToObject<WorkItemComment>();
                                    if (comment != null && !string.IsNullOrEmpty(comment.Text))
                                    {
                                        comments.Add(comment);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to parse comment from array: {ex.Message}");
                                }
                            }
                        }
                        else if (dynamicResponse.comments != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Response has 'comments' property");
                            foreach (var item in dynamicResponse.comments)
                            {
                                try
                                {
                                    var comment = JsonConvert.DeserializeObject<WorkItemComment>(item.ToString());
                                    if (comment != null && !string.IsNullOrEmpty(comment.Text))
                                    {
                                        comments.Add(comment);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to parse comment from comments property: {ex.Message}");
                                }
                            }
                        }
                        else if (dynamicResponse.value != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Response has 'value' property");
                            foreach (var item in dynamicResponse.value)
                            {
                                try
                                {
                                    var comment = JsonConvert.DeserializeObject<WorkItemComment>(item.ToString());
                                    if (comment != null && !string.IsNullOrEmpty(comment.Text))
                                    {
                                        comments.Add(comment);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to parse comment from value property: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Successfully parsed {comments.Count} comments");
                
                if (comments.Count > 0)
                {
                    foreach (var comment in comments.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"Comment ID: {comment.Id}, Text: {comment.Text?.Substring(0, Math.Min(50, comment.Text?.Length ?? 0))}..., CreatedBy: {comment.CreatedBy?.DisplayName}");
                    }
                }
                
                return comments;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse comments from response: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // コメント取得に失敗しても全体の処理は続行
                return new List<WorkItemComment>();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}