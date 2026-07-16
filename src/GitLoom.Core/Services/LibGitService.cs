using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using GitLoom.Core.Models;

namespace GitLoom.Core.Services;

public class LibGitService : IGitService
{
    private Repository? _repo;

    public bool IsRepository(string path)
    {
        try
        {
            return Repository.IsValid(path);
        }
        catch
        {
            return false;
        }
    }

    public void OpenRepository(string path)
    {
        CloseRepository();
        if (!IsRepository(path))
        {
            throw new ArgumentException("Path is not a valid git repository.", nameof(path));
        }
        _repo = new Repository(path);
    }

    public void CloseRepository()
    {
        _repo?.Dispose();
        _repo = null;
    }

    public string GetRepositoryName()
    {
        if (_repo == null) return string.Empty;
        var dirInfo = new DirectoryInfo(_repo.Info.WorkingDirectory);
        return dirInfo.Name;
    }

    public List<string> GetLocalBranches()
    {
        if (_repo == null) return [];
        return _repo.Branches
            .Where(b => !b.IsRemote)
            .Select(b => b.FriendlyName)
            .ToList();
    }

    public List<string> GetRemoteBranches()
    {
        if (_repo == null) return [];
        return _repo.Branches
            .Where(b => b.IsRemote)
            .Select(b => b.FriendlyName)
            .ToList();
    }

    public List<string> GetTags()
    {
        if (_repo == null) return [];
        return _repo.Tags
            .Select(t => t.FriendlyName)
            .ToList();
    }

    public string? GetCurrentBranchName()
    {
        if (_repo == null) return null;
        return _repo.Head?.FriendlyName;
    }

    public List<CommitInfo> GetCommits(int limit = 0)
    {
        if (_repo == null) return [];

        // Build mapping of commit SHA to branches and tags
        var shaToBranches = new Dictionary<string, List<string>>();
        foreach (var branch in _repo.Branches)
        {
            if (branch.Tip == null) continue;
            var sha = branch.Tip.Sha;
            if (!shaToBranches.TryGetValue(sha, out var list))
            {
                list = [];
                shaToBranches[sha] = list;
            }
            list.Add(branch.FriendlyName);
        }

        var shaToTags = new Dictionary<string, List<string>>();
        foreach (var tag in _repo.Tags)
        {
            if (tag.Target == null) continue;
            // Target might be an annotated tag, resolve to commit
            var commit = tag.PeeledTarget as Commit ?? tag.Target as Commit;
            if (commit == null) continue;

            var sha = commit.Sha;
            if (!shaToTags.TryGetValue(sha, out var list))
            {
                list = [];
                shaToTags[sha] = list;
            }
            list.Add(tag.FriendlyName);
        }

        // Walk commits in topological-time order
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
            IncludeReachableFrom = _repo.Refs
        };

        IEnumerable<Commit> commits = _repo.Commits.QueryBy(filter);
        if (limit > 0)
        {
            commits = commits.Take(limit);
        }

        var commitList = new List<CommitInfo>();
        foreach (var c in commits)
        {
            commitList.Add(new CommitInfo
            {
                Sha = c.Sha,
                ShortSha = c.Sha.Substring(0, 7),
                Message = c.Message,
                MessageSubject = c.MessageShort,
                AuthorName = c.Author.Name,
                AuthorEmail = c.Author.Email,
                CommitterName = c.Committer.Name,
                CommitterEmail = c.Committer.Email,
                AuthorDateTime = c.Author.When,
                ParentShas = c.Parents.Select(p => p.Sha).ToList(),
                Branches = shaToBranches.GetValueOrDefault(c.Sha) ?? [],
                Tags = shaToTags.GetValueOrDefault(c.Sha) ?? []
            });
        }

        return commitList;
    }

    public CommitDiff GetCommitDiff(string commitSha)
    {
        if (_repo == null) throw new InvalidOperationException("Repository is not open.");

        var commit = _repo.Lookup<Commit>(commitSha);
        if (commit == null) throw new ArgumentException($"Commit {commitSha} not found.", nameof(commitSha));

        var commitDiff = new CommitDiff { CommitSha = commitSha };

        // For first parent, if it exists. Else compare against empty tree
        var parent = commit.Parents.FirstOrDefault();
        Tree? parentTree = parent?.Tree;
        Tree commitTree = commit.Tree;

        // Perform comparison
        var patch = _repo.Diff.Compare<Patch>(parentTree, commitTree);

        foreach (var entry in patch)
        {
            var fileDiff = new FileDiff
            {
                Path = entry.Path,
                OldPath = entry.OldPath,
                Status = entry.Status.ToString()
            };

            // Parse unified diff string
            var patchString = entry.Patch;
            if (!string.IsNullOrEmpty(patchString))
            {
                fileDiff.Lines = ParseUnifiedPatch(patchString);
            }

            commitDiff.FileDiffs.Add(fileDiff);
        }

        return commitDiff;
    }

    private List<DiffLine> ParseUnifiedPatch(string patchContent)
    {
        var lines = new List<DiffLine>();
        var patchLines = patchContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int oldLineNum = 0;
        int newLineNum = 0;

        foreach (var pl in patchLines)
        {
            if (string.IsNullOrEmpty(pl)) continue;

            if (pl.StartsWith("---") || pl.StartsWith("+++") || pl.StartsWith("diff --git") || pl.StartsWith("index ") || pl.StartsWith("new file mode") || pl.StartsWith("deleted file mode"))
            {
                lines.Add(new DiffLine { Content = pl, Type = DiffLineType.Header });
            }
            else if (pl.StartsWith("@@"))
            {
                lines.Add(new DiffLine { Content = pl, Type = DiffLineType.Header });
                // Parse hunk header: @@ -oldStart,oldLength +newStart,newLength @@
                // Example: @@ -1,5 +1,6 @@
                var parts = pl.Split(' ');
                if (parts.Length >= 3)
                {
                    var oldPart = parts[1].TrimStart('-');
                    var newPart = parts[2].TrimStart('+');

                    var oldSubParts = oldPart.Split(',');
                    var newSubParts = newPart.Split(',');

                    if (int.TryParse(oldSubParts[0], out var oStart))
                    {
                        oldLineNum = oStart - 1;
                    }
                    if (int.TryParse(newSubParts[0], out var nStart))
                    {
                        newLineNum = nStart - 1;
                    }
                }
            }
            else if (pl.StartsWith('+'))
            {
                newLineNum++;
                lines.Add(new DiffLine
                {
                    Content = pl,
                    Type = DiffLineType.Added,
                    NewLineNumber = newLineNum
                });
            }
            else if (pl.StartsWith('-'))
            {
                oldLineNum++;
                lines.Add(new DiffLine
                {
                    Content = pl,
                    Type = DiffLineType.Deleted,
                    OldLineNumber = oldLineNum
                });
            }
            else
            {
                // Context line starts with space or is empty
                oldLineNum++;
                newLineNum++;
                lines.Add(new DiffLine
                {
                    Content = pl,
                    Type = DiffLineType.Context,
                    OldLineNumber = oldLineNum,
                    NewLineNumber = newLineNum
                });
            }
        }

        return lines;
    }

    public void Dispose()
    {
        CloseRepository();
    }

    public (List<string> Unstaged, List<string> Staged) GetWorkingDirStatus()
    {
        if (_repo == null) return ([], []);
        
        var unstaged = new List<string>();
        var staged = new List<string>();
        
        try
        {
            var status = _repo.RetrieveStatus();
            foreach (var entry in status)
            {
                if (entry.State.HasFlag(FileStatus.ModifiedInIndex) || 
                    entry.State.HasFlag(FileStatus.NewInIndex) || 
                    entry.State.HasFlag(FileStatus.DeletedFromIndex) || 
                    entry.State.HasFlag(FileStatus.RenamedInIndex) || 
                    entry.State.HasFlag(FileStatus.TypeChangeInIndex))
                {
                    staged.Add(entry.FilePath);
                }
                else if (entry.State != FileStatus.Unaltered)
                {
                    unstaged.Add(entry.FilePath);
                }
            }
        }
        catch { }
        
        return (unstaged, staged);
    }

    public void StageFile(string filepath)
    {
        if (_repo == null) return;
        Commands.Stage(_repo, filepath);
    }

    public void UnstageFile(string filepath)
    {
        if (_repo == null) return;
        Commands.Unstage(_repo, filepath);
    }

    public void Commit(string message, string authorName, string authorEmail)
    {
        if (_repo == null) return;
        var author = new Signature(authorName, authorEmail, DateTimeOffset.Now);
        var committer = author;
        _repo.Commit(message, author, committer);
    }

    public CommitDiff GetWipDiff()
    {
        if (_repo == null) throw new InvalidOperationException("Repository is not open.");
        
        var commitDiff = new CommitDiff { CommitSha = "WIP" };
        
        try
        {
            var headCommit = _repo.Head.Tip;
            Tree? headTree = headCommit?.Tree;
            
            // Diff between HEAD and working directory (both staged & unstaged)
            var diff = _repo.Diff.Compare<Patch>(headTree, DiffTargets.WorkingDirectory);
            
            foreach (var entry in diff)
            {
                var fileDiff = new FileDiff
                {
                    Path = entry.Path,
                    OldPath = entry.OldPath,
                    Status = entry.Status.ToString()
                };

                var patchString = entry.Patch;
                if (!string.IsNullOrEmpty(patchString))
                {
                    fileDiff.Lines = ParseUnifiedPatch(patchString);
                }

                commitDiff.FileDiffs.Add(fileDiff);
            }
        }
        catch { }

        return commitDiff;
    }

    private void RunGitCommand(string arguments)
    {
        if (_repo == null) return;
        var workDir = _repo.Info.WorkingDirectory;
        
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null) return;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var err = process.StandardError.ReadToEnd();
            throw new Exception(err);
        }
    }

    public void Push()
    {
        RunGitCommand("push");
    }

    public void Pull()
    {
        RunGitCommand("pull");
    }

    public void Fetch()
    {
        RunGitCommand("fetch");
    }
}
