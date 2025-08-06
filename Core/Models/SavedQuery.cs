using System;
using System.Collections.Generic;

namespace GadgetTools.Core.Models
{
    /// <summary>
    /// 保存済みクエリモデル
    /// </summary>
    public class SavedQuery
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastUsedDate { get; set; } = DateTime.Now;
        public int UseCount { get; set; } = 0;
        public bool IsFavorite { get; set; } = false;
        
        // Query parameters
        public List<string> Projects { get; set; } = new List<string>();
        public List<string> Areas { get; set; } = new List<string>();
        public List<string> Iterations { get; set; } = new List<string>();
        public string WorkItemType { get; set; } = "";
        public string State { get; set; } = "";
        public string AssignedTo { get; set; } = "";
        public int MaxResults { get; set; } = 100;
        
        // Extended search parameters
        public string TitleSearch { get; set; } = "";
        public string DescriptionSearch { get; set; } = "";
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public DateTime? UpdatedAfter { get; set; }
        public DateTime? UpdatedBefore { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public int? MinPriority { get; set; }
        public int? MaxPriority { get; set; }
    }

    /// <summary>
    /// クイックフィルタ定義（検索用）
    /// </summary>
    public class SearchQuickFilter
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Tooltip { get; set; } = "";
        public Func<SavedQuery> QueryBuilder { get; set; } = () => new SavedQuery();
        public bool IsBuiltIn { get; set; } = true;
    }
}