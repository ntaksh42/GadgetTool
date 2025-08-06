using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// 保存済みクエリ管理サービス
    /// </summary>
    public class SavedQueryService
    {
        private static SavedQueryService? _instance;
        public static SavedQueryService Instance => _instance ??= new SavedQueryService();

        private readonly string _savedQueriesPath;
        private List<SavedQuery> _savedQueries;
        private List<SearchQuickFilter> _quickFilters;

        private SavedQueryService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GadgetTools");
            Directory.CreateDirectory(appDataPath);
            _savedQueriesPath = Path.Combine(appDataPath, "saved_queries.json");
            
            _savedQueries = LoadSavedQueries();
            _quickFilters = InitializeQuickFilters();
        }

        public event EventHandler<SavedQuery>? QuerySaved;
        public event EventHandler<string>? QueryDeleted;
        public event EventHandler? QueriesChanged;

        #region Saved Queries Management

        public List<SavedQuery> GetSavedQueries()
        {
            return _savedQueries.OrderByDescending(q => q.IsFavorite)
                                .ThenByDescending(q => q.LastUsedDate)
                                .ToList();
        }

        public List<SavedQuery> GetFavoriteQueries()
        {
            return _savedQueries.Where(q => q.IsFavorite)
                                .OrderByDescending(q => q.LastUsedDate)
                                .ToList();
        }

        public List<SavedQuery> GetRecentQueries(int count = 5)
        {
            return _savedQueries.OrderByDescending(q => q.LastUsedDate)
                                .Take(count)
                                .ToList();
        }

        public SavedQuery? GetQueryById(string id)
        {
            return _savedQueries.FirstOrDefault(q => q.Id == id);
        }

        public void SaveQuery(SavedQuery query)
        {
            var existingQuery = _savedQueries.FirstOrDefault(q => q.Id == query.Id);
            if (existingQuery != null)
            {
                var index = _savedQueries.IndexOf(existingQuery);
                _savedQueries[index] = query;
            }
            else
            {
                _savedQueries.Add(query);
            }

            SaveToFile();
            QuerySaved?.Invoke(this, query);
            QueriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool DeleteQuery(string id)
        {
            var query = _savedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null)
            {
                _savedQueries.Remove(query);
                SaveToFile();
                QueryDeleted?.Invoke(this, id);
                QueriesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void UseQuery(string id)
        {
            var query = _savedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null)
            {
                query.LastUsedDate = DateTime.Now;
                query.UseCount++;
                SaveToFile();
                QueriesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ToggleFavorite(string id)
        {
            var query = _savedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null)
            {
                query.IsFavorite = !query.IsFavorite;
                SaveToFile();
                QueriesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Quick Filters

        public List<SearchQuickFilter> GetQuickFilters()
        {
            return _quickFilters;
        }

        private List<SearchQuickFilter> InitializeQuickFilters()
        {
            return new List<SearchQuickFilter>
            {
                new SearchQuickFilter
                {
                    Name = "自分担当",
                    Icon = "👤",
                    Tooltip = "自分に割り当てられたアイテム",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "自分担当",
                        AssignedTo = Environment.UserName
                    }
                },
                new SearchQuickFilter
                {
                    Name = "今日更新",
                    Icon = "📅",
                    Tooltip = "今日更新されたアイテム",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "今日更新",
                        UpdatedAfter = DateTime.Today
                    }
                },
                new SearchQuickFilter
                {
                    Name = "高優先度",
                    Icon = "🔥",
                    Tooltip = "優先度が高いアイテム",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "高優先度",
                        MaxPriority = 1
                    }
                },
                new SearchQuickFilter
                {
                    Name = "未解決",
                    Icon = "⚠️",
                    Tooltip = "Active状態のアイテム",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "未解決",
                        State = "Active"
                    }
                },
                new SearchQuickFilter
                {
                    Name = "今週作成",
                    Icon = "🆕",
                    Tooltip = "今週作成されたアイテム",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "今週作成",
                        CreatedAfter = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek)
                    }
                },
                new SearchQuickFilter
                {
                    Name = "バグ",
                    Icon = "🐛",
                    Tooltip = "バグアイテムのみ",
                    QueryBuilder = () => new SavedQuery
                    {
                        Name = "バグ",
                        WorkItemType = "Bug"
                    }
                }
            };
        }

        #endregion

        #region File Operations

        private List<SavedQuery> LoadSavedQueries()
        {
            try
            {
                if (File.Exists(_savedQueriesPath))
                {
                    var json = File.ReadAllText(_savedQueriesPath);
                    return JsonConvert.DeserializeObject<List<SavedQuery>>(json) ?? new List<SavedQuery>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load saved queries: {ex.Message}");
            }

            return new List<SavedQuery>();
        }

        private void SaveToFile()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_savedQueries, Formatting.Indented);
                File.WriteAllText(_savedQueriesPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save queries: {ex.Message}");
            }
        }

        #endregion

        #region Search and Filter Helpers

        public List<SavedQuery> SearchQueries(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return GetSavedQueries();

            searchText = searchText.ToLowerInvariant();
            return _savedQueries.Where(q => 
                q.Name.ToLowerInvariant().Contains(searchText) ||
                q.Description.ToLowerInvariant().Contains(searchText) ||
                q.Projects.Any(p => p.ToLowerInvariant().Contains(searchText)) ||
                q.Areas.Any(a => a.ToLowerInvariant().Contains(searchText)) ||
                q.WorkItemType.ToLowerInvariant().Contains(searchText)
            ).OrderByDescending(q => q.IsFavorite)
             .ThenByDescending(q => q.LastUsedDate)
             .ToList();
        }

        public SavedQuery CreateQueryFromCurrentFilter(string name, string description = "")
        {
            return new SavedQuery
            {
                Name = name,
                Description = description,
                CreatedDate = DateTime.Now,
                LastUsedDate = DateTime.Now
            };
        }

        #endregion
    }
}