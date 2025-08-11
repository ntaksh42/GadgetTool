using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using GadgetTools.Shared.Models;

namespace GadgetTools.Services
{
    public class AzureDevOpsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private static readonly HttpClient _sharedClient = new HttpClient();
        private readonly bool _isSharedClient;
        
        // キャッシュ用
        private static readonly Dictionary<string, (List<WorkItem> items, DateTime cachedAt)> _workItemCache 
            = new Dictionary<string, (List<WorkItem> items, DateTime cachedAt)>();
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
        private readonly AzureDevOpsConfig _config;

        public AzureDevOpsService(AzureDevOpsConfig config) : this(config, false)
        {
        }
        
        public AzureDevOpsService(AzureDevOpsConfig config, bool useSharedClient = false)
        {
            _config = config;
            _isSharedClient = useSharedClient;
            
            if (useSharedClient)
            {
                _httpClient = _sharedClient;
            }
            else
            {
                _httpClient = new HttpClient();
            }
            
            // Basic認証のヘッダーを設定
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{config.PersonalAccessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            // タイムアウト設定
            if (!useSharedClient)
            {
                _httpClient.Timeout = TimeSpan.FromMinutes(2);
            }
        }

        public async Task<List<WorkItem>> GetWorkItemsAsync(WorkItemQueryRequest request)
        {
            try
            {
                // キャッシュチェック
                var cacheKey = GenerateCacheKey(request);
                if (_workItemCache.TryGetValue(cacheKey, out var cached) && 
                    DateTime.Now - cached.cachedAt < _cacheExpiry)
                {
                    System.Diagnostics.Debug.WriteLine($"Returning cached work items for key: {cacheKey}");
                    return new List<WorkItem>(cached.items);
                }
                
                var allWorkItems = new List<WorkItem>();
                var projects = GetProjectsToQuery(request);
                
                foreach (var project in projects)
                {
                    var projectRequest = new WorkItemQueryRequest
                    {
                        Organization = request.Organization,
                        Project = project,
                        WorkItemType = request.WorkItemType,
                        State = request.State,
                        AssignedTo = request.AssignedTo,
                        MaxResults = request.MaxResults,
                        AreaPaths = request.AreaPaths,
                        IterationPaths = request.IterationPaths,
                        // 後方互換性のため
                        AreaPath = request.AreaPath,
                        IterationPath = request.IterationPath
                    };
                    
                    var projectWorkItems = await GetWorkItemsForProjectAsync(projectRequest);
                    allWorkItems.AddRange(projectWorkItems);
                    
                    // MaxResultsに達した場合は停止
                    if (allWorkItems.Count >= request.MaxResults)
                    {
                        break;
                    }
                }
                
                // MaxResultsでカット
                var result = allWorkItems.Take(request.MaxResults).ToList();
                
                // キャッシュに保存
                _workItemCache[cacheKey] = (result, DateTime.Now);
                
                // 古いキャッシュエントリをクリーンアップ
                CleanupExpiredCache();
                
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ワークアイテムの取得に失敗しました: {ex.Message}", ex);
            }
        }
        
        private List<string> GetProjectsToQuery(WorkItemQueryRequest request)
        {
            // 複数プロジェクトが指定されている場合はそれを使用
            if (request.Projects?.Any() == true)
            {
                return request.Projects;
            }
            
            // 単一プロジェクトが指定されている場合（後方互換性）
            if (!string.IsNullOrWhiteSpace(request.Project))
            {
                return new List<string> { request.Project };
            }
            
            throw new ArgumentException("プロジェクトが指定されていません。");
        }
        
        private async Task<List<WorkItem>> GetWorkItemsForProjectAsync(WorkItemQueryRequest request)
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
            
            // エリアパス条件（複数対応）
            var areaConditions = new List<string>();
            if (request.AreaPaths?.Any() == true)
            {
                foreach (var areaPath in request.AreaPaths.Where(a => !string.IsNullOrWhiteSpace(a)))
                {
                    areaConditions.Add($"[System.AreaPath] UNDER '{areaPath.Trim()}'");
                }
            }
            else if (!string.IsNullOrEmpty(request.AreaPath)) // 後方互換性
            {
                areaConditions.Add($"[System.AreaPath] UNDER '{request.AreaPath}'");
            }
            
            if (areaConditions.Any())
            {
                conditions.Add($"({string.Join(" OR ", areaConditions)})");
            }
            
            // イテレーションパス条件（複数対応）
            var iterationConditions = new List<string>();
            if (request.IterationPaths?.Any() == true)
            {
                foreach (var iterationPath in request.IterationPaths.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    iterationConditions.Add($"[System.IterationPath] UNDER '{iterationPath.Trim()}'");
                }
            }
            else if (!string.IsNullOrEmpty(request.IterationPath)) // 後方互換性
            {
                iterationConditions.Add($"[System.IterationPath] UNDER '{request.IterationPath}'");
            }
            
            if (iterationConditions.Any())
            {
                conditions.Add($"({string.Join(" OR ", iterationConditions)})");
            }
            
            var whereClause = string.Join(" AND ", conditions);
            
            return $@"
                SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType], 
                       [System.TeamProject], [System.AreaPath], [System.IterationPath],
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
                var allComments = new List<WorkItemComment>();
                
                // Try to get discussions (newer API for modern Azure DevOps)
                var discussionComments = await GetDiscussionCommentsAsync(workItemId, project);
                allComments.AddRange(discussionComments);
                
                // Try to get traditional comments if discussions didn't work
                if (allComments.Count == 0)
                {
                    var traditionalComments = await GetTraditionalCommentsAsync(workItemId, project);
                    allComments.AddRange(traditionalComments);
                }
                
                System.Diagnostics.Debug.WriteLine($"Total comments retrieved: {allComments.Count}");
                return allComments;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get work item comments for Work Item {workItemId}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<WorkItemComment>();
            }
        }

        private async Task<List<WorkItemComment>> GetDiscussionCommentsAsync(int workItemId, string project)
        {
            try
            {
                // Try the discussions API endpoints - Azure DevOps uses different API versions and endpoints
                var discussionEndpoints = new[]
                {
                    // Latest discussions API
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.2-preview.1",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.1-preview.1",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.0-preview.1",
                    
                    // Alternative endpoints without project context
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.2-preview.1",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.1-preview.1",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/discussions?api-version=7.0-preview.1",
                    
                    // Try different discussion-related endpoints
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/discussion?api-version=7.1-preview.1",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/discussion?api-version=7.1-preview.1"
                };

                foreach (var endpoint in discussionEndpoints)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying Discussion API URL: {endpoint}");
                        
                        var response = await _httpClient.GetAsync(endpoint);
                        System.Diagnostics.Debug.WriteLine($"Discussion API Response Status: {response.StatusCode}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Discussion API Raw Response Length: {result.Length}");
                            System.Diagnostics.Debug.WriteLine($"Discussion API Raw Response: {result.Substring(0, Math.Min(1000, result.Length))}...");
                            
                            var comments = ParseDiscussionResponse(result);
                            if (comments.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully got {comments.Count} discussion comments from {endpoint}");
                                return comments;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Discussion API returned successful response but no comments parsed from {endpoint}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Discussion API failed with status: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Discussion endpoint {endpoint} failed: {ex.Message}");
                        continue;
                    }
                }
                
                return new List<WorkItemComment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get discussion comments: {ex.Message}");
                return new List<WorkItemComment>();
            }
        }

        private async Task<List<WorkItemComment>> GetTraditionalCommentsAsync(int workItemId, string project)
        {
            try
            {
                // Try traditional comment endpoints with various API versions and formats
                var endpoints = new[]
                {
                    // Comments API endpoints
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/comments?api-version=7.2",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/comments?api-version=7.1",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/comments?api-version=7.0",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/comments?api-version=7.2",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/comments?api-version=7.1",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/comments?api-version=7.0",
                    
                    // Updates API - often contains comment history
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/updates?api-version=7.2",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/updates?api-version=7.1",
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/updates?api-version=7.0",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/updates?api-version=7.2",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/updates?api-version=7.1",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/updates?api-version=7.0",
                    
                    // Revisions API - sometimes contains comments
                    $"{_config.BaseUrl}/{project}/_apis/wit/workItems/{workItemId}/revisions?api-version=7.0",
                    $"{_config.BaseUrl}/_apis/wit/workItems/{workItemId}/revisions?api-version=7.0"
                };

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Trying Traditional Comments API URL: {endpoint}");
                        
                        var response = await _httpClient.GetAsync(endpoint);
                        System.Diagnostics.Debug.WriteLine($"Traditional Comments API Response Status: {response.StatusCode}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Traditional Comments API Raw Response Length: {result.Length}");
                            System.Diagnostics.Debug.WriteLine($"Traditional Comments API Raw Response: {result.Substring(0, Math.Min(1000, result.Length))}...");
                            
                            var comments = ParseCommentsFromResponse(result, endpoint.Contains("updates"), endpoint.Contains("revisions"));
                            if (comments.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully got {comments.Count} traditional comments from {endpoint}");
                                return comments;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Traditional API returned successful response but no comments parsed from {endpoint}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Traditional API failed with status: {response.StatusCode}, trying next endpoint");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Traditional endpoint {endpoint} failed: {ex.Message}");
                        continue;
                    }
                }
                
                return new List<WorkItemComment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get traditional comments: {ex.Message}");
                return new List<WorkItemComment>();
            }
        }

        private List<WorkItemComment> ParseDiscussionResponse(string result)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Parsing discussion response");
                dynamic dynamicResponse = JsonConvert.DeserializeObject(result)!;
                var comments = new List<WorkItemComment>();
                
                if (dynamicResponse?.value != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {((System.Collections.IEnumerable)dynamicResponse.value).Cast<object>().Count()} discussion entries");
                    
                    foreach (var discussion in dynamicResponse.value)
                    {
                        try
                        {
                            // Parse discussion thread
                            if (discussion.comments != null)
                            {
                                foreach (var comment in discussion.comments)
                                {
                                    try
                                    {
                                        var discussionComment = new WorkItemComment
                                        {
                                            Id = comment.id ?? 0,
                                            Text = comment.content?.ToString() ?? "",
                                            CreatedDate = comment.createdDate ?? DateTime.Now,
                                            ModifiedDate = comment.lastUpdatedDate ?? comment.createdDate ?? DateTime.Now,
                                            CreatedBy = new AssignedPerson
                                            {
                                                DisplayName = comment.createdBy?.displayName?.ToString() ?? "Unknown",
                                                UniqueName = comment.createdBy?.uniqueName?.ToString() ?? "",
                                                ImageUrl = comment.createdBy?.imageUrl?.ToString() ?? ""
                                            }
                                        };
                                        
                                        if (!string.IsNullOrEmpty(discussionComment.Text))
                                        {
                                            comments.Add(discussionComment);
                                            System.Diagnostics.Debug.WriteLine($"Added discussion comment: {discussionComment.Text.Substring(0, Math.Min(50, discussionComment.Text.Length))}...");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse individual discussion comment: {ex.Message}");
                                    }
                                }
                            }
                            else if (discussion.content != null)
                            {
                                // Single discussion entry without nested comments
                                var discussionComment = new WorkItemComment
                                {
                                    Id = discussion.id ?? 0,
                                    Text = discussion.content.ToString(),
                                    CreatedDate = discussion.createdDate ?? DateTime.Now,
                                    ModifiedDate = discussion.lastUpdatedDate ?? discussion.createdDate ?? DateTime.Now,
                                    CreatedBy = new AssignedPerson
                                    {
                                        DisplayName = discussion.createdBy?.displayName?.ToString() ?? "Unknown",
                                        UniqueName = discussion.createdBy?.uniqueName?.ToString() ?? "",
                                        ImageUrl = discussion.createdBy?.imageUrl?.ToString() ?? ""
                                    }
                                };
                                
                                if (!string.IsNullOrEmpty(discussionComment.Text))
                                {
                                    comments.Add(discussionComment);
                                    System.Diagnostics.Debug.WriteLine($"Added single discussion: {discussionComment.Text.Substring(0, Math.Min(50, discussionComment.Text.Length))}...");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to parse discussion entry: {ex.Message}");
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Successfully parsed {comments.Count} discussion comments");
                return comments;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse discussion response: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<WorkItemComment>();
            }
        }

        private List<WorkItemComment> ParseCommentsFromResponse(string result, bool isUpdatesEndpoint, bool isRevisionsEndpoint = false)
        {
            try
            {
                // Try parsing as dynamic first to understand the structure
                dynamic dynamicResponse = JsonConvert.DeserializeObject(result)!;
                System.Diagnostics.Debug.WriteLine($"Dynamic response type: {dynamicResponse?.GetType()}");
                
                var comments = new List<WorkItemComment>();
                
                if (dynamicResponse != null)
                {
                    if (isUpdatesEndpoint || isRevisionsEndpoint)
                    {
                        // For updates/revisions endpoint, look for comments in the updates/revisions
                        if (dynamicResponse.value != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing {(isUpdatesEndpoint ? "updates" : "revisions")} endpoint response");
                            foreach (var item in dynamicResponse.value)
                            {
                                try
                                {
                                    // Look for comment-like text in various fields
                                    string historyText = null;
                                    DateTime? itemDate = null;
                                    string authorName = "Unknown";
                                    
                                    if (item.fields != null && item.fields["System.History"] != null)
                                    {
                                        historyText = item.fields["System.History"]?.newValue?.ToString();
                                        itemDate = item.revisedDate ?? item.createdDate;
                                        authorName = item.revisedBy?.displayName?.ToString() ?? item.createdBy?.displayName?.ToString() ?? "Unknown";
                                    }
                                    else if (item.fields != null)
                                    {
                                        // Try to find any text field that might contain comments
                                        foreach (var field in item.fields)
                                        {
                                            if (field.Value?.newValue != null)
                                            {
                                                var fieldValue = field.Value.newValue.ToString();
                                                if (!string.IsNullOrEmpty(fieldValue) && fieldValue.Length > 10)
                                                {
                                                    historyText = fieldValue;
                                                    itemDate = item.revisedDate ?? item.createdDate;
                                                    authorName = item.revisedBy?.displayName?.ToString() ?? item.createdBy?.displayName?.ToString() ?? "Unknown";
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    
                                    // For revisions, also check direct field values
                                    if (isRevisionsEndpoint && string.IsNullOrEmpty(historyText) && item.fields != null)
                                    {
                                        if (item.fields["System.Description"] != null)
                                        {
                                            historyText = item.fields["System.Description"]?.ToString();
                                            itemDate = item.fields["System.ChangedDate"] ?? DateTime.Now;
                                            authorName = item.fields["System.ChangedBy"]?.displayName?.ToString() ?? "Unknown";
                                        }
                                    }
                                    
                                    if (!string.IsNullOrEmpty(historyText))
                                    {
                                        var comment = new WorkItemComment
                                        {
                                            Id = item.id ?? item.rev ?? 0,
                                            Text = historyText,
                                            CreatedDate = itemDate ?? DateTime.Now,
                                            ModifiedDate = itemDate ?? DateTime.Now,
                                            CreatedBy = new AssignedPerson
                                            {
                                                DisplayName = authorName
                                            }
                                        };
                                        comments.Add(comment);
                                        System.Diagnostics.Debug.WriteLine($"Added {(isUpdatesEndpoint ? "update" : "revision")} comment: {historyText.Substring(0, Math.Min(50, historyText.Length))}...");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to parse {(isUpdatesEndpoint ? "update" : "revision")} as comment: {ex.Message}");
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

        private string GenerateCacheKey(WorkItemQueryRequest request)
        {
            var keyBuilder = new StringBuilder();
            keyBuilder.Append($"{request.Organization}_{request.Project}_{request.WorkItemType}_{request.State}");
            keyBuilder.Append($"_{request.MaxResults}_{string.Join(",", request.Projects ?? new List<string>())}");
            keyBuilder.Append($"_{string.Join(",", request.AreaPaths ?? new List<string>())}");
            keyBuilder.Append($"_{string.Join(",", request.IterationPaths ?? new List<string>())}");
            return keyBuilder.ToString();
        }
        
        private static void CleanupExpiredCache()
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in _workItemCache)
            {
                if (DateTime.Now - kvp.Value.cachedAt > _cacheExpiry)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _workItemCache.Remove(key);
            }
        }
        
        public static void ClearCache()
        {
            _workItemCache.Clear();
        }

        public void Dispose()
        {
            if (!_isSharedClient)
            {
                _httpClient?.Dispose();
            }
        }
    }
}