using System.Collections.Generic;
using System.Linq;

namespace ArticleGraphProject.Models
{
    public class Graph
    {
        public Dictionary<string, GraphNode> Nodes { get; set; }
        public List<GraphEdge> Edges { get; set; }
        public List<GraphEdge> GreenEdges { get; set; }

        private Dictionary<string, int> _incomingBlackEdgeCounts;

        public Graph()
        {
            Nodes = new Dictionary<string, GraphNode>();
            Edges = new List<GraphEdge>();
            GreenEdges = new List<GraphEdge>();
            _incomingBlackEdgeCounts = new Dictionary<string, int>();
        }

        public void ClearCache()
        {
            _incomingBlackEdgeCounts.Clear();
        }

        public void AddNode(GraphNode node)
        {
            if (!Nodes.ContainsKey(node.NumericId))
                Nodes[node.NumericId] = node;
        }

        public void AddEdge(string from, string to, EdgeType edgeType)
        {
            Edges.Add(new GraphEdge(from, to, edgeType));
            if (edgeType == EdgeType.Green) GreenEdges.Add(new GraphEdge(from, to, edgeType));

            if (edgeType == EdgeType.Black)
            {
                if (Nodes.ContainsKey(from)) Nodes[from].OutgoingEdges.Add(to);
                if (Nodes.ContainsKey(to))
                {
                    Nodes[to].IncomingEdges.Add(from);
                    if (_incomingBlackEdgeCounts.ContainsKey(to)) _incomingBlackEdgeCounts.Remove(to);
                }
            }
        }

        public GraphNode? GetNode(string id)
        {
            return Nodes.ContainsKey(id) ? Nodes[id] : null;
        }

        public List<string> GetNeighbors(string nodeId, bool blackEdgesOnly = true)
        {
            var neighbors = new HashSet<string>();
            if (blackEdgesOnly)
            {
                if (Nodes.TryGetValue(nodeId, out var node))
                {
                    foreach (var neighbor in node.IncomingEdges) neighbors.Add(neighbor);
                    foreach (var neighbor in node.OutgoingEdges) neighbors.Add(neighbor);
                }
            }
            else
            {
                foreach (var edge in Edges)
                {
                    if (edge.From == nodeId) neighbors.Add(edge.To);
                    if (edge.To == nodeId) neighbors.Add(edge.From);
                }
            }
            return neighbors.ToList();
        }

        public int GetIncomingBlackEdgeCount(string nodeId)
        {
            if (_incomingBlackEdgeCounts.ContainsKey(nodeId)) return _incomingBlackEdgeCounts[nodeId];
            int count = Edges.Count(e => e.To == nodeId && e.EdgeType == EdgeType.Black);
            _incomingBlackEdgeCounts[nodeId] = count;
            return count;
        }

        public int GetOutgoingBlackEdgeCount(string nodeId)
        {
            return Edges.Count(e => e.From == nodeId && e.EdgeType == EdgeType.Black);
        }

        public int GetBlackEdgeCount()
        {
            return Edges.Count(e => e.EdgeType == EdgeType.Black);
        }

        // Get undirected black neighbors (incoming and outgoing edges)
        public List<string> GetUndirectedBlackNeighbors(string nodeId)
        {
            var neighbors = new HashSet<string>();
            if (Nodes.TryGetValue(nodeId, out var node))
            {
                neighbors.UnionWith(node.IncomingEdges);
                neighbors.UnionWith(node.OutgoingEdges);
            }
            return neighbors.ToList();
        }

        public Graph ToUndirected()
        {
            var undirected = new Graph();
            // Copy nodes
            foreach (var kvp in Nodes)
            {
                var original = kvp.Value;
                var newNode = new GraphNode(original.NumericId, original.ArticleData, original.AuthorInitials);
                undirected.AddNode(newNode);
            }
            // Convert edges
            foreach (var edge in Edges)
            {
                if (edge.EdgeType == EdgeType.Black)
                {
                    // Add forward direction
                    undirected.AddEdge(edge.From, edge.To, EdgeType.Black);
                    // Add reverse direction
                    undirected.AddEdge(edge.To, edge.From, EdgeType.Black);
                }
            }
            return undirected;
        }
    }
}
