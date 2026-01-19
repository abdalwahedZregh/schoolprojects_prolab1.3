using System.Collections.Generic;
using System.Linq;
using ArticleGraphProject.Models;

namespace ArticleGraphProject.Algorithms
{
    // K-Core decomposition algorithm
    public class KCoreDecomposer
    {
        // Find K-Core: nodes remaining after iteratively removing nodes with degree less than K
        public HashSet<string> FindKCore(Graph graph, int k)
        {
            // Create working copy of node set and degree counts
            var workingNodes = new HashSet<string>(graph.Nodes.Keys);
            var degrees = new Dictionary<string, int>();

            // Initialize degrees (black edges only, undirected)
            foreach (var nodeId in workingNodes)
            {
                degrees[nodeId] = CountDegree(graph, nodeId, workingNodes);
            }

            // Iteratively remove nodes with degree < k
            bool changed = true;
            while (changed)
            {
                changed = false;

                // Find nodes to remove in this iteration
                var nodesToRemove = new List<string>();
                foreach (var nodeId in workingNodes)
                {
                    if (degrees[nodeId] < k)
                    {
                        nodesToRemove.Add(nodeId);
                    }
                }

                // Remove nodes and update degrees
                foreach (var nodeId in nodesToRemove)
                {
                    workingNodes.Remove(nodeId);
                    changed = true;
                }

                // Recalculate degrees for remaining nodes
                if (changed)
                {
                    foreach (var nodeId in workingNodes)
                    {
                        degrees[nodeId] = CountDegree(graph, nodeId, workingNodes);
                    }
                }
            }

            return workingNodes;
        }

        // Count node degree considering black edges within the working set
        private int CountDegree(Graph graph, string nodeId, HashSet<string> workingNodes)
        {
            int degree = 0;

            // Get undirected black neighbors
            var neighbors = graph.GetUndirectedBlackNeighbors(nodeId);
            
            foreach (var neighbor in neighbors)
            {
                // Count only neighbors in the working set
                if (workingNodes.Contains(neighbor))
                {
                    degree++;
                }
            }

            return degree;
        }
    }
}
