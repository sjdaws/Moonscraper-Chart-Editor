using UnityEditor;
using UnityEngine;

public class BuildManager {
    private static Build build = null;

    [MenuItem("Build For/All Platforms #%a", false, 0)]
    public static void BuildAll()
    {
        BuildWindows32();
        BuildWindows64();
        BuildLinux();
    }

    [MenuItem("Build For/Linux x64 #%l", false, 100)]
    public static void BuildLinux()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneLinux64);
    }

    [MenuItem("Build For/Windows x86", false, 101)]
    public static void BuildWindows32()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneWindows);
    }

    [MenuItem("Build For/Windows x64 #%w", false, 102)]
    public static void BuildWindows64()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Build For/TEST #%t", false, 1020)]
    public static void ReleaseLinux()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneLinux64);
    }

    public static void ReleaseWindows32()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneWindows);
    }

    public static void ReleaseWindows64()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneWindows64);
    }

    private static void prepareBuild()
    {
        if (build == null)
        {
            build = new();
        }
    }
}
