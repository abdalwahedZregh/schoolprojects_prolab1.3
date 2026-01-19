namespace ArticleGraphProject.Models
{
    // Directed edge in the citation graph
    public class GraphEdge
    {
        public string From { get; set; }
        public string To { get; set; }
        public EdgeType EdgeType { get; set; }

        public GraphEdge(string from, string to, EdgeType edgeType)
        {
            From = from;
            To = to;
            EdgeType = edgeType;
        }
    }
}
