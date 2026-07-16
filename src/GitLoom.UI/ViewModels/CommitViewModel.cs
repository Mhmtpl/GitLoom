using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using GitLoom.Core.Models;

namespace GitLoom.UI.ViewModels;

public class CommitViewModel : ObservableObject
{
    private readonly CommitInfo _commitInfo;

    public CommitViewModel(CommitInfo commitInfo)
    {
        _commitInfo = commitInfo;
    }

    public string Sha => _commitInfo.Sha;
    public string ShortSha => _commitInfo.ShortSha;
    public string Message => _commitInfo.Message;
    public string MessageSubject => _commitInfo.MessageSubject;
    public string AuthorName => _commitInfo.AuthorName;
    public string AuthorEmail => _commitInfo.AuthorEmail;
    public string CommitterName => _commitInfo.CommitterName;
    public string CommitterEmail => _commitInfo.CommitterEmail;
    public DateTimeOffset AuthorDateTime => _commitInfo.AuthorDateTime;
    public List<string> ParentShas => _commitInfo.ParentShas;
    public List<string> Branches => _commitInfo.Branches;
    public List<string> Tags => _commitInfo.Tags;

    public string AuthorInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AuthorName)) return "?";
            var parts = AuthorName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }
    }

    public string RelativeDate
    {
        get
        {
            var span = DateTimeOffset.Now - _commitInfo.AuthorDateTime;
            if (span.TotalDays > 365)
            {
                int years = (int)(span.TotalDays / 365);
                return years == 1 ? "1 year ago" : $"{years} years ago";
            }
            if (span.TotalDays > 30)
            {
                int months = (int)(span.TotalDays / 30);
                return months == 1 ? "1 month ago" : $"{months} months ago";
            }
            if (span.TotalDays >= 7)
            {
                int weeks = (int)(span.TotalDays / 7);
                return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
            }
            if (span.TotalDays >= 1)
            {
                int days = (int)span.TotalDays;
                return days == 1 ? "Yesterday" : $"{days} days ago";
            }
            if (span.TotalHours >= 1)
            {
                int hours = (int)span.TotalHours;
                return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            }
            if (span.TotalMinutes >= 1)
            {
                int minutes = (int)span.TotalMinutes;
                return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
            }
            return "Just now";
        }
    }
}
