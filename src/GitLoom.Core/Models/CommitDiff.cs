using System.Collections.Generic;

namespace GitLoom.Core.Models;

public enum DiffLineType
{
    Header,
    Context,
    Added,
    Deleted
}

public class DiffLine
{
    public required string Content { get; set; }
    public required DiffLineType Type { get; set; }
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
}

public class FileDiff
{
    public required string Path { get; set; }
    public required string OldPath { get; set; }
    public required string Status { get; set; } // "Added", "Deleted", "Modified", "Renamed"
    public List<DiffLine> Lines { get; set; } = [];
}

public class CommitDiff
{
    public required string CommitSha { get; set; }
    public List<FileDiff> FileDiffs { get; set; } = [];
}
