using System.Collections.Generic;

namespace Listenarr.Api.Models
{
    public class IndexerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Implementation { get; set; } = string.Empty;
        public string ConfigContract { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public bool EnableRss { get; set; }
        public bool EnableAutomaticSearch { get; set; }
        public bool EnableInteractiveSearch { get; set; }
        public int Priority { get; set; }
        public List<IndexerField> Fields { get; set; } = new List<IndexerField>();
        public List<string> Tags { get; set; } = new List<string>();
        public bool AddedByProwlarr { get; set; }
        public int? ProwlarrIndexerId { get; set; }
    }

    public class IndexerField
    {
        public string Name { get; set; } = string.Empty;
        public object Value { get; set; } = new object();
    }
}
