namespace ArticleGraphProject.Models
{
    // Edge type in the graph
    public enum EdgeType
    {
        // Black edge - reference/citation edge (used in all metrics)
        Black,

        // Green edge - traversal edge for connectivity only (excluded from metrics)
        Green
    }
}
