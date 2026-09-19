using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace AutoWorks;

public sealed partial class MainForm : Form
{
    private readonly IServiceProvider serviceProvider;

    public MainForm()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddMudServices();
        serviceProvider = services.BuildServiceProvider();


        BlazorWebView.HostPage = "wwwroot/index.html";
        BlazorWebView.Services = serviceProvider;
        BlazorWebView.RootComponents.Clear();
        BlazorWebView.RootComponents.Add<App>("#app");
    }
}
