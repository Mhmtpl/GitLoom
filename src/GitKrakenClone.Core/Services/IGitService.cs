using System;
using System.Collections.Generic;
using GitKrakenClone.Core.Models;

namespace GitKrakenClone.Core.Services;

public interface IGitService : IDisposable
{
    bool IsRepository(string path);
    void OpenRepository(string path);
    void CloseRepository();
    List<CommitInfo> GetCommits(int limit = 0);
    CommitDiff GetCommitDiff(string commitSha);
    string GetRepositoryName();
    List<string> GetLocalBranches();
    List<string> GetRemoteBranches();
    List<string> GetTags();
    string? GetCurrentBranchName();
    
    // Working Directory & Staging API
    (List<string> Unstaged, List<string> Staged) GetWorkingDirStatus();
    void StageFile(string filepath);
    void UnstageFile(string filepath);
    void Commit(string message, string authorName, string authorEmail);
    CommitDiff GetWipDiff();
    
    // Remote Operations
    void Push();
    void Pull();
    void Fetch();
}
