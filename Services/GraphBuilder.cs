using System.Collections.Generic;
using System.Linq;
using ArticleGraphProject.Models;

namespace ArticleGraphProject.Services
{
    // Builds a global citation graph from article data
    public class GraphBuilder
    {
        private readonly JsonParser _jsonParser;

        public GraphBuilder()
        {
            _jsonParser = new JsonParser();
        }

        // Create global graph with all articles
        public Graph BuildGlobalGraph(List<Article> articles)
        {
            var graph = new Graph();

            // Step 1: Create nodes for all articles
            foreach (var article in articles)
            {
                string id = _jsonParser.ExtractId(article.Id);
                string authorInitials = _jsonParser.GenerateAuthorInitials(article.Authors);

                var node = new GraphNode(id, article, authorInitials);
                graph.AddNode(node);
            }

            // Step 2: Create black edges from referenced_works
            foreach (var article in articles)
            {
                string fromId = _jsonParser.ExtractId(article.Id);

                foreach (var referencedWork in article.ReferencedWorks)
                {
                    string toId = _jsonParser.ExtractId(referencedWork);

                    // Add edge only if both nodes exist in the graph
                    if (graph.Nodes.ContainsKey(fromId) && graph.Nodes.ContainsKey(toId))
                    {
                        graph.AddEdge(fromId, toId, EdgeType.Black);
                    }
                }
            }

            // Step 3: Create green edges for connectivity (sequential)
            var sortedNodeIds = graph.Nodes.Keys.OrderBy(id => id).ToList();
            for (int i = 0; i < sortedNodeIds.Count - 1; i++)
            {
                graph.AddEdge(sortedNodeIds[i], sortedNodeIds[i + 1], EdgeType.Green);
            }

            return graph;
        }
    }
}
