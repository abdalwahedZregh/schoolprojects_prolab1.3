using System.Collections.Generic;

namespace ArticleGraphProject.Models
{
    // Research article from JSON dataset
    public class Article
    {
        public string Id { get; set; } = string.Empty;

        public string? Doi { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Year { get; set; }

        public List<string> Authors { get; set; } = new List<string>();

        public string? Venue { get; set; }

        public List<string>? Keywords { get; set; }

        public List<string> ReferencedWorks { get; set; } = new List<string>();
    }
}
