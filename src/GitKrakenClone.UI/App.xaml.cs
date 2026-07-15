using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using GitKrakenClone.Core.Services;
using GitKrakenClone.UI.ViewModels;

namespace GitKrakenClone.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IGitService, LibGitService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }
}
