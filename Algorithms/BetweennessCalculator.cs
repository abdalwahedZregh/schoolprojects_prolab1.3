using System;
using System.Collections.Generic;
using System.Linq;
using ArticleGraphProject.Models;

namespace ArticleGraphProject.Algorithms
{
    // Betweenness Centrality calculation using Brandes algorithm
    // Graph is processed as undirected
    public class BetweennessCalculator
    {
        // Calculate betweenness centrality for all nodes using Brandes algorithm
        public Dictionary<string, double> CalculateBetweenness(Graph graph)
        {
            var betweenness = new Dictionary<string, double>();
            
            // Initialize betweenness for all nodes
            foreach (var nodeId in graph.Nodes.Keys)
            {
                betweenness[nodeId] = 0.0;
            }

            // Run Brandes algorithm from each node as source
            foreach (var source in graph.Nodes.Keys)
            {
                var stack = new Stack<string>();
                var predecessors = new Dictionary<string, List<string>>();
                var sigma = new Dictionary<string, double>();
                var distance = new Dictionary<string, int>();
                var delta = new Dictionary<string, double>();

                // Initialize
                foreach (var nodeId in graph.Nodes.Keys)
                {
                    predecessors[nodeId] = new List<string>();
                    sigma[nodeId] = 0;
                    distance[nodeId] = -1;
                    delta[nodeId] = 0;
                }

                sigma[source] = 1;
                distance[source] = 0;

                // BFS to find shortest paths
                var queue = new Queue<string>();
                queue.Enqueue(source);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    stack.Push(current);

                    // Undirected neighbors (black edges only)
                    var neighbors = graph.GetUndirectedBlackNeighbors(current);

                    foreach (var neighbor in neighbors)
                    {
                        // First visit to this neighbor
                        if (distance[neighbor] < 0)
                        {
                            queue.Enqueue(neighbor);
                            distance[neighbor] = distance[current] + 1;
                        }

                        // If shortest path passes through current node
                        if (distance[neighbor] == distance[current] + 1)
                        {
                            sigma[neighbor] += sigma[current];
                            predecessors[neighbor].Add(current);
                        }
                    }
                }

                // Accumulation phase - backtrack from farthest nodes
                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    
                    foreach (var predecessor in predecessors[node])
                    {
                        delta[predecessor] += (sigma[predecessor] / sigma[node]) * (1 + delta[node]);
                    }

                    // Accumulate betweenness (excluding source)
                    if (node != source)
                    {
                        betweenness[node] += delta[node];
                    }
                }
            }

            // Divide by 2 since graph is treated as undirected
            var result = new Dictionary<string, double>();
            foreach (var kvp in betweenness)
            {
                result[kvp.Key] = kvp.Value / 2.0;
            }

            return result;
        }

        // Sort nodes by betweenness centrality (descending)
        public List<(string NodeId, double Betweenness)> GetSortedBetweenness(Dictionary<string, double> betweenness)
        {
            return betweenness
                .Select(kvp => (kvp.Key, kvp.Value))
                .OrderByDescending(x => x.Value)
                .ToList();
        }
    }
}
