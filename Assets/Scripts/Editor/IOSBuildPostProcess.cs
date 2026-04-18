#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[InitializeOnLoad]
public class IOSBuildPostProcess
{
    static IOSBuildPostProcess()
    {
        // Set environment variables for the current process so all internal Unity calls (like EDM4U)
        // use the correct encoding and path.
        System.Environment.SetEnvironmentVariable("LANG", "en_US.UTF-8");
        System.Environment.SetEnvironmentVariable("LC_ALL", "en_US.UTF-8");
        System.Environment.SetEnvironmentVariable("LC_CTYPE", "en_US.UTF-8");
        System.Environment.SetEnvironmentVariable("RUBYOPT", "-Eutf-8");
        
        string currentPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        string homebrewPath = "/opt/homebrew/bin:/usr/local/bin";
        if (!currentPath.StartsWith(homebrewPath))
        {
            System.Environment.SetEnvironmentVariable("PATH", homebrewPath + ":" + currentPath);
        }

        // Silence EDM4U as early as possible (when script is loaded)
        SilenceEDM4U();
    }

    // High order to ensure it runs after EDM4U and other processors
    [PostProcessBuild(2000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        UpdatePodfile(pathToBuiltProject);
        UpdateXcodeProject(pathToBuiltProject);
        FixFBLPromisesPrivacyBundle(pathToBuiltProject);
    }

    private static void SilenceEDM4U()
    {
        try
        {
            // We use reflection to set EDM4U settings to false to prevent it from running its own
            // failing 'pod install' and showing the error popup.
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains("Google.IOSResolver"))
                {
                    var type = assembly.GetType("Google.IOSResolver");
                    if (type != null)
                    {
                        var podfileEnabled = type.GetProperty("PodfileEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (podfileEnabled != null) podfileEnabled.SetValue(null, false);
                        
                        var autoInstall = type.GetProperty("AutoPodToolInstallInEditor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (autoInstall != null) autoInstall.SetValue(null, false);

                        var podToolExecutionViaShellEnabled = type.GetProperty("PodToolExecutionViaShellEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (podToolExecutionViaShellEnabled != null) podToolExecutionViaShellEnabled.SetValue(null, true);
                        
                        Debug.Log("[IOSBuildPostProcess] programmatically silenced EDM4U resolver at load time.");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[IOSBuildPostProcess] Could not silence EDM4U via reflection: " + e.Message);
        }
    }

    private static void UpdateXcodeProject(string pathToBuiltProject)
    {
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        // 1. Add Push Notifications Capability
        var entitlementsPath = "Unity-iPhone/Unity-iPhone.entitlements";
        var entitlements = new ProjectCapabilityManager(projectPath, entitlementsPath, "Unity-iPhone");
        entitlements.AddPushNotifications(true);
        entitlements.WriteToFile();

        // 2. Add Background Modes (Remote Notifications)
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        
        PlistElementDict rootDict = plist.root;
        
        // ATT Permission Message
        rootDict.SetString("NSUserTrackingUsageDescription", "Your data will be used to provide you with a better and more personalized ad experience.");

        PlistElementArray backgroundModes = rootDict.CreateArray("UIBackgroundModes");
        backgroundModes.AddString("remote-notification");

        // 3. Add SKAdNetwork IDs for Attribution
        AddSKAdNetworkIds(rootDict);
        
        plist.WriteToFile(plistPath);

        // 3. Reliable Build Settings (Classic Must-Haves)
        foreach (var targetGuid in new[] { mainTargetGuid, frameworkTargetGuid })
        {
            project.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
            project.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
            project.SetBuildProperty(targetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
            project.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
        }

        EmbedFramework(project, mainTargetGuid, "FBAudienceNetwork", pathToBuiltProject);

        // Add the user's manual shell script fix as a Build Phase
        // Updated to find ALL .bundle folders and remove invalid executable keys (fixes FBAudienceNetwork.bundle error)
        string scriptBody = "find \"${TARGET_BUILD_DIR}\" -name \"*.bundle\" -type d | while read -r BUNDLE; do\n" +
                            "    PLIST=\"$BUNDLE/Info.plist\"\n" +
                            "    if [ -f \"$PLIST\" ]; then\n" +
                            "        echo \"Checking bundle: $BUNDLE\"\n" +
                            "        /usr/libexec/PlistBuddy -c \"Delete :CFBundleExecutable\" \"$PLIST\" || true\n" +
                            "        /usr/libexec/PlistBuddy -c \"Set :CFBundlePackageType BNDL\" \"$PLIST\" || true\n" +
                            "    fi\n" +
                            "done";
        project.AddShellScriptBuildPhase(mainTargetGuid, "Fix ALL Resource Bundles", "/bin/sh", scriptBody);

        project.WriteToFile(projectPath);
        
        // --- START SURGERY ---
        // Since the Unity API for "Embed & Sign" is failing, we manually patch the pbxproj file.
        FixCodeSigningInPbxproj(projectPath);
        // --- END SURGERY ---

        Debug.Log("[IOSBuildPostProcess] Xcode project settings updated successfully.");
    }

    private static void AddSKAdNetworkIds(PlistElementDict rootDict)
    {
        PlistElementArray skanItems;
        if (rootDict.values.ContainsKey("SKAdNetworkItems"))
        {
            skanItems = rootDict.values["SKAdNetworkItems"].AsArray();
        }
        else
        {
            skanItems = rootDict.CreateArray("SKAdNetworkItems");
        }

        // List of essential SKAdNetwork IDs (Singular, ironSource, and common networks)
        string[] skanIds = new string[] {
            "v72qych5uu.skadnetwork", // Singular
            "su67r6k2v3.skadnetwork", // ironSource
            "4pfyvq9l8r.skadnetwork", // ironSource
            "ludvb6z3bs.skadnetwork", // ironSource
            "mlmmfth3ar.skadnetwork", // ironSource
            "5lm9lj6jb7.skadnetwork", // ironSource
            "9rd848q2sf.skadnetwork", // ironSource
            "7ug5zh24hu.skadnetwork", // ironSource
            "hs6bdukanm.skadnetwork", // ironSource
            "m8dbw4sv7c.skadnetwork", // ironSource
            "9nlqeag3gk.skadnetwork", // ironSource
            "cj5566h2ga.skadnetwork", // ironSource
            "v9wttpbfk9.skadnetwork", // ironSource
            "n38lu8286q.skadnetwork", // ironSource
            "cstr6suwn9.skadnetwork", // ironSource/LifeStreet
            "wzmmz9fp6w.skadnetwork", // InMobi
            "f38h382jlk.skadnetwork", // Unity Ads
            "2u9pt9hc89.skadnetwork", // Unity Ads
            "3rd42ekr43.skadnetwork", // Unity Ads
            "4468km3ulz.skadnetwork", // Apple Search Ads
            "4fzdc2evr5.skadnetwork", // AppLovin
            "t38b2kh725.skadnetwork", // AppLovin
            "7rz5w94nxq.skadnetwork", // AppLovin
            "9t245vhm4d.skadnetwork"  // AppLovin
        };

        foreach (string id in skanIds)
        {
            bool exists = false;
            foreach (var item in skanItems.values)
            {
                if (item.AsDict().values.ContainsKey("SKAdNetworkIdentifier") &&
                    item.AsDict().values["SKAdNetworkIdentifier"].AsString() == id)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                PlistElementDict dict = skanItems.AddDict();
                dict.SetString("SKAdNetworkIdentifier", id);
            }
        }
    }

    private static void UpdatePodfile(string pathToBuiltProject)
    {
        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        if (!File.Exists(podfilePath))
        {
            Debug.LogWarning("[IOSBuildPostProcess] Podfile not found at: " + podfilePath);
            return;
        }

        string podfileContent = File.ReadAllText(podfilePath);

        // Check for our ultimate fix signature
        if (podfileContent.Contains("FIX_FB12_S6"))
        {
            Debug.Log("[IOSBuildPostProcess] Podfile already contains the FB12 fixes.");
            return;
        }

        // Refined post_install block that is extremely aggressive
        // Using concatenation to avoid '#' at the start of lines which can confuse some C# compilers
        string postInstallBlock = "\n# FIX_FB12_S6\n" +
                                  "use_frameworks! :linkage => :static\n" +
                                  "post_install do |installer|\n" +
                                  "  installer.pods_project.targets.each do |target|\n" +
                                  "    target.build_configurations.each do |config|\n" +
                                  "      config.build_settings['SWIFT_VERSION'] = '5.10'\n" +
                                  "      config.build_settings['ENABLE_BITCODE'] = 'NO'\n" +
                                  "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                                  "      config.build_settings['GENERATE_INFOPLIST_FILE'] = 'YES'\n" +
                                  "      config.build_settings['EXCLUDED_ARCHS[sdk=iphonesimulator*]'] = 'arm64'\n" +
                                  "      config.build_settings['STRIP_INSTALLED_PRODUCT'] = 'YES'\n" +
                                  "      config.build_settings['DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'\n" +
                                  "      flags = '$(inherited) -enable-experimental-feature AccessLevelOnImport -enable-experimental-feature RegionBasedIsolation -Xfrontend -enable-upcoming-feature -Xfrontend RegionBasedIsolation'\n" +
                                  "      config.build_settings['OTHER_SWIFT_FLAGS'] = flags\n" +
                                  "      config.build_settings['CODE_SIGN_ON_COPY'] = 'YES'\n" +
                                  "    end\n" +
                                  "  end\n" +
                                  "  \n" +
                                  "  # Ensure the Main Target and UnityFramework also generate dSYMs\n" +
                                  "  installer.aggregate_targets.each do |aggregate_target|\n" +
                                  "    aggregate_target.user_project.targets.each do |target|\n" +
                                  "      target.build_configurations.each do |config|\n" +
                                  "        config.build_settings['DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'\n" +
                                  "      end\n" +
                                  "    end\n" +
                                  "  end\n" +
                                  "end\n";

        // Append our block at the very end to ensure it takes precedence
        podfileContent += "\n" + postInstallBlock;

        File.WriteAllText(podfilePath, podfileContent);
        Debug.Log("[IOSBuildPostProcess] Podfile updated with aggressive Firebase 12 compatibility fixes.");
        
        RunPodInstall(pathToBuiltProject);
    }

    private static void RunPodInstall(string pathToBuiltProject)
    {
        Debug.Log("[IOSBuildPostProcess] Running pod install with UTF-8 environment...");
        
        string[] podPaths = { "/opt/homebrew/bin/pod", "/usr/local/bin/pod", "pod" };
        string chosenPod = "pod";
        
        foreach (var path in podPaths)
        {
            if (File.Exists(path))
            {
                chosenPod = path;
                break;
            }
        }

        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = "/bin/bash";
        // Force absolute paths for homebrew and exports for UTF-8 and Ruby compatibility
        startInfo.Arguments = $"-c \"export PATH=\\\"/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin\\\" && export LANG=en_US.UTF-8 && export LC_ALL=en_US.UTF-8 && export RUBYOPT=\\\"-Eutf-8\\\" && cd \\\"{pathToBuiltProject}\\\" && \\\"{chosenPod}\\\" install\"";
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Debug.Log("[IOSBuildPostProcess] pod install finished successfully.\n" + output);
            }
            else
            {
                Debug.LogError("[IOSBuildPostProcess] pod install failed with exit code " + process.ExitCode + ".\nError: " + error + "\nOutput: " + output);
            }
        }
    }

    private static void FixFBLPromisesPrivacyBundle(string pathToBuiltProject)
    {
        // Common locations for this bundle in Unity iOS builds
        string[] relativePaths = {
            "UnityFramework/FBLPromises_Privacy.bundle/Info.plist",
            "Pods/FBLPromises/Sources/FBLPromises/Resources/FBLPromises_Privacy.bundle/Info.plist",
            "Frameworks/FBLPromises_Privacy.bundle/Info.plist"
        };

        bool found = false;
        foreach (var relPath in relativePaths)
        {
            string fullPath = Path.Combine(pathToBuiltProject, relPath);
            if (File.Exists(fullPath))
            {
                PatchPlist(fullPath);
                found = true;
            }
        }

        if (!found)
        {
            // Search recursively if not found in common spots
            try
            {
                string[] files = Directory.GetFiles(pathToBuiltProject, "Info.plist", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (file.Contains("FBLPromises_Privacy.bundle"))
                    {
                        PatchPlist(file);
                        found = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IOSBuildPostProcess] Error searching for FBLPromises_Privacy.bundle: " + e.Message);
            }
        }

        if (!found)
        {
            Debug.LogWarning("[IOSBuildPostProcess] FBLPromises_Privacy.bundle/Info.plist not found in " + pathToBuiltProject);
        }
    }

    private static void PatchPlist(string plistPath)
    {
        try
        {
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            
            bool modified = false;
            if (plist.root.values.ContainsKey("CFBundleExecutable"))
            {
                plist.root.values.Remove("CFBundleExecutable");
                modified = true;
                Debug.Log("[IOSBuildPostProcess] Removed CFBundleExecutable from " + plistPath);
            }
            
            string currentType = "";
            if (plist.root.values.ContainsKey("CFBundlePackageType"))
            {
                currentType = plist.root.values["CFBundlePackageType"].AsString();
            }

            if (currentType != "BNDL")
            {
                plist.root.SetString("CFBundlePackageType", "BNDL");
                modified = true;
                Debug.Log("[IOSBuildPostProcess] Set CFBundlePackageType to BNDL in " + plistPath);
            }
            
            if (modified)
            {
                plist.WriteToFile(plistPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[IOSBuildPostProcess] Failed to patch plist at " + plistPath + ": " + e.Message);
        }
    }

    private static void EmbedFramework(PBXProject project, string mainTargetGuid, string frameworkName, string pathToBuiltProject)
    {
        // Start with a recursive search for the framework/xcframework
        string foundPath = null;
        try
        {
            // Debug: Log what folders exist in Pods to help us find the right name/path
            string podsPath = Path.Combine(pathToBuiltProject, "Pods");
            if (Directory.Exists(podsPath))
            {
                string[] allDirs = Directory.GetDirectories(podsPath, "*", SearchOption.AllDirectories);
                Debug.Log("[IOSBuildPostProcess] Found " + allDirs.Length + " directories in Pods.");
                foreach (var d in allDirs)
                {
                    if (d.ToLower().Contains("audience")) Debug.Log("[IOSBuildPostProcess]   Checking Pod Dir: " + d);
                }
            }
            else
            {
                Debug.LogWarning("[IOSBuildPostProcess] Pods directory does NOT exist yet at: " + podsPath);
            }

            string[] dirs = Directory.GetDirectories(pathToBuiltProject, frameworkName + ".*", SearchOption.AllDirectories);
            foreach (var dir in dirs)
            {
                if (dir.EndsWith(".framework") || dir.EndsWith(".xcframework"))
                {
                    foundPath = dir.Replace(pathToBuiltProject, "").TrimStart(Path.DirectorySeparatorChar, '/');
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[IOSBuildPostProcess] Error searching for " + frameworkName + ": " + e.Message);
        }

        if (string.IsNullOrEmpty(foundPath))
        {
            // Fallback to common locations if search failed
            string[] possiblePaths = {
                "Pods/" + frameworkName + "/" + frameworkName + ".xcframework",
                "Pods/FBAudienceNetwork/" + frameworkName + ".xcframework",
                "Frameworks/" + frameworkName + ".framework"
            };
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(Path.Combine(pathToBuiltProject, path)))
                {
                    foundPath = path;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(foundPath))
        {
            Debug.Log("[IOSBuildPostProcess] Found " + frameworkName + " on disk at: " + foundPath);
            
            // 2. Try to find the GUID in the project. If not found, add it.
            string fileGuid = project.FindFileGuidByProjectPath(foundPath);
            if (string.IsNullOrEmpty(fileGuid))
            {
                Debug.Log("[IOSBuildPostProcess] Adding " + frameworkName + " reference to Xcode project.");
                fileGuid = project.AddFile(foundPath, foundPath, PBXSourceTree.Source);
            }

            // 3. Embed it with "Embed & Sign"
            if (!string.IsNullOrEmpty(fileGuid))
            {
                Debug.Log("[IOSBuildPostProcess] Embedding FBAudienceNetwork (GUID: " + fileGuid + ")");
                project.AddFileToEmbedFrameworks(mainTargetGuid, fileGuid);
                // Note: Code signing is now handled via the Podfile post_install block to avoid API compatibility issues.
            }
        }
        else
        {
            Debug.LogError("[IOSBuildPostProcess] CRITICAL: Could not find " + frameworkName + " on disk in the exported project!");
        }
    }

    private static void FixCodeSigningInPbxproj(string projectPath)
    {
        string pbxprojText = File.ReadAllText(projectPath);
        // More robust search for FBAudienceNetwork in any build file block
        string searchLabel = "FBAudienceNetwork.xcframework";
        
        if (pbxprojText.Contains(searchLabel))
        {
            Debug.Log("[IOSBuildPostProcess] Manually patching pbxproj for FBAudienceNetwork signing...");
            // Regex to find the PBXBuildFile entry for FBAudienceNetwork and inject settings = {ATTRIBUTES = (CodeSignOnCopy, ); }
            // We look for any instance that ends in }; and doesn't already have attributes.
            string pattern = "(/\\* " + searchLabel + ".* \\*/ = {isa = PBXBuildFile; fileRef = [A-Z0-9]+; )};";
            string replacement = "$1settings = {ATTRIBUTES = (CodeSignOnCopy, ); }; };";
            pbxprojText = Regex.Replace(pbxprojText, pattern, replacement);
            
            File.WriteAllText(projectPath, pbxprojText);
        }
        else
        {
            Debug.LogWarning("[IOSBuildPostProcess] Could not find FBAudienceNetwork entry in pbxproj for manual signing patch. Checking for .framework variant...");
            // Try again with .framework just in case
            searchLabel = "FBAudienceNetwork.framework";
            if (pbxprojText.Contains(searchLabel))
            {
                 string pattern = "(/\\* " + searchLabel + ".* \\*/ = {isa = PBXBuildFile; fileRef = [A-Z0-9]+; )};";
                 string replacement = "$1settings = {ATTRIBUTES = (CodeSignOnCopy, ); }; };";
                 pbxprojText = Regex.Replace(pbxprojText, pattern, replacement);
                 File.WriteAllText(projectPath, pbxprojText);
            }
        }
    }
}
#endif


