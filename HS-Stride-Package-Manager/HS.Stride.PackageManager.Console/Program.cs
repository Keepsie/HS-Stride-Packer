// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using HS.Stride.Packer.Utilities;

namespace HS.Stride.Packer.Console;

internal class Program
{
    private const string APP_NAME = "HS Stride Packer";
    private const string VERSION = "0.8.0";
    
    static async Task Main(string[] args)
    {
        // Setup the console helper with ASCII art
        Helper.SetupHelper(
@"
╔═══════════════════════════════════════════════════════════════════════╗
║                                                                       ║
║  ██╗  ██╗███████╗    ███████╗████████╗██████╗ ██╗██████╗ ███████╗     ║
║  ██║  ██║██╔════╝    ██╔════╝╚══██╔══╝██╔══██╗██║██╔══██╗██╔════╝     ║
║  ███████║███████╗    ███████╗   ██║   ██████╔╝██║██║  ██║█████╗       ║
║  ██╔══██║╚════██║    ╚════██║   ██║   ██╔══██╗██║██║  ██║██╔══╝       ║
║  ██║  ██║███████║    ███████║   ██║   ██║  ██║██║██████╔╝███████╗     ║
║  ╚═╝  ╚═╝╚══════╝    ╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═╝╚═════╝ ╚══════╝     ║
║                                                                       ║
║                       Packer v" + VERSION + @"                        ║
║           © 2025 Happenstance Games LLC - All Rights Reserved         ║
║                                                                       ║
╚═══════════════════════════════════════════════════════════════════════╝
",
            APP_NAME,
            VERSION
        );
        
        Helper.Clear();
        Helper.ShowTitle();
        
        Helper.ShowInfo("Interactive Package Manager - Create, Import, and Manage .stridepackage files");
        Helper.ShowInfo("Type 'help' for available commands or 'export' to start creating a package");
        Helper.AddSpace();
        
        Helper.ShowWarning("IMPORTANT: Close Stride GameStudio before importing/exporting packages");
        Helper.ShowWarning("⚠️ Registry Content Warning");
        Helper.ShowWarning("Registry packages are hosted by other users and not controlled by HS Stride Packer.");
        Helper.ShowWarning("By continuing, you use these services at your own risk. See LICENSE.txt for full disclaimers.");
        Helper.AddSpace();

        try
        {
            // Initialize the command handler
            var commandHandler = new StrideCommandHandler();
            
            // Simple command loop
            var running = true;
            while (running)
            {
                try
                {
                    var input = Helper.ReadString("packer> ", allowEmpty: true);
                    
                    if (string.IsNullOrWhiteSpace(input))
                        continue;
                        
                    if (input.Trim().ToLower() == "exit")
                    {
                        if (Helper.ReadBool("Are you sure you want to exit?"))
                            running = false;
                        continue;
                    }
                    
                    await commandHandler.HandleInput(input.Trim());
                }
                catch (Exception ex)
                {
                    Helper.ShowError($"An error occurred: {ex.Message}");
                    Helper.AddSpace();
                }
            }
        }
        catch (Exception ex)
        {
            Helper.AddSpace();
            Helper.ShowError($"An unexpected error occurred: {ex.Message}");
            if (ex.InnerException != null)
            {
                Helper.ShowError($"Details: {ex.InnerException.Message}");
            }
            Helper.AddSpace();
            Helper.Exit();
        }
    }
}