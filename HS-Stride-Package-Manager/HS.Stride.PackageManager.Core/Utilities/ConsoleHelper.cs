// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Packer.Utilities
{
    public static class Helper
    {
        // Constants for common formatting
        private const int DEFAULT_SPACING = 1;
        private const int DEFAULT_MAX_ATTEMPTS = 3;

        // App customization
        public static string AppName { get; set; } = "App Name";
        public static string Version { get; set; } = "0.0.0";
        public static string Title { get; set; } = $"=============== {AppName} v{Version} ===============";

        public static void SetupHelper(string title, string appName, string version)
        {
            Title = title;
            AppName = appName;
            Version = version;
        }

        // Core Output Methods
        public static void Write(string message)
        {
            Console.WriteLine(message);
        }

        public static void Write(string message, ConsoleColor color)
        {
            var currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = currentColor;
        }

        // Display Methods
        public static void ShowTitle()
        {
            Write(Title);
            AddSpace();
        }

        public static void ShowError(string message)
        {
            Write($"Error: {message}", ConsoleColor.Red);
        }

        public static void ShowSuccess(string message)
        {
            Write(message, ConsoleColor.Green);
        }

        public static void ShowWarning(string message)
        {
            Write($"Warning: {message}", ConsoleColor.Yellow);
        }

        public static void ShowInfo(string message)
        {
            Write(message, ConsoleColor.Cyan);
        }

        // Input Methods
        public static string ReadString(string prompt, bool allowEmpty = false, int maxAttempts = DEFAULT_MAX_ATTEMPTS)
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Console.Write($"{prompt}: ");
                var input = Console.ReadLine()?.Trim();

                if (allowEmpty || !string.IsNullOrWhiteSpace(input))
                {
                    return input ?? string.Empty;
                }

                ShowError("Invalid input. Please enter a non-empty value.");
                AddSpace();
            }
            throw new InvalidOperationException($"Failed to get valid input after {maxAttempts} attempts.");
        }

        public static int ReadInt(string prompt, int maxAttempts = DEFAULT_MAX_ATTEMPTS)
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Console.Write($"{prompt}: ");
                if (int.TryParse(Console.ReadLine(), out int result))
                {
                    return result;
                }
                ShowError($"Invalid input. Please enter a number.");
                AddSpace();
            }
            throw new InvalidOperationException($"Failed to get valid input after {maxAttempts} attempts.");
        }

        public static bool ReadBool(string prompt, int maxAttempts = DEFAULT_MAX_ATTEMPTS)
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Console.Write($"{prompt} (y/n): ");
                var input = Console.ReadLine()?.Trim().ToLower();

                switch (input)
                {
                    case "y":
                    case "yes":
                        return true;
                    case "n":
                    case "no":
                        return false;
                }

                ShowError("Please enter 'y' for yes or 'n' for no.");
                AddSpace();
            }
            throw new InvalidOperationException($"Failed to get valid input after {maxAttempts} attempts.");
        }

        // Menu Methods
        public static void ShowMenu(string[] options, string header = "Menu Options")
        {
            ShowInfo(header);
            AddSpace();
            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {options[i]}");
            }
            AddSpace();
        }

        public static void ShowList<T>(IEnumerable<T> items, string header = null)
        {
            if (!string.IsNullOrEmpty(header))
            {
                ShowInfo(header);
                AddSpace();
            }

            int count = 1;
            foreach (var item in items)
            {
                Console.WriteLine($"{count}. {item}");
                count++;
            }
            AddSpace();
        }

        // Utility Methods
        public static void AddSpace(int count = DEFAULT_SPACING)
        {
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine();
            }
        }

        public static void Clear()
        {
            Console.Clear();
        }

        public static void PauseAndContinue()
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        public static void Exit(string message = "Press any key to exit...")
        {
            Console.WriteLine(message);
            Console.ReadKey(true);
            Environment.Exit(0);
        }
    }
}


