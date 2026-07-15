using System;
using System.Collections.Generic;
using System.Linq;
using GitKrakenClone.Core.Models;

namespace GitKrakenClone.Rendering.Layout;

public enum EdgeType
{
    Straight,
    Merge,
    BranchSplit
}

public class CommitNodeLayout
{
    public required string Sha { get; set; }
    public required int Row { get; set; }
    public required int Lane { get; set; }
}

public class GraphEdgeLayout
{
    public required string SourceSha { get; set; }
    public required string TargetSha { get; set; }
    public required int SourceRow { get; set; }
    public required int SourceLane { get; set; }
    public required int TargetRow { get; set; }
    public required int TargetLane { get; set; }
    public required int ColorIndex { get; set; }
    public required EdgeType Type { get; set; }
}

public class CommitGraphLayout
{
    public Dictionary<string, CommitNodeLayout> Nodes { get; set; } = [];
    public List<GraphEdgeLayout> Edges { get; set; } = [];
    public int MaxLanes { get; set; }
}

public class LaneAssigner
{
    public CommitGraphLayout CalculateLayout(List<CommitInfo> commits)
    {
        var layout = new CommitGraphLayout();
        if (commits == null || commits.Count == 0)
        {
            return layout;
        }

        var activeLanes = new List<string?>();
        var nodes = new Dictionary<string, CommitNodeLayout>();
        var edges = new List<GraphEdgeLayout>();

        // Phase 1: Assign Lanes
        for (int row = 0; row < commits.Count; row++)
        {
            var commit = commits[row];
            int assignedLane = -1;

            // Find all active lanes targeting this commit
            var matchingLanes = new List<int>();
            for (int i = 0; i < activeLanes.Count; i++)
            {
                if (activeLanes[i] == commit.Sha)
                {
                    matchingLanes.Add(i);
                }
            }

            if (matchingLanes.Count == 0)
            {
                // This is a new branch tip. Find the first empty slot or append.
                assignedLane = activeLanes.IndexOf(null);
                if (assignedLane == -1)
                {
                    assignedLane = activeLanes.Count;
                    activeLanes.Add(commit.Sha);
                }
                else
                {
                    activeLanes[assignedLane] = commit.Sha;
                }
            }
            else
            {
                // Merge multiple active lanes targeting this commit into the leftmost lane
                assignedLane = matchingLanes[0];
                
                // Clear out the other lanes (they merge here)
                for (int i = 1; i < matchingLanes.Count; i++)
                {
                    activeLanes[matchingLanes[i]] = null;
                }
            }

            // Save layout for this node
            nodes[commit.Sha] = new CommitNodeLayout
            {
                Sha = commit.Sha,
                Row = row,
                Lane = assignedLane
            };

            // Set targets for the parents in activeLanes
            if (commit.ParentShas.Count > 0)
            {
                // Primary parent continues on the same lane
                var primaryParent = commit.ParentShas[0];
                activeLanes[assignedLane] = primaryParent;

                // Secondary parents (merge commits) get new lanes
                for (int pIdx = 1; pIdx < commit.ParentShas.Count; pIdx++)
                {
                    var parent = commit.ParentShas[pIdx];
                    
                    // Assign to the first free lane (excluding the current commit's lane)
                    int secondaryLane = -1;
                    for (int i = 0; i < activeLanes.Count; i++)
                    {
                        if (i != assignedLane && activeLanes[i] == null)
                        {
                            secondaryLane = i;
                            break;
                        }
                    }

                    if (secondaryLane == -1)
                    {
                        secondaryLane = activeLanes.Count;
                        activeLanes.Add(parent);
                    }
                    else
                    {
                        activeLanes[secondaryLane] = parent;
                    }
                }
            }
            else
            {
                // Root commit, close this lane
                activeLanes[assignedLane] = null;
            }
        }

        // Calculate max active lanes used
        layout.MaxLanes = activeLanes.Count;

        // Phase 2: Create Edges
        for (int row = 0; row < commits.Count; row++)
        {
            var commit = commits[row];
            if (!nodes.TryGetValue(commit.Sha, out var sourceNode)) continue;

            for (int pIdx = 0; pIdx < commit.ParentShas.Count; pIdx++)
            {
                var parentSha = commit.ParentShas[pIdx];
                if (!nodes.TryGetValue(parentSha, out var targetNode))
                {
                    // Parent commit is not in our loaded list, skip or draw a default down edge.
                    // For now, we skip it since we paged it out or it doesn't exist in viewport.
                    continue;
                }

                var edge = new GraphEdgeLayout
                {
                    SourceSha = commit.Sha,
                    TargetSha = parentSha,
                    SourceRow = sourceNode.Row,
                    SourceLane = sourceNode.Lane,
                    TargetRow = targetNode.Row,
                    TargetLane = targetNode.Lane,
                    ColorIndex = sourceNode.Lane, // Default color based on source
                    Type = EdgeType.Straight
                };

                if (sourceNode.Lane == targetNode.Lane)
                {
                    edge.Type = EdgeType.Straight;
                    edge.ColorIndex = sourceNode.Lane;
                }
                else
                {
                    if (pIdx == 0)
                    {
                        // Primary parent, but in a different lane. This is a branch split (e.g. branch created).
                        // It goes vertically down the child's lane, then curves into the parent's lane.
                        edge.Type = EdgeType.BranchSplit;
                        edge.ColorIndex = sourceNode.Lane;
                    }
                    else
                    {
                        // Secondary parent. This is a merge connection.
                        // It curves immediately from the child's lane to the parent's lane, then goes straight down.
                        edge.Type = EdgeType.Merge;
                        edge.ColorIndex = targetNode.Lane; // Paint with target lane color for visual consistency
                    }
                }

                edges.Add(edge);
            }
        }

        layout.Nodes = nodes;
        layout.Edges = edges;
        return layout;
    }
}
