using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class FixIronSourceBundle {
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {
        if (target != BuildTarget.iOS) return;

        string plistPath = pathToBuiltProject + "/Frameworks/UnityFramework.framework/IronSourceAdQualityPrivacyInfo.bundle/Info.plist";
        
        if (File.Exists(plistPath)) {
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            
            // The Hero Moves:
            if (plist.root.values.ContainsKey("CFBundleExecutable"))
            {
                plist.root.values.Remove("CFBundleExecutable");
            }
            plist.root.SetString("CFBundlePackageType", "BNDL");
            
            plist.WriteToFile(plistPath);
        }
    }
}