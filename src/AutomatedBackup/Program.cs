// Program.cs - Entry point for the Automated Backup application
// This file initializes the TUI (Text User Interface) and starts the application

using Terminal.Gui;           // Library for creating terminal/console user interfaces
using AutomatedBackup.UI;     // Our custom UI components

namespace AutomatedBackup;

class Program
{
    /// <summary>
    /// Main entry point - this method runs when you start the application
    /// </summary>
    static void Main(string[] args)
    {
        // Initialize the Terminal.Gui library
        // This sets up the console for drawing UI elements (windows, buttons, etc.)
        Application.Init();

        // Create and run our main application window
        // The 'using' statement ensures resources are properly cleaned up when done
        using var mainView = new MainView();
        
        // Run the application - this starts the event loop
        // The app will keep running until the user closes it (e.g., pressing Ctrl+Q)
        Application.Run(mainView);

        // Clean up Terminal.Gui resources when the application exits
        Application.Shutdown();
    }
}
