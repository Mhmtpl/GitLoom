# 🧶 GitLoom

> **GitLoom** is a premium, high-performance desktop Git client built with **.NET 10 (LTS)**, **WPF**, **SkiaSharp**, and **LibGit2Sharp**. Inspired by the visual elegance of GitKraken, GitLoom renders your commit history as a beautiful, virtualized interactive loom of threads.

---

## ✨ Features

- **🚀 60fps Viewport Virtualization:** Custom SkiaSharp rendering engine that dynamically computes and paints only the visible commits and Bezier S-curves, providing buttery-smooth scrolling even on repositories with 10,000+ commits.
- **🎨 Elite Dark Theme:** A curated obsidian, charcoal, and neon cyan theme with refined borders and clean typographic hierarchy.
- **📂 Advanced Staging Panel:** A clutter-free drag/double-click interface to view unstaged/staged files, complete with file status indicators, clean hover-triggered ghost actions, and custom commit inputs.
- **🌿 Sorted Explorer:** Keep your workspace structured with separate sections for Local Branches, Remote Branches, and Tags, styled with unique colored accent bars.
- **🔄 Complete Sync Actions:** Full support for `Pull`, `Push`, and `Fetch` directly from the toolbar using your local Git credentials.

---

## 🛠️ Technology Stack

- **Core Framework:** .NET 10 (LTS) & WPF (Windows Presentation Foundation)
- **Git Operations:** LibGit2Sharp (compiled wrapper around libgit2)
- **High-Performance Graphics:** SkiaSharp (Google's Skia graphics engine wrapper)
- **Architecture Pattern:** MVVM (CommunityToolkit.Mvvm)

---

## 🚀 Quick Start

### Prerequisites
Make sure you have [.NET 10 SDK](https://dotnet.microsoft.com/download) installed on your machine.

### Build and Run
1. Clone the repository:
   ```bash
   git clone https://github.com/Mhmtpl/GitLoom.git
   cd GitLoom
   ```
2. Restore and build the solution:
   ```bash
   dotnet build
   ```
3. Run the UI application:
   ```bash
   dotnet run --project src/GitKrakenClone.UI
   ```

---

## 📂 Project Architecture

```
src/
├── GitKrakenClone.Core/         - Git operation services, models & unified diff parsers
├── GitKrakenClone.Rendering/    - Math layouts, lane assignment algorithms & Skia rendering controls
├── GitKrakenClone.UI/           - WPF Shell, Views, ViewModels, and controls
└── GitKrakenClone.Tests/        - Lane assignment test suites (xUnit)
```

---

## 📄 License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
