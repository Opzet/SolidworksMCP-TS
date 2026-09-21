using System.Reflection;
using System.Runtime.ExceptionServices;

namespace AutoWorks;

internal static class Program
{
    static Mutex mutex = new Mutex(false, "AutoWorks");

    [STAThread]
    private static void Main()
    {

        // If (!Debugger.IsAttached())
        //{
        //if (mutex.WaitOne(TimeSpan.Zero, true) == false)
        //{
        //    // send our Win32 message to make the currently running instance
        //    // jump on top of all the other windows
        //    //MessageBox.Show($"ALREADY RUNNING- CLOSING");

        //    NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
        //    mutex.ReleaseMutex();
        //    //Switch to active app
        //    return;
        //}
        //}

        // Do regional settings as en-au MMGS
        System.Globalization.CultureInfo customCulture = new System.Globalization.CultureInfo("en-AU");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = customCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = customCulture;

        ApplicationConfiguration.Initialize();
        // Show splash screen immediately
        FrmSplashScreen splash = new FrmSplashScreen( );
        
        splash.Show( );
        splash.UpdateStatus($"Initializing application for {Environment.MachineName}\\{Environment.UserName}...");
        
        Application.DoEvents( );
        Thread.Sleep(500);
        splash.UpdateStatus("Initializing hosting environment...");
        
        Application.DoEvents( );
        Thread.Sleep(500);
        var versionInfo = GetAppVersion( );//


        if (versionInfo != null)
        {
            splash.UpdateStatus($"Version: {versionInfo.ToString( )}");
        }
        else
        {
            splash.UpdateStatus("Failed to retrieve version.");
        }

        Thread.Sleep(500);
        splash.UpdateStatus("Loading main window...");

        Application.Run(new MainForm());
    }

    private static Version GetAppVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly( );
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>( );
            if (versionAttribute != null)
            {
                return new Version(versionAttribute.InformationalVersion);
            }
            else
            {
                return assembly.GetName( ).Version ?? new Version(1, 0, 0, 0);
            }
        }
        catch
        {
            return new Version(1, 0, 0, 0); // Default version if something goes wrong
        }
    }   

    private static void FirstChanceExceptionHandler(object? sender, FirstChanceExceptionEventArgs e)
    {
        string body = "UnhandledException\n" + e.ToString( ) + "\n";
        try
        {
            Exception ex = (Exception) e.Exception;
            body += "StackTrace: " + ex.StackTrace.ToString( ) + "\n";

            // do nothing to silently swallow error, or try something else...
           // ClientEvent.LogException(body, ex);//body, "UnhandledException - Stacktrace");

            MessageBox.Show(body, "App Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        }
        catch
        {
            // do nothing to silently swallow error, or try something else...
            //ClientEvent.LogException(body);
        }

        //UnhandledException method prevents the Windows crash notification from showing up:
        // RestartApplication();

    }

    private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e?.ExceptionObject == null)
            return;

        string body = $"UnhandledException (IsTerminating: {e.IsTerminating})\n";
        Exception? ex = null;

        try
        {
            ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                body += $"Type: {ex.GetType( ).FullName}\nMessage: {ex.Message}\n";
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    body += $"StackTrace: {ex.StackTrace}\n";
                }

                if (ex.InnerException != null)
                {
                    body += $"InnerException: {ex.InnerException.Message}\n";
                }

              //  ClientEvent.LogException(body, ex);
            }
            else
            {
                body += $"Non-Exception object: {e.ExceptionObject}\n";
               // ClientEvent.LogException(body);
            }
        }
        catch (Exception logEx)
        {
            try
            {
               // ClientEvent.LogException($"{body}\nLogging failed: {logEx.Message}");

                MessageBox.Show(body, "App Crash - Reported", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"Critical: Failed to log unhandled exception");
            }
        }

        //// Only restart if the application is terminating and we haven't exceeded restart limit
        //if (e.IsTerminating)
        //{
        //    RestartApplication();
        //}
    }

}
