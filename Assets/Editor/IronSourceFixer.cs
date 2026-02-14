using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System.Collections.Generic;

public class IronSourceFixer {
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject) {
        if (target != BuildTarget.iOS) return;

        // List of known problematic bundles
        string[] bundles = {
            "IronSourceAdQualityPrivacyInfo.bundle",
            "IronSourcePrivacyInfo.bundle",
            "UnityAdsResources.bundle"
        };

        foreach (string bundleName in bundles) {
            // Find the bundle anywhere in the exported project
            string[] files = Directory.GetFiles(pathToBuiltProject, "Info.plist", SearchOption.AllDirectories);
            foreach (string file in files) {
                if (file.Contains(bundleName)) {
                    FixPlist(file);
                }
            }
        }
    }

    private static void FixPlist(string path) {
        // Use the Mac's built-in PlistBuddy to surgically remove the key
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "/usr/libexec/PlistBuddy";
        process.StartInfo.Arguments = $"-c \"Delete :CFBundleExecutable\" \"{path}\"";
        process.Start();
        process.WaitForExit();
        
        // Ensure the type is set to BNDL
        process.StartInfo.Arguments = $"-c \"Add :CFBundlePackageType string BNDL\" \"{path}\"";
        process.Start();
        process.WaitForExit();

        Debug.Log("Sustainably Fixed Plist at: " + path);
    }
}