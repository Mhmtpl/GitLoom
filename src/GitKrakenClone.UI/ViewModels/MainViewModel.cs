using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitKrakenClone.Core.Models;
using GitKrakenClone.Core.Services;
using GitKrakenClone.Rendering.Layout;

namespace GitKrakenClone.UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly LaneAssigner _laneAssigner;

    private string _repositoryPath = string.Empty;
    private string _repositoryName = "No Repository Open";
    private string? _currentBranch = string.Empty;
    private string? _selectedSha;
    private CommitViewModel? _selectedCommit;
    private CommitGraphLayout? _graphLayout;
    private string? _headSha;
    private double _rowHeight = 40.0;

    public double RowHeight
    {
        get => _rowHeight;
        set => SetProperty(ref _rowHeight, value);
    }

    private ObservableCollection<CommitViewModel> _commits = [];
    private List<string> _branches = [];
    private List<string> _tags = [];

    private CommitDiff? _selectedCommitDiff;
    private List<FileDiff> _selectedFileDiffs = [];
    private FileDiff? _selectedFile;
    private List<DiffLine> _selectedFileLines = [];

    public string RepositoryPath
    {
        get => _repositoryPath;
        set => SetProperty(ref _repositoryPath, value);
    }

    public string RepositoryName
    {
        get => _repositoryName;
        set => SetProperty(ref _repositoryName, value);
    }

    public string? CurrentBranch
    {
        get => _currentBranch;
        set => SetProperty(ref _currentBranch, value);
    }

    public string? SelectedSha
    {
        get => _selectedSha;
        set
        {
            if (SetProperty(ref _selectedSha, value))
            {
                if (value != null)
                {
                    var matching = Commits.FirstOrDefault(c => c.Sha == value);
                    if (matching != null && SelectedCommit != matching)
                    {
                        SelectedCommit = matching;
                    }
                }
                else
                {
                    SelectedCommit = null;
                }
            }
        }
    }

    public CommitViewModel? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            if (SetProperty(ref _selectedCommit, value))
            {
                SelectedSha = value?.Sha;
                _ = LoadCommitDiffAsync();
            }
        }
    }

    public CommitGraphLayout? GraphLayout
    {
        get => _graphLayout;
        set => SetProperty(ref _graphLayout, value);
    }

    public string? HeadSha
    {
        get => _headSha;
        set => SetProperty(ref _headSha, value);
    }

    public ObservableCollection<CommitViewModel> Commits
    {
        get => _commits;
        set => SetProperty(ref _commits, value);
    }

    public List<string> Branches
    {
        get => _branches;
        set => SetProperty(ref _branches, value);
    }

    public List<string> Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    private List<string> _localBranches = [];
    public List<string> LocalBranches
    {
        get => _localBranches;
        set => SetProperty(ref _localBranches, value);
    }

    private List<string> _remoteBranches = [];
    public List<string> RemoteBranches
    {
        get => _remoteBranches;
        set => SetProperty(ref _remoteBranches, value);
    }

    private ObservableCollection<string> _unstagedFiles = [];
    public ObservableCollection<string> UnstagedFiles
    {
        get => _unstagedFiles;
        set => SetProperty(ref _unstagedFiles, value);
    }

    private ObservableCollection<string> _stagedFiles = [];
    public ObservableCollection<string> StagedFiles
    {
        get => _stagedFiles;
        set => SetProperty(ref _stagedFiles, value);
    }

    private string _commitSubject = string.Empty;
    public string CommitSubject
    {
        get => _commitSubject;
        set => SetProperty(ref _commitSubject, value);
    }

    private string _commitDescription = string.Empty;
    public string CommitDescription
    {
        get => _commitDescription;
        set => SetProperty(ref _commitDescription, value);
    }

    public CommitDiff? SelectedCommitDiff
    {
        get => _selectedCommitDiff;
        set => SetProperty(ref _selectedCommitDiff, value);
    }

    public List<FileDiff> SelectedFileDiffs
    {
        get => _selectedFileDiffs;
        set => SetProperty(ref _selectedFileDiffs, value);
    }

    public FileDiff? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                SelectedFileLines = value?.Lines ?? [];
            }
        }
    }

    public List<DiffLine> SelectedFileLines
    {
        get => _selectedFileLines;
        set => SetProperty(ref _selectedFileLines, value);
    }

    public ICommand OpenRepositoryCommand { get; }
    public ICommand StageFileCommand { get; }
    public ICommand UnstageFileCommand { get; }
    public ICommand StageAllCommand { get; }
    public ICommand UnstageAllCommand { get; }
    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand PullCommand { get; }
    public ICommand FetchCommand { get; }

    public MainViewModel(IGitService gitService)
    {
        _gitService = gitService;
        _laneAssigner = new LaneAssigner();
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
        
        StageFileCommand = new RelayCommand<string>(StageFile);
        UnstageFileCommand = new RelayCommand<string>(UnstageFile);
        StageAllCommand = new RelayCommand(StageAll);
        UnstageAllCommand = new RelayCommand(UnstageAll);
        CommitCommand = new RelayCommand(CommitChanges);
        
        PushCommand = new RelayCommand(Push);
        PullCommand = new RelayCommand(Pull);
        FetchCommand = new RelayCommand(Fetch);
    }

    private void OpenRepository()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Git Repository Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            var path = dialog.FolderName;
            if (_gitService.IsRepository(path))
            {
                LoadRepository(path);
            }
            else
            {
                System.Windows.MessageBox.Show("Selected folder is not a valid Git repository.", "Invalid Repository", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public void LoadRepository(string path)
    {
        try
        {
            RepositoryPath = path;
            _gitService.OpenRepository(path);
            RepositoryName = _gitService.GetRepositoryName();
            
            // Set HEAD SHA first
            var currentBranch = _gitService.GetCurrentBranchName();
            CurrentBranch = currentBranch;
            string? headSha = null;

            // Fetch commits
            var commitInfos = _gitService.GetCommits();
            if (currentBranch != null)
            {
                var headCommit = commitInfos.FirstOrDefault(c => c.Branches.Contains(currentBranch));
                headSha = headCommit?.Sha;
                HeadSha = headSha;
            }

            // Check WIP Status
            var (unstaged, staged) = _gitService.GetWorkingDirStatus();
            UnstagedFiles = new ObservableCollection<string>(unstaged);
            StagedFiles = new ObservableCollection<string>(staged);

            if (unstaged.Count > 0 || staged.Count > 0)
            {
                var wipInfo = new CommitInfo
                {
                    Sha = "WIP",
                    ShortSha = "WIP",
                    Message = "Uncommitted changes in working directory.",
                    MessageSubject = "Work in Progress",
                    AuthorName = "You",
                    AuthorEmail = "",
                    CommitterName = "You",
                    CommitterEmail = "",
                    AuthorDateTime = DateTimeOffset.Now,
                    ParentShas = headSha != null ? new List<string> { headSha } : new List<string>(),
                    Branches = [],
                    Tags = []
                };
                commitInfos.Insert(0, wipInfo);
            }

            // Map to ViewModels
            var viewModels = commitInfos.Select(c => new CommitViewModel(c)).ToList();
            Commits = new ObservableCollection<CommitViewModel>(viewModels);

            // Calculate Graph Layout
            GraphLayout = _laneAssigner.CalculateLayout(commitInfos);

            // Fetch current branches and tags
            LocalBranches = _gitService.GetLocalBranches();
            RemoteBranches = _gitService.GetRemoteBranches();
            Branches = LocalBranches.Concat(RemoteBranches).ToList(); // Keep for backward compat
            Tags = _gitService.GetTags();

            // Reset selected states
            SelectedCommit = null;
            SelectedCommitDiff = null;
            SelectedFileDiffs = [];
            SelectedFile = null;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error opening repository: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task LoadCommitDiffAsync()
    {
        if (SelectedCommit == null)
        {
            SelectedCommitDiff = null;
            SelectedFileDiffs = [];
            SelectedFile = null;
            return;
        }

        var sha = SelectedCommit.Sha;
        try
        {
            CommitDiff diff;
            if (sha == "WIP")
            {
                diff = await Task.Run(() => _gitService.GetWipDiff());
            }
            else
            {
                diff = await Task.Run(() => _gitService.GetCommitDiff(sha));
            }
            
            // Only update if selection didn't change while loading
            if (SelectedCommit?.Sha == sha)
            {
                SelectedCommitDiff = diff;
                SelectedFileDiffs = diff.FileDiffs;
                SelectedFile = diff.FileDiffs.FirstOrDefault();
            }
        }
        catch
        {
            SelectedCommitDiff = null;
            SelectedFileDiffs = [];
            SelectedFile = null;
        }
    }

    private void StageFile(string? filepath)
    {
        if (string.IsNullOrEmpty(filepath)) return;
        _gitService.StageFile(filepath);
        RefreshStatus();
    }

    private void UnstageFile(string? filepath)
    {
        if (string.IsNullOrEmpty(filepath)) return;
        _gitService.UnstageFile(filepath);
        RefreshStatus();
    }

    private void StageAll()
    {
        foreach (var file in UnstagedFiles.ToList())
        {
            _gitService.StageFile(file);
        }
        RefreshStatus();
    }

    private void UnstageAll()
    {
        foreach (var file in StagedFiles.ToList())
        {
            _gitService.UnstageFile(file);
        }
        RefreshStatus();
    }

    private void CommitChanges()
    {
        if (string.IsNullOrWhiteSpace(CommitSubject))
        {
            System.Windows.MessageBox.Show("Please enter a commit message subject.", "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var message = CommitSubject;
        if (!string.IsNullOrWhiteSpace(CommitDescription))
        {
            message += "\n\n" + CommitDescription;
        }

        try
        {
            _gitService.Commit(message, "You", "you@example.com");
            CommitSubject = string.Empty;
            CommitDescription = string.Empty;
            LoadRepository(RepositoryPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error committing: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RefreshStatus()
    {
        var (unstaged, staged) = _gitService.GetWorkingDirStatus();
        UnstagedFiles = new ObservableCollection<string>(unstaged);
        StagedFiles = new ObservableCollection<string>(staged);
        
        // Refresh the WIP commit diff if it's selected
        if (SelectedCommit?.Sha == "WIP")
        {
            _ = LoadCommitDiffAsync();
        }
    }

    private void Push()
    {
        try
        {
            _gitService.Push();
            System.Windows.MessageBox.Show("Push operation completed successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadRepository(RepositoryPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error pushing: {ex.Message}", "Push Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void Pull()
    {
        try
        {
            _gitService.Pull();
            System.Windows.MessageBox.Show("Pull operation completed successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadRepository(RepositoryPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error pulling: {ex.Message}", "Pull Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void Fetch()
    {
        try
        {
            _gitService.Fetch();
            System.Windows.MessageBox.Show("Fetch operation completed successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadRepository(RepositoryPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error fetching: {ex.Message}", "Fetch Failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
