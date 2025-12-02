using UnityEditor;
using UnityEngine;

public class BuildManager {
    private static Build build = null;

    [MenuItem("Build For/Linux x64 #%l", false, 100)]
    public static void BuildLinux()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneLinux64);
    }

    [MenuItem("Build For/Windows x64 #%w", false, 100)]
    public static void BuildWindows()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneWindows64);
    }

    public static void BuildWindows32()
    {
        prepareBuild();

        build.For(BuildTarget.StandaloneWindows);
    }

    public static void ReleaseLinux()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneLinux64);
    }

    public static void ReleaseWindows()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneWindows64);
    }

    public static void ReleaseWindows32()
    {
        BuildRelease release = new BuildRelease();

        release.For(BuildTarget.StandaloneWindows);
    }

    private static void prepareBuild()
    {
        if (build == null)
        {
            build = new();
        }
    }
}
