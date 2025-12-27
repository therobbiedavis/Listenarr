using System;
using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    public enum SearchMode
    {
        Simple,
        Advanced
    }

    public class Pagination
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 50;
    }

    public class SearchRequest
    {
        public SearchMode Mode { get; set; } = SearchMode.Simple;

        // Simple free-text query (used by simple mode)
        public string? Query { get; set; }

        // Advanced fields
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Asin { get; set; }
        public string? Isbn { get; set; }
        public string? Series { get; set; }

        public Pagination? Pagination { get; set; } = new Pagination();

        public string Region { get; set; } = "us";
        public string? Language { get; set; }

        // If true, controller will enrich results with metadata (may incur additional requests)
        public bool IncludeEnrichment { get; set; } = true;

        // Optional cap on number of results to return
        public int? Cap { get; set; }
    }
}
