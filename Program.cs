using System;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace AIPaste;

static class Program
{
    private const string PipeName = "AIPasteSingleInstancePipe";
    private static Mutex? _mutex;
    private static Thread? _pipeServerThread;
    private static bool _keepRunning = true;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        
        // Try to get a single-instance lock using a mutex
        _mutex = new Mutex(true, "Global\\" + PipeName, out bool createdNew);
        
        if (!createdNew)
        {
            // Another instance is already running, send a message to it
            SendMessage("SHOW");
            return;
        }
        
        // Start the pipe server in a separate thread
        _pipeServerThread = new Thread(PipeServerThread);
        _pipeServerThread.IsBackground = true;
        _pipeServerThread.Start();
        
        // Create main form
        var mainForm = new MainForm(true);
        
        try
        {
            // Run the application
            Application.Run(mainForm);
        }
        finally
        {
            // Clean up resources
            _keepRunning = false;
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
    
    private static void PipeServerThread()
    {
        while (_keepRunning)
        {
            try
            {
                using (var pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In))
                {
                    // Wait for connection from another instance
                    pipeServer.WaitForConnection();
                    
                    // Read the message
                    using (var reader = new StreamReader(pipeServer))
                    {
                        string message = reader.ReadLine() ?? string.Empty;
                        
                        // Handle the message
                        if (message == "SHOW")
                        {
                            // Use Invoke to access UI thread
                            var form = Application.OpenForms[0];
                            if (form != null)
                            {
                                form.Invoke(new Action(() => {
                                    if (form is MainForm mainForm)
                                    {
                                        mainForm.OpenClipboardPopupPublic();
                                    }
                                }));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Pipe was broken or another error occurred
                // Wait a bit before trying again
                Thread.Sleep(100);
            }
        }
    }
    
    private static void SendMessage(string message)
    {
        try
        {
            using (var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                // Connect to the pipe server
                pipeClient.Connect(1000); // 1 second timeout
                
                // Send the message
                using (var writer = new StreamWriter(pipeClient) { AutoFlush = true })
                {
                    writer.WriteLine(message);
                }
            }
        }
        catch (Exception)
        {
            // Failed to communicate with the existing instance
            // The existing instance might have closed or is not responding
        }
    }
}