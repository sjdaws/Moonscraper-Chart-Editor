using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class BuildRelease : Build
{
    private string packageDescription = "A song editor for Guitar Hero style rhythm games made in Unity";
    private string packageName = "moonscraper-chart-editor";
    private string packagePrefix = "msce";

    private static string applicationURL = "https://github.com/FireFox2000000/Moonscraper-Chart-Editor";
    private string applicationReleaseURL = applicationURL + "/issues";
    private string applicationSupportURL = applicationURL + "/releases";

    private string compressionProgramPath = null;
    private string dpkgProgramPath = null;
    private string installerProgramPath = null;
    private string rpmProgramPath = null;

    public BuildRelease() : base()
    {
        compressionProgramPath = IO.FindExecutablePath(new string[] { "7z.exe", "7z", "7zz" }, new string[] { }, "Select 7-zip executable");
        if (string.IsNullOrEmpty(compressionProgramPath))
        {
            throw new PrerequisiteException("7-Zip not found, cannot proceed with compressed build.");
        }

        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            installerProgramPath = IO.FindExecutablePath(new string[] { "ISCC.exe" }, new string[] { "ISCC", "ISCC_PATH" }, "Select Inno Setup executable");
            if (string.IsNullOrEmpty(installerProgramPath))
            {
                throw new PrerequisiteException("Inno Setup not found, cannot proceed with building windows installer.");
            }
        }

        if (Application.platform != RuntimePlatform.WindowsEditor)
        {
            dpkgProgramPath = IO.FindExecutablePath(new string[] { "dpkg-deb" }, new string[] { }, "Select dpkg executable");
            if (string.IsNullOrEmpty(dpkgProgramPath))
            {
                throw new PrerequisiteException("dpkg not found, cannot proceed with building debian installer.");
            }

            rpmProgramPath = IO.FindExecutablePath(new string[] { "rpm" }, new string[] { }, "Select rpm executable");
            if (string.IsNullOrEmpty(rpmProgramPath))
            {
                throw new PrerequisiteException("rpm not found, cannot proceed with building redhat installer.");
            }
        }
    }

    public override string For(BuildTarget target)
    {
        string targetPath = base.For(target);

        compress(target, targetPath);
        package(target, targetPath);

        return targetPath;
    }

    private void compress(string source, string destination)
    {
        using (var process = new System.Diagnostics.Process())
        {
            string path = Path.GetFullPath(Path.Combine(buildPath, destination));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            process.StartInfo.FileName = compressionProgramPath;
            process.StartInfo.WorkingDirectory = buildPath;
            process.StartInfo.Arguments = string.Format("a \"{0}\" \"{1}\"", destination, source);

            process.Start();

            process.WaitForExit();
        }
    }

    private void compress(BuildTarget target, string targetPath)
    {
        string filename = IO.GetTargetFolder(packagePrefix, target);

        switch (target)
        {
            case BuildTarget.StandaloneLinux64:
                compress(targetPath, filename + ".tar");
                compress(filename + ".tar", filename + ".tar.gz");

                string tarpath = Path.GetFullPath(Path.Combine(buildPath, filename + ".tar"));
                if (File.Exists(tarpath))
                {
                    File.Delete(tarpath);
                }
                break;

            default:
                compress(targetPath, filename + ".zip");
                break;
        }
    }

    private void package(BuildTarget target, string targetPath)
    {
        string platform = string.Empty;
        switch (target) {
            case BuildTarget.StandaloneLinux64:
                packageDPKG(targetPath);
                packageRPM(targetPath);
                break;

            case BuildTarget.StandaloneWindows:
                packageWindows("x86", targetPath);
                break;

            case BuildTarget.StandaloneWindows64:
                packageWindows("x64", targetPath);
                break;

            default:
                Debug.Log($"Installer requested for unsupported build target '{target}', skipping.");

                return;
        }
    }

    private void packageDPKG(string targetPath)
    {
        string version = Application.version + "-1";
        string dpkgPath = Path.Combine(buildPath, packageName + "_" + version);

        string optPath = Path.Combine(dpkgPath, "opt");
        string desktopPath = Path.Combine(dpkgPath, "usr", "local", "share", "applications");
        string debPath = Path.Combine(dpkgPath, "DEBIAN");

        IO.CreateDirectory(dpkgPath, true);
        IO.CreateDirectory(optPath, true);
        IO.CreateDirectory(desktopPath, true);
        IO.CreateDirectory(debPath, true);

        string controlPath = Path.Combine(debPath, "control");

        FileUtil.CopyFileOrDirectory(targetPath, Path.Combine(optPath, packageName));
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Shortcuts/" + packageName + ".desktop")), Path.Combine(desktopPath, Application.productName + ".desktop"));
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Scripts/" + packageName + ".dpkg")), controlPath);

        string text = File.ReadAllText(controlPath);
        text = text.Replace("#DESCRIPTION#", packageDescription);
        text = text.Replace("#NAME#", packageName);
        text = text.Replace("#PUBLISHER#", Application.companyName);
        text = text.Replace("#VERSION#", version);
        File.WriteAllText(controlPath, text);

        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
        {
            process.StartInfo.FileName = dpkgProgramPath;
            process.StartInfo.WorkingDirectory = buildPath;
            process.StartInfo.Arguments = "--build " + dpkgPath;

            process.Start();

            process.WaitForExit();
        }
    }

    private void packageRPM(string targetPath)
    {

    }

    private void packageWindows(string platform, string targetPath)
    {
        string installerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Installer"));

        string scriptPath = Path.Combine(installerPath, "Scripts/Windows.iss");
        if (Directory.Exists(installerPath) == false || File.Exists(scriptPath) == false)
        {
            Debug.Assert(false, "Installer script not found, unable to create installer.");
        }

        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
        {
            StringBuilder args = new StringBuilder();
            args.AppendFormat("/dAppName=\"{0}\" ", Application.productName);
            args.AppendFormat("/dAppPublisher=\"{0}\" ", Application.companyName);
            args.AppendFormat("/dAppReleaseURL=\"{0}\" ", applicationReleaseURL);
            args.AppendFormat("/dAppSupportURL=\"{0}\" ", applicationSupportURL);
            args.AppendFormat("/dAppURL=\"{0}\" ", applicationURL);
            args.AppendFormat("/dAppVersion=\"{0}\" ", Application.version);
            args.AppendFormat("/dPackagePrefix=\"{0}\" ", packagePrefix);
            args.AppendFormat("/dBuildPath=\"{0}\" ", buildPath);
            args.AppendFormat("/dPlatform={0} ", platform);
            args.AppendFormat("/dTargetPath=\"{0}\" ", targetPath);
            args.AppendFormat("\"{0}\"", scriptPath);

            process.StartInfo.FileName = installerProgramPath;
            process.StartInfo.WorkingDirectory = installerPath;
            process.StartInfo.Arguments = args.ToString().Trim();

            process.Start();

            process.WaitForExit();
        }
    }
}
