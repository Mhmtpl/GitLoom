using System;
using System.Collections.Generic;

namespace GitKrakenClone.Core.Models;

public class CommitInfo
{
    public required string Sha { get; set; }
    public required string ShortSha { get; set; }
    public required string Message { get; set; }
    public required string MessageSubject { get; set; }
    public required string AuthorName { get; set; }
    public required string AuthorEmail { get; set; }
    public required string CommitterName { get; set; }
    public required string CommitterEmail { get; set; }
    public required DateTimeOffset AuthorDateTime { get; set; }
    public List<string> ParentShas { get; set; } = [];
    public List<string> Branches { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}
