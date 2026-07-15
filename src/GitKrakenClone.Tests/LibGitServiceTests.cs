using System;
using System.IO;
using System.Linq;
using Xunit;
using GitKrakenClone.Core.Services;

namespace GitKrakenClone.Tests;

public class LibGitServiceTests
{
    [Fact]
    public void Test_LocalRepository_Loading()
    {
        // Path to the current repository (solution root contains .git)
        string repoPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        
        // Let's find the .git folder by walking up if needed
        string? current = AppContext.BaseDirectory;
        string? foundPath = null;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                foundPath = current;
                break;
            }
            current = Path.GetDirectoryName(current);
        }

        Assert.NotNull(foundPath);
        
        using var gitService = new LibGitService();
        Assert.True(gitService.IsRepository(foundPath));
        
        gitService.OpenRepository(foundPath);
        
        var repoName = gitService.GetRepositoryName();
        Assert.False(string.IsNullOrEmpty(repoName));
        
        var commits = gitService.GetCommits(50);
        Assert.NotNull(commits);
        
        // If there are commits, verify layout on them
        if (commits.Count > 0)
        {
            var firstCommit = commits.First();
            Assert.NotNull(firstCommit.Sha);
            Assert.NotNull(firstCommit.ShortSha);
            Assert.NotNull(firstCommit.MessageSubject);
            
            // Check diff
            var diff = gitService.GetCommitDiff(firstCommit.Sha);
            Assert.NotNull(diff);
            Assert.Equal(firstCommit.Sha, diff.CommitSha);
        }

        var branches = gitService.GetLocalBranches();
        Assert.NotNull(branches);
    }
}
