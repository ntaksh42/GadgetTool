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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}