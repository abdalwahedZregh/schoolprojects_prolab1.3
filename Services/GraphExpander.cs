using System.Collections.Generic;
using System.Linq;
using ArticleGraphProject.Models;

namespace ArticleGraphProject.Services
{
    // H-Index calculation result
    public class HIndexResult
    {
        public int HIndex { get; set; }
        public List<string> HCore { get; set; }
        public double HMedian { get; set; }

        public HIndexResult()
        {
            HCore = new List<string>();
        }
    }

    // H-Index based incremental graph expansion management
    public class GraphExpander
    {
        private readonly Graph _globalGraph;
        private readonly JsonParser _jsonParser;

        public GraphExpander(Graph globalGraph)
        {
            _globalGraph = globalGraph;
            _jsonParser = new JsonParser();
        }

        // Expand graph by adding H-Core nodes and relevant edges
        public HIndexResult ExpandGraph(Graph currentGraph, string articleId)
        {
            // Calculate H-Index for the selected article
            var hIndexResult = CalculateHIndex(articleId);

            // Add H-Core nodes to the current graph
            foreach (var coreNodeId in hIndexResult.HCore)
            {
                if (!currentGraph.Nodes.ContainsKey(coreNodeId))
                {
                    var globalNode = _globalGraph.GetNode(coreNodeId);
                    if (globalNode != null)
                    {
                        // Create node copy
                        var newNode = new GraphNode(
                            globalNode.NumericId,
                            globalNode.ArticleData,
                            globalNode.AuthorInitials
                        );
                        currentGraph.AddNode(newNode);
                    }
                }
            }

            // Add black edges between all nodes in the expanded graph
            AddRelevantEdges(currentGraph);

            return hIndexResult;
        }

        // Add edges from global graph connecting current graph nodes
        private void AddRelevantEdges(Graph currentGraph)
        {
            var currentNodeIds = new HashSet<string>(currentGraph.Nodes.Keys);

            // Scan all black edges from global graph
            foreach (var edge in _globalGraph.Edges.Where(e => e.EdgeType == EdgeType.Black))
            {
                if (currentNodeIds.Contains(edge.From) && currentNodeIds.Contains(edge.To))
                {
                    // Check if edge already exists
                    if (!currentGraph.Edges.Any(e => e.From == edge.From && e.To == edge.To && e.EdgeType == EdgeType.Black))
                    {
                        currentGraph.AddEdge(edge.From, edge.To, EdgeType.Black);
                    }
                }
            }

            // Green edges for connectivity visualization
            foreach (var greenEdge in _globalGraph.GreenEdges)
            {
                if (currentNodeIds.Contains(greenEdge.From) && currentNodeIds.Contains(greenEdge.To))
                {
                    if (!currentGraph.Edges.Any(e => e.From == greenEdge.From && e.To == greenEdge.To && e.EdgeType == EdgeType.Green))
                    {
                        currentGraph.AddEdge(greenEdge.From, greenEdge.To, EdgeType.Green);
                    }
                }
            }
        }
        
        // Check edge existence in graph
        private bool HasEdge(Graph graph, string from, string to, EdgeType type)
        {
            return graph.Edges.Any(e => e.From == from && e.To == to && e.EdgeType == type);
        }

        // Calculate H-Index for a specific article
        public HIndexResult CalculateHIndex(string articleId)
        {
            var result = new HIndexResult();

            // Get target node
            var targetNode = _globalGraph.GetNode(articleId);
            if (targetNode == null)
            {
                result.HIndex = 0;
                result.HMedian = 0;
                return result;
            }

            var citingArticles = targetNode.IncomingEdges.ToList();

            // Calculate citation count for each citing article
            var citationCounts = new List<(string ArticleId, int CitationCount)>();
            foreach (var citingArticle in citingArticles)
            {
                int citationCount = _globalGraph.GetIncomingBlackEdgeCount(citingArticle);
                citationCounts.Add((citingArticle, citationCount));
            }

            // Sort descending by citation count
            citationCounts = citationCounts.OrderByDescending(x => x.CitationCount).ToList();

            // H-Index: the largest value H such that at least H articles have >= H citations
            int hIndex = 0;
            for (int i = 0; i < citationCounts.Count; i++)
            {
                int h = i + 1;
                if (citationCounts[i].CitationCount >= h)
                {
                    hIndex = h;
                }
                else
                {
                    break;
                }
            }

            result.HIndex = hIndex;

            // H-Core: articles defining the H-Index (first H articles)
            if (hIndex > 0)
            {
                result.HCore = citationCounts.Take(hIndex).Select(x => x.ArticleId).ToList();

                // H-Median: median of H-Core citation counts
                var hCoreCounts = citationCounts.Take(hIndex).Select(x => x.CitationCount).OrderBy(x => x).ToList();
                if (hCoreCounts.Count % 2 == 0)
                {
                    result.HMedian = (hCoreCounts[hCoreCounts.Count / 2 - 1] + hCoreCounts[hCoreCounts.Count / 2]) / 2.0;
                }
                else
                {
                    result.HMedian = hCoreCounts[hCoreCounts.Count / 2];
                }
            }

            return result;
        }
    }
}
