using System;
using System.Collections.Generic;
using Xunit;
using GitKrakenClone.Core.Models;
using GitKrakenClone.Rendering.Layout;

namespace GitKrakenClone.Tests;

public class LaneAssignerTests
{
    [Fact]
    public void CalculateLayout_LinearHistory_AllOnLaneZero()
    {
        // C2 -> C1 -> C0
        var commits = new List<CommitInfo>
        {
            new() { Sha = "C2", ShortSha = "C2", Message = "Commit 2", MessageSubject = "C2", AuthorName = "A", AuthorEmail = "A", CommitterName = "A", CommitterEmail = "A", AuthorDateTime = DateTimeOffset.Now.AddSeconds(2), ParentShas = ["C1"] },
            new() { Sha = "C1", ShortSha = "C1", Message = "Commit 1", MessageSubject = "C1", AuthorName = "A", AuthorEmail = "A", CommitterName = "A", CommitterEmail = "A", AuthorDateTime = DateTimeOffset.Now.AddSeconds(1), ParentShas = ["C0"] },
            new() { Sha = "C0", ShortSha = "C0", Message = "Commit 0", MessageSubject = "C0", AuthorName = "A", AuthorEmail = "A", CommitterName = "A", CommitterEmail = "A", AuthorDateTime = DateTimeOffset.Now, ParentShas = [] }
        };

        var assigner = new LaneAssigner();
        var layout = assigner.CalculateLayout(commits);

        Assert.Equal(3, layout.Nodes.Count);
        Assert.Equal(0, layout.Nodes["C2"].Lane);
        Assert.Equal(0, layout.Nodes["C1"].Lane);
        Assert.Equal(0, layout.Nodes["C0"].Lane);

        Assert.Equal(2, layout.Edges.Count);
        Assert.All(layout.Edges, edge => Assert.Equal(EdgeType.Straight, edge.Type));
    }

    [Fact]
    public void CalculateLayout_BranchSplit_AllocatesNewLane()
    {
        // C2 -> C1 -> C0
        //       B1 -> C0
        // Order: C2, C1, B1, C0
        var commits = new List<CommitInfo>
        {
            new() { Sha = "C2", ShortSha = "C2", Message = "C2", MessageSubject = "C2", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(3), ParentShas = ["C1"] },
            new() { Sha = "C1", ShortSha = "C1", Message = "C1", MessageSubject = "C1", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(2), ParentShas = ["C0"] },
            new() { Sha = "B1", ShortSha = "B1", Message = "B1", MessageSubject = "B1", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(1), ParentShas = ["C0"] },
            new() { Sha = "C0", ShortSha = "C0", Message = "C0", MessageSubject = "C0", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now, ParentShas = [] }
        };

        var assigner = new LaneAssigner();
        var layout = assigner.CalculateLayout(commits);

        Assert.Equal(4, layout.Nodes.Count);
        Assert.Equal(0, layout.Nodes["C2"].Lane);
        Assert.Equal(0, layout.Nodes["C1"].Lane);
        Assert.Equal(1, layout.Nodes["B1"].Lane); // B1 gets a new lane
        Assert.Equal(0, layout.Nodes["C0"].Lane); // C0 is on lane 0

        // Edges: C2 -> C1 (Straight), C1 -> C0 (Straight), B1 -> C0 (BranchSplit curve)
        var edgeB1ToC0 = layout.Edges.Find(e => e.SourceSha == "B1" && e.TargetSha == "C0");
        Assert.NotNull(edgeB1ToC0);
        Assert.Equal(EdgeType.BranchSplit, edgeB1ToC0.Type);
    }

    [Fact]
    public void CalculateLayout_MergeCommit_AllocatesAndMergesLane()
    {
        // C2 (merge C1 and B1) -> C1 -> C0
        //                         B1 -> C0
        // Order: C2, C1, B1, C0
        var commits = new List<CommitInfo>
        {
            new() { Sha = "C2", ShortSha = "C2", Message = "C2", MessageSubject = "C2", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(3), ParentShas = ["C1", "B1"] },
            new() { Sha = "C1", ShortSha = "C1", Message = "C1", MessageSubject = "C1", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(2), ParentShas = ["C0"] },
            new() { Sha = "B1", ShortSha = "B1", Message = "B1", MessageSubject = "B1", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now.AddSeconds(1), ParentShas = ["C0"] },
            new() { Sha = "C0", ShortSha = "C0", Message = "C0", MessageSubject = "C0", AuthorName = "A", AuthorEmail = "a", CommitterName = "a", CommitterEmail = "a", AuthorDateTime = DateTimeOffset.Now, ParentShas = [] }
        };

        var assigner = new LaneAssigner();
        var layout = assigner.CalculateLayout(commits);

        // C2 is on lane 0.
        // P0 (C1) continues on lane 0.
        // P1 (B1) is assigned a new lane (lane 1) at row 0.
        // So B1 should be on lane 1.
        Assert.Equal(0, layout.Nodes["C2"].Lane);
        Assert.Equal(0, layout.Nodes["C1"].Lane);
        Assert.Equal(1, layout.Nodes["B1"].Lane);
        Assert.Equal(0, layout.Nodes["C0"].Lane);

        // Edges:
        // C2 -> C1: lane 0 -> lane 0 (Straight)
        // C2 -> B1: lane 0 -> lane 1 (Merge connection)
        var edgeC2ToB1 = layout.Edges.Find(e => e.SourceSha == "C2" && e.TargetSha == "B1");
        Assert.NotNull(edgeC2ToB1);
        Assert.Equal(EdgeType.Merge, edgeC2ToB1.Type);
    }
}
