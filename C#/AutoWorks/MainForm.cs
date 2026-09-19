using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace AutoWorks
{
    public partial class MainForm : Form
    {
        private FormBorderStyle _previousBorderStyle;
        private Rectangle _previousBounds;
        private bool _wasMaximized;

        public bool IsFullScreen => FormBorderStyle == FormBorderStyle.None;

        public MainForm()
        {
            InitializeComponent( );

            var services = new ServiceCollection( );
            services.AddWindowsFormsBlazorWebView( );
            services.AddMudServices( );
            BlazorWebView.Services = services.BuildServiceProvider( );

            InitializeBlazorWebView( );
            RegisterHotKey( );
        }

        private void RegisterHotKey()
        {
            KeyPreview = true;
            KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.F11)
                {
                    ToggleFullScreen( );
                    e.Handled = true;
                }
            };
        }

        private void InitializeBlazorWebView()
        {
            BlazorWebView.HostPage = "wwwroot/index.html";
            BlazorWebView.RootComponents.Add<Routes>("#app");

            BlazorWebView.WebView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                BlazorWebView.WebView.CoreWebView2.AddHostObjectToScript("winFormHost", new WinFormInterop(this));
            };
        }

        public void ToggleFullScreen()
        {
            if (FormBorderStyle != FormBorderStyle.None)
            {
                // Save current state
                _previousBorderStyle = FormBorderStyle;
                _previousBounds = Bounds;
                _wasMaximized = WindowState == FormWindowState.Maximized;

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                FormBorderStyle = _previousBorderStyle;
                Bounds = _previousBounds;
                WindowState = _wasMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }

            BlazorWebView.WebView.ExecuteScriptAsync("onFullScreenChanged(" + (IsFullScreen ? "true" : "false") + ");");
        }
    }
}
