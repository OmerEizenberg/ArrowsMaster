#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class IOSBuildPostProcess
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        // Get the main target (Unity-iPhone)
        string targetGuid = project.GetUnityMainTargetGuid();

        // 1. Add Push Notifications Capability
        // This will add the "aps-environment" to the entitlements file and the capability to the project
        var entitlementsPath = "Unity-iPhone/Unity-iPhone.entitlements";
        var entitlements = new ProjectCapabilityManager(projectPath, entitlementsPath, "Unity-iPhone");
        entitlements.AddPushNotifications(true); // true = development, will be handled by provisioning profile for production
        entitlements.WriteToFile();

        // 2. Add Background Modes (Remote Notifications)
        // This modifies the Info.plist
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        
        PlistElementDict rootDict = plist.root;
        PlistElementArray backgroundModes = rootDict.CreateArray("UIBackgroundModes");
        backgroundModes.AddString("remote-notification");
        
        plist.WriteToFile(plistPath);

        // 3. Optional: Add any missing frameworks if needed by Firebase
        // project.AddFrameworkToProject(targetGuid, "UserNotifications.framework", false);

        project.WriteToFile(projectPath);
    }
}
#endif
