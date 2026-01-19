using System.Collections.Generic;

namespace ArticleGraphProject.Models
{
    // Node in the citation graph with enriched metadata
    public class GraphNode
    {
        // Numeric ID extracted from W-prefixed URL
        public string NumericId { get; set; }

        // Reference to original article data
        public Article ArticleData { get; set; }

        // Author initials in A.B.C. format
        public string AuthorInitials { get; set; }

        // Incoming edge IDs (those citing this article)
        public List<string> IncomingEdges { get; set; }

        // Outgoing edge IDs (those cited by this article)
        public List<string> OutgoingEdges { get; set; }

        // Node state for visualization
        public NodeState State { get; set; }

        public GraphNode(string numericId, Article article, string authorInitials)
        {
            NumericId = numericId;
            ArticleData = article;
            AuthorInitials = authorInitials;
            IncomingEdges = new List<string>();
            OutgoingEdges = new List<string>();
            State = NodeState.Normal;
        }
    }

    // Visual state of the node
    public enum NodeState
    {
        Normal,
        Selected,
        HCore,
        KCore
    }
}
