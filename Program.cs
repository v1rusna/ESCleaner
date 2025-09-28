using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESC
{
    public record Result(bool IsError, string[] Errors);

    internal class Settings
    {
        public string RenPyVersion { get; set; } = "7.4.11";
        public string Path { get; set; } =
            @"C:\Program Files (x86)\Steam\steamapps\common\Everlasting Summer";
        public bool FileOptimize { get; set; } = true;

        [JsonIgnore]
        public bool RemoveFilters { get; set; }

        public IReadOnlyList<string> DeleteLanguages { get; init; } =
            [
                "chinese","english","french","german","italian",
                "portuguese","spanish","turkish"
            ];

        public void Save(string file = "settings.json") =>
            JsonFileHelper.Write(file, this);

        public void Print()
        {
            Program.Print($"Path: {Path}");
            Program.Print($"FileOptimize: {FileOptimize}");
            Program.Print("DeleteLanguages:");
            foreach (var lang in DeleteLanguages)
                Program.Print($" - {lang}");
        }
    }

    static class JsonFileHelper
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        public static void Write<T>(string path, T obj)
        {
            string json = JsonSerializer.Serialize(obj, Options);
            File.WriteAllText(path, json);
        }

        public static T? Read<T>(string path)
        {
            if (!File.Exists(path)) return default;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }

    class Program
    {
        static readonly public string ExeDir = AppContext.BaseDirectory;

        static void Main(string[] args)
        {
            Console.Clear();
            PrintInfo();

            Result result = FileSystem.IsExistsFiles();

            if (result.IsError)
            {
                Print("The necessary files are not present:", 1);
                foreach (var item in result.Errors)
                {
                    Print(item, 1);
                }
                Console.ReadKey(true);
                return;
            }

            string settingsPath = Path.Combine(ExeDir, "settings.json");
            Settings settings = JsonFileHelper.Read<Settings>(settingsPath)
                                ?? new Settings();

            ParseArgs(args, settings);

            if (FileSystem.Run(settings))
                Print("Operation completed successfully.", 32);
            else
                Print("Operation failed.", 31);

            Console.ReadKey(true);
        }

        private static void ParseArgs(string[] args, Settings settings)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.Equals("-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    FileSystem.CheckPath(args[++i], settings);
                }
                else if (arg.Equals("-help", StringComparison.OrdinalIgnoreCase))
                {
                    ShowHelp();
                    Console.ReadKey(true);
                    Environment.Exit(0);
                }
                else if (arg.StartsWith("-fileOpt:", StringComparison.OrdinalIgnoreCase))
                {
                    HandleFileOptArg(arg, settings);
                }
                else if (arg.StartsWith("-RenPy=", StringComparison.OrdinalIgnoreCase))
                {
                    string version = arg[7..];
                    settings.RenPyVersion = version;
                    Print($"RenPy version set to {version}");
                }
                else if (arg.Equals("-nofilters", StringComparison.OrdinalIgnoreCase))
                {
                    settings.RemoveFilters = true;
                    Print("Filters will be disabled during initialization");
                }
                else if (arg.Equals("-openSettings", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(ExeDir, "settings.json"),
                        UseShellExecute = true
                    });
                    Print("Open settings file");
                    Environment.Exit(0);
                }
                else
                {
                    Print($"Unknown argument: {arg}");
                    Console.ReadKey(true);
                    Environment.Exit(1);
                }
            }
        }

        private static void HandleFileOptArg(string arg, Settings settings)
        {
            string valuePart = arg[9..]; // после "-fileOpt:"
            bool save = valuePart.EndsWith(":save", StringComparison.OrdinalIgnoreCase);
            string flag = save ? valuePart[..^5] : valuePart;

            if (bool.TryParse(flag, out bool imgOpt))
            {
                settings.FileOptimize = imgOpt;
                if (save) settings.Save(Path.Combine(ExeDir, "settings.json"));
                Print($"Image optimization set to {imgOpt}" + (save ? " and saved" : ""));
            }
            else
            {
                Print($"Invalid value for -fileOpt: {flag}");
            }
        }

        private static void ShowHelp()
        {
            Print("Usage:");
            Print(" -path <path>                                Set path");
            Print(" -fileOpt:true / -fileOpt:false              Enable/disable file optimization");
            Print(" -fileOpt:true:save / -fileOpt:false:save    ...and save settings");
            Print(" -RenPy=<version>                            Set RenPy version");
            Print(" -nofilters                                  Removing filters");
            Print(" -openSettings                               Opens the settings file");
            Print(" -help                                       Show this help message");
        }

        public static void Print(string message, byte? ANSIcode = null, string end = "\n")
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (ANSIcode.HasValue)
                message = $"\u001b[38;5;{ANSIcode}m{message}\u001b[0m";
            Console.Write(message + end);
        }

        private static void PrintInfo()
        {
            int width = Console.WindowWidth;
            Console.SetCursorPosition(width - "v1rus team".Length - 1, 0);
            Print("v1rus team", 165, "");
            Console.SetCursorPosition(width - "28.09.2025".Length - 1, 1);
            Print("28.09.2025", 87, "");
            Console.SetCursorPosition(0, 0);
        }
    }

    static class FileSystem
    {
        public static Result IsExistsFiles()
        {
            static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

            List<string> errors = [];
            string optimizedDir = Path.Combine(Program.ExeDir, "optimized");

            string[] check = [
                Path.Combine(Program.ExeDir, "settings.json"),
                optimizedDir,
                Path.Combine(optimizedDir, "data.json"),
                Path.Combine(optimizedDir, "optimize.rpy")
            ];

            foreach (string item in check)
            {
                if (!Exists(item)) errors.Add(item);
            }

            var filesMap = JsonFileHelper.Read<Dictionary<string, string>>(Path.Combine(optimizedDir, "data.json"));
            if (filesMap == null) errors.Add(Path.Combine(optimizedDir, "data.json"));

            string[] files = [];
            if (Directory.Exists(optimizedDir))
                files = Directory.GetFiles(optimizedDir);

            if (filesMap != null)
            {
                foreach (var item in filesMap)
                {
                    string expectedFile = Path.Combine(optimizedDir, item.Key + ".optimize");
                    if (!files.Contains(expectedFile))
                        errors.Add(expectedFile);
                }
            }

            return new Result(errors.Count > 0, errors.ToArray());
        }

        public static void CheckPath(string path, Settings settings)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) || !path.Contains("Everlasting Summer"))
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            settings.Path = path;
            settings.Save(Path.Combine(Program.ExeDir, "settings.json"));
        }

        public static bool Run(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Path) || !Directory.Exists(settings.Path))
            {
                Program.Print($"Invalid path: {settings.Path}", 31);
                return false;
            }

            FileOptimize(settings);
            return DeleteLanguageDirs(settings);
        }

        private static void FileOptimize(Settings settings)
        {
            if (!settings.FileOptimize) return;

            string mainGamePath = Path.Combine(settings.Path, "game");
            CopyOptimizedFiles(mainGamePath);

            string optimizePath = Path.Combine(Program.ExeDir, "optimized", "optimize.rpy");
            string[] lines = File.ReadAllLines(optimizePath);
            lines[1] = $"    $ persistent.nofilters = {(settings.RemoveFilters ? "True" : "False")}";
            File.WriteAllLines(Path.Combine(mainGamePath, "optimize.rpy"), lines);
        }

        private static void CopyOptimizedFiles(string mainGamePath)
        {
            string optimizedDir = Path.Combine(Program.ExeDir, "optimized");
            var filesMap = JsonFileHelper.Read<Dictionary<string, string>>(Path.Combine(optimizedDir, "data.json"));
            if (filesMap == null) return;

            foreach (var kvp in filesMap)
            {
                string fileName = kvp.Key;
                string relativePath = Path.Combine(mainGamePath, kvp.Value);
                string source = Path.Combine(optimizedDir, fileName + ".optimize");
                string dest = Path.Combine(relativePath, fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest, overwrite: true);
                Program.Print($"File {fileName} successfully replaced", 34);
            }
        }

        private static bool DeleteLanguageDirs(Settings settings)
        {
            string langDir = Path.Combine(settings.Path, "game", "tl");
            if (!Directory.Exists(langDir))
            {
                Program.Print($"Language directory not found: {langDir}", 31);
                return false;
            }

            foreach (var lang in settings.DeleteLanguages)
            {
                string dirPath = Path.Combine(langDir, lang);
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                    Program.Print($"Deleted language directory: {dirPath}", 34);
                }
                else
                {
                    Program.Print($"Language directory not found: {dirPath}", 31);
                }
            }
            return true;
        }
    }
}
