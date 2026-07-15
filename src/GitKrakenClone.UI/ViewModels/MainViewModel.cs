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

    public MainViewModel(IGitService gitService)
    {
        _gitService = gitService;
        _laneAssigner = new LaneAssigner();
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
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
            CurrentBranch = _gitService.GetCurrentBranchName();

            // Fetch commits
            var commitInfos = _gitService.GetCommits();
            
            // Map to ViewModels
            var viewModels = commitInfos.Select(c => new CommitViewModel(c)).ToList();
            Commits = new ObservableCollection<CommitViewModel>(viewModels);

            // Calculate Graph Layout
            GraphLayout = _laneAssigner.CalculateLayout(commitInfos);

            // Fetch current branches and tags
            Branches = _gitService.GetLocalBranches().Concat(_gitService.GetRemoteBranches()).ToList();
            Tags = _gitService.GetTags();

            // Set HEAD SHA
            // Search if HEAD branch matches tip commit
            var currentBranch = _gitService.GetCurrentBranchName();
            if (currentBranch != null)
            {
                // LibGit2Sharp usually resolves head branch tip, let's find if any commit matches the head tip
                // Or we can find the commit that has currentBranch in its Branches list
                var headCommit = Commits.FirstOrDefault(c => c.Branches.Contains(currentBranch));
                HeadSha = headCommit?.Sha;
            }

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
            // LibGit2Sharp diff can block the UI thread, so we run it in background
            var diff = await Task.Run(() => _gitService.GetCommitDiff(sha));
            
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
}
