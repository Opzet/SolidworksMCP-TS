using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoWorks
{
    [ComVisible(true)]
    public class WinFormInterop(MainForm form)
    {
        private readonly MainForm _mainForm = form;

        public void ToggleFullScreenFromJs()
        {
            _mainForm.ToggleFullScreen( );
            NotifyBlazor( );
        }

        public void ExitFullScreenFromJs()
        {
            if (_mainForm.IsFullScreen)
            {
                _mainForm.ToggleFullScreen( );
                NotifyBlazor( );
            }
        }

        public bool IsFullScreen()
        {
            return _mainForm.IsFullScreen;
        }

        public void OpenPathInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var target = path;

            if (File.Exists(path))
            {
                target = Path.GetDirectoryName(path) ?? path;
            }

            if (!Directory.Exists(target))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true,
            };

            Process.Start(psi);
        }

        private void NotifyBlazor()
        {
            _mainForm.BlazorWebView.WebView.ExecuteScriptAsync(
                $"onFullScreenChanged({_mainForm.IsFullScreen.ToString( ).ToLower( )});"
            );
        }
    }
}
