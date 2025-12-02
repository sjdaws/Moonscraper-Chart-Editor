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
        compressionProgramPath = IO.FindExecutablePath(new string[] { "7z.exe", "7z", "7zz" }, new string[] { "7-zip" }, "Select 7-zip executable");
        if (string.IsNullOrEmpty(compressionProgramPath))
        {
            throw new PrerequisiteException("7-Zip not found, cannot proceed with compressed build.");
        }

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        installerProgramPath = IO.FindExecutablePath(new string[] { "iscc.exe", "ISCC.exe" }, new string[] { "ISCC" }, "Select Inno Setup executable");
        if (string.IsNullOrEmpty(installerProgramPath))
        {
            Debug.Log("Inno Setup not found, Windows installer will not be built.");
        }
#elif (UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
        dpkgProgramPath = IO.FindExecutablePath(new string[] { "dpkg-deb" }, new string[] { }, "Select dpkg executable");
        if (string.IsNullOrEmpty(dpkgProgramPath))
        {
            Debug.Log("dpkg not found, Debian package will not be built.");
        }

        rpmProgramPath = IO.FindExecutablePath(new string[] { "rpmbuild" }, new string[] { }, "Select rpm executable");
        if (string.IsNullOrEmpty(rpmProgramPath))
        {
            Debug.Log("rpmbuild not found, Red Hat package can not be built.");
        }
#endif
    }

    public override string For(BuildTarget target)
    {
        string targetPath = base.For(target);

        package(target, targetPath);
        compress(target, targetPath);

        return targetPath;
    }

    private void compress(string source, string destination)
    {
        string path = IO.EmptyPath(buildPath, destination);

        Process.Run(buildPath, compressionProgramPath, string.Format("a \"{0}\" \"{1}\"", destination, source));

        // Clean up source files
        IO.Remove(source);
    }

    private void compress(BuildTarget target, string targetPath)
    {
        string filename = IO.GetTargetFolder(packagePrefix.ToUpper(), target);

        switch (target)
        {
            case BuildTarget.StandaloneLinux64:
                string tarPath = Path.Combine(buildPath, filename + ".tar");
                compress(targetPath, tarPath);
                compress(tarPath, filename + ".tar.gz");
                break;

            default:
                compress(targetPath, Path.Combine(buildPath, filename + ".zip"));
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
        string dpkgPath = IO.EmptyPath(buildPath, packagePrefix + "_" + version + ".x86_64");

        string optPath = Path.Combine(dpkgPath, "opt");
        string desktopPath = Path.Combine(dpkgPath, "usr", "local", "share", "applications");
        string debianPath = Path.Combine(dpkgPath, "DEBIAN");

        IO.CreateDirectory(dpkgPath, true);
        IO.CreateDirectory(optPath, true);
        IO.CreateDirectory(desktopPath, true);
        IO.CreateDirectory(debianPath, true);

        FileUtil.CopyFileOrDirectory(targetPath, Path.Combine(optPath, packageName));
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Shortcuts/" + packageName + ".desktop")), Path.Combine(desktopPath, Application.productName + ".desktop"));

        string controlPath = Path.Combine(debianPath, "control");
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Scripts/debian.dpkg")), controlPath);
        string control = File.ReadAllText(controlPath);
        control = control.Replace("#DESCRIPTION#", packageDescription);
        control = control.Replace("#PACKAGE#", packageName);
        control = control.Replace("#PUBLISHER#", Application.companyName);
        control = control.Replace("#VERSION#", version);
        File.WriteAllText(controlPath, control);

        string postinstPath = Path.Combine(debianPath, "postinst");
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Scripts/debian.postinst")), postinstPath);
        string postinst = File.ReadAllText(postinstPath);
        postinst = postinst.Replace("#NAME#", Application.productName);
        postinst = postinst.Replace("#PACKAGE#", packageName);
        File.WriteAllText(postinstPath, postinst);

        string debPath = IO.EmptyPath(dpkgPath + ".deb");

        Process.Run(buildPath, dpkgProgramPath, "--build " + dpkgPath);

        IO.Remove(dpkgPath);
    }

    private void packageRPM(string targetPath)
    {
        string version = Application.version + "-1";
        string rpmbuildPath = IO.EmptyPath(buildPath, packagePrefix + "_" + version + ".x86_64");

        string optPath = Path.Combine(rpmbuildPath, "opt");
        string desktopPath = Path.Combine(rpmbuildPath, "usr", "local", "share", "applications");

        IO.CreateDirectory(rpmbuildPath, true);
        IO.CreateDirectory(optPath, true);
        IO.CreateDirectory(desktopPath, true);

        FileUtil.CopyFileOrDirectory(targetPath, Path.Combine(optPath, packageName));
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Shortcuts/" + packageName + ".desktop")), Path.Combine(desktopPath, Application.productName + ".desktop"));

        string rpmSpecPath = IO.EmptyPath(buildPath, "rhel.spec");
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../Installer/Scripts/rhel.spec")), rpmSpecPath);
        string spec = File.ReadAllText(rpmSpecPath);
        spec = spec.Replace("#DESCRIPTION#", packageDescription);
        spec = spec.Replace("#NAME#", Application.productName);
        spec = spec.Replace("#NAME_PATH#", Application.productName.Replace(" ", "?"));
        spec = spec.Replace("#PACKAGE#", packageName);
        spec = spec.Replace("#PACKAGE_PATH#", packageName.Replace(" ", "\\ "));
        spec = spec.Replace("#PUBLISHER#", Application.companyName);
        spec = spec.Replace("#SUPPORT_URL#", applicationSupportURL);
        spec = spec.Replace("#URL#", applicationURL);
        spec = spec.Replace("#VERSION#", Application.version);
        File.WriteAllText(rpmSpecPath, spec);

        Process.Run(buildPath, rpmProgramPath, "--build-in-place --buildroot " + rpmbuildPath + " -bb " + rpmSpecPath);

        string rpmPath = IO.EmptyPath(buildPath, packagePrefix + "_" + version + ".x86_64.rpm");

        string rpmPackagePath = Path.Combine(buildPath, "x86_64");
        FileUtil.CopyFileOrDirectory(Path.Combine(rpmPackagePath, packageName + "-" + version + ".x86_64.rpm"), rpmPath);

        IO.Remove(rpmbuildPath);
        IO.Remove(rpmPackagePath);
        IO.Remove(rpmSpecPath);
    }

    private void packageWindows(string platform, string targetPath)
    {
        string installerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Installer"));

        string scriptPath = Path.Combine(installerPath, "Scripts/windows.iss");
        if (Directory.Exists(installerPath) == false || File.Exists(scriptPath) == false)
        {
            Debug.Assert(false, "Installer script not found, unable to create installer.");
        }

        StringBuilder args = new StringBuilder();
        args.AppendFormat("/dAppName=\"{0}\" ", Application.productName);
        args.AppendFormat("/dAppPublisher=\"{0}\" ", Application.companyName);
        args.AppendFormat("/dAppReleaseURL=\"{0}\" ", applicationReleaseURL);
        args.AppendFormat("/dAppSupportURL=\"{0}\" ", applicationSupportURL);
        args.AppendFormat("/dAppURL=\"{0}\" ", applicationURL);
        args.AppendFormat("/dAppVersion=\"{0}\" ", Application.version);
        args.AppendFormat("/dPackagePrefix=\"{0}\" ", packagePrefix.ToUpper());
        args.AppendFormat("/dBuildPath=\"{0}\" ", buildPath);
        args.AppendFormat("/dPlatform={0} ", platform);
        args.AppendFormat("/dTargetPath=\"{0}\" ", targetPath);
        args.AppendFormat("\"{0}\"", scriptPath);

        Process.Run(installerPath, installerProgramPath, args.ToString().Trim());
    }
}
