using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Build {
    protected string buildPath = null;

    public Build()
    {
        buildPath = IO.FindSavePath(new string[0], new string[] { "BUILD_PATH" }, "Select build directory");
        if (string.IsNullOrEmpty(buildPath))
        {
            throw new BuildException("Build cancelled.");
        }

        if (File.Exists(buildPath))
        {
            throw new PrerequisiteException("Selected build directory is actually a file, aborting build.");
        }
    }

    public virtual string For(BuildTarget target)
    {
        string extension = null;
        string targetPath = Path.Combine(buildPath, IO.GetTargetFolder(Application.productName, target));

        switch (target)
        {
            case BuildTarget.StandaloneLinux64:
                extension = string.Empty;
                break;

            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                extension = ".exe";
                break;

            default:
                throw new BuildException($"Build target '{target}' is not supported.");
        }

        Debug.Log("Creating build directory: " + buildPath);

        IO.CreateDirectory(buildPath, false);

        Debug.Log(string.Format("Creating build directory for {0}: {1}", target.ToString(), targetPath));

        IO.CreateDirectory(targetPath, true);

        Debug.Log("Building target: " + target.ToString());

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.locationPathName = Path.Combine(targetPath, Application.productName + extension);
        options.options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode;
        options.scenes = EditorBuildSettings.scenes
            .Where((scene) => scene.enabled)
            .Select((scene) => scene.path)
            .ToArray();
        options.target = target;

        // Build player
        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Assert(false, "Build failed.");
        }

        copyResources(targetPath);

        Debug.Log("Build target complete!");

        return targetPath;
    }

    private static void copyResources(string path)
    {
        if (Directory.Exists("Assets/Custom Resources"))
        {
            FileUtil.CopyFileOrDirectory("Assets/Custom Resources", Path.Combine(path, "Custom Resources"));
        }

        if (Directory.Exists("Assets/Documentation"))
        {
            FileUtil.CopyFileOrDirectory("Assets/Documentation", Path.Combine(path, "Documentation"));
        }

        string extraFiles = "Assets/ExtraBuildFiles";
        if (Directory.Exists(extraFiles))
        {
            foreach (string filepath in Directory.GetFiles(extraFiles))
            {
                FileUtil.CopyFileOrDirectory(filepath, Path.Combine(path, Path.GetFileName(filepath)));
            }

            foreach (string filepath in Directory.GetDirectories(extraFiles))
            {
                string directory = filepath.Remove(0, extraFiles.Count() + 1);
                FileUtil.CopyFileOrDirectory(filepath, Path.Combine(path, directory));
            }
        }

        string licenseFile = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LICENSE"));
        if (File.Exists(licenseFile))
        {
            FileUtil.CopyFileOrDirectory(licenseFile, Path.Combine(path, "LICENSE"));
        }

        foreach (string file in Directory.GetFiles(path, "*.meta", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }
    }

    protected class IO {
        public static void CreateDirectory(string path, bool clean = false)
        {
            if (Directory.Exists(path) && clean == false)
            {
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        public static string FindExecutablePath(string[] names, string[] envs, string prompt)
        {
            string file = null;

            file = findInPath(names);
            if (string.IsNullOrEmpty(file))
            {
                file = findInEnv(envs, true);
            }

            if (string.IsNullOrEmpty(file) == false)
            {
                return file;
            }

            // Prompt user
            return EditorUtility.OpenFilePanel(prompt, "", "");
        }

        public static string EmptyPath(params string[] paths)
        {
            string path = Path.Combine(paths);

            Remove(path);

            return path;
        }

        public static string FindSavePath(string[] names, string[] envs, string prompt)
        {
            string directory = null;

            directory = findInEnv(envs, false);

            if (string.IsNullOrEmpty(directory) == false)
            {
                return directory;
            }

            // Prompt user
            return EditorUtility.SaveFolderPanel(prompt, "", "");
        }

        public static string GetTargetFolder(string prefix, BuildTarget target)
        {
            string name = string.Format("{0} v{1}", prefix, Application.version);

            if (string.IsNullOrEmpty(Globals.applicationBranchName) == false)
            {
                name += string.Format(" {0}", Globals.applicationBranchName);
            }

            string architecture = null;
            switch (target)
            {
                case BuildTarget.StandaloneLinux64:
                    architecture = "Linux x64";
                    break;

                case BuildTarget.StandaloneWindows:
                    architecture = "Windows x86";
                    break;

                case BuildTarget.StandaloneWindows64:
                    architecture = "Windows x64";
                    break;

                default:
                    architecture = target.ToString();
                    break;
            }
            name += string.Format(" {0}", architecture);

            return name;
        }

        public static void Remove(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        // Search env for a list of names
        private static string findInEnv(string[] names, bool exists = true)
        {
            // Iterate envs
            foreach (string name in names)
            {
                string fullpath = System.Environment.GetEnvironmentVariable(name);
                if (validatePath(fullpath, exists))
                {
                    return fullpath;
                }
            }

            return string.Empty;
        }

        // Search $PATH/%PATH% for a list of names and return
        private static string findInPath(string[] names)
        {
            string command = Application.platform == RuntimePlatform.WindowsEditor ? "where.exe" : "which";

            foreach (string name in names)
            {
                string output = Process.Run("", command, name);

                string[] paths = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if (paths.Length > 0)
                {
                    if (validatePath(paths[0], true))
                    {
                        return paths[0];
                    }
                }
            }

            return string.Empty;
        }

        private static bool validatePath(string path, bool exists)
        {
            return string.IsNullOrEmpty(path) == false && (exists == false || File.Exists(path));
        }
    }

    [Serializable]
    protected class PrerequisiteException : Exception
    {
        public PrerequisiteException(string message) : base(message) { }
    }

    protected class Process
    {
        public static string Run(string directory, string command, string arguments)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.WorkingDirectory = directory;
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return output;
        }
    }

    [Serializable]
    private class BuildException : Exception
    {
        public BuildException(string message) : base(message) { }
    }
}

