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

        private void NotifyBlazor()
        {
            _mainForm.BlazorWebView.WebView.ExecuteScriptAsync(
                $"onFullScreenChanged({_mainForm.IsFullScreen.ToString( ).ToLower( )});"
            );
        }
    }
}