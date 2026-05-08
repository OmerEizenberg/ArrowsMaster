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
        entitlements.AddAssociatedDomains(new string[] { "applinks:levelupmedia.singular.com", "applinks:levelupmedia.online" });
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
            project.SetBuildProperty(targetGuid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
            project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
            project.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
        }

        // --- NEW FIX: Removed manual embedding of FBAudienceNetwork ---
        // Apple does not permit static libraries in the Frameworks folder. 
        // Since we use 'use_frameworks! :linkage => :static' in the Podfile, 
        // CocoaPods will link FBAudienceNetwork correctly into the main binary.
        // Embedding it manually was causing the "binary file is not permitted" validation error.

        // Add a selective Build Phase to fix invalid executable keys in Resource Bundles only.
        // This targets only 'BNDL' types to avoid breaking actual frameworks.
        string scriptBody = "# Search for ALL Info.plist files inside the built app\n" +
                            "find \"${TARGET_BUILD_DIR}\" -name \"Info.plist\" | while read -r PLIST; do\n" +
                            "    # Check if it is a Resource Bundle (BNDL)\n" +
                            "    PACKAGE_TYPE=$(/usr/libexec/PlistBuddy -c \"Print :CFBundlePackageType\" \"$PLIST\" 2>/dev/null)\n" +
                            "    if [[ \"$PACKAGE_TYPE\" == \"BNDL\" || \"$PLIST\" == *\"PrivacyInfo.bundle\"* || \"$PLIST\" == *\"Resources.bundle\"* ]]; then\n" +
                            "        if /usr/libexec/PlistBuddy -c \"Print :CFBundleExecutable\" \"$PLIST\" > /dev/null 2>&1; then\n" +
                            "            echo \"Fixing invalid executable key in Resource Bundle: $PLIST\"\n" +
                            "            /usr/libexec/PlistBuddy -c \"Delete :CFBundleExecutable\" \"$PLIST\" || true\n" +
                            "            /usr/libexec/PlistBuddy -c \"Set :CFBundlePackageType BNDL\" \"$PLIST\" || true\n" +
                            "        fi\n" +
                            "    fi\n" +
                            "done";
        project.AddShellScriptBuildPhase(mainTargetGuid, "Fix Resource Bundles", "/bin/sh", scriptBody);

        project.WriteToFile(projectPath);

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

        // Check for our ultimate fix signature (Updated to S7 for new fixes)
        if (podfileContent.Contains("FIX_FB12_S7"))
        {
            Debug.Log("[IOSBuildPostProcess] Podfile already contains the latest FB12/S7 fixes.");
            return;
        }

        // Refined post_install block that is extremely aggressive
        // Using concatenation to avoid '#' at the start of lines which can confuse some C# compilers
        string postInstallBlock = "\n# FIX_FB12_S7\n" +
                                  "use_frameworks! :linkage => :static\n" +
                                  "post_install do |installer|\n" +
                                  "  installer.pods_project.targets.each do |target|\n" +
                                  "    target.build_configurations.each do |config|\n" +
                                  "      config.build_settings['SWIFT_VERSION'] = '5.10'\n" +
                                  "      config.build_settings['ENABLE_BITCODE'] = 'NO'\n" +
                                  "      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n" +
                                  "      config.build_settings['GENERATE_INFOPLIST_FILE'] = 'YES'\n" +
                                  "      config.build_settings['ENABLE_USER_SCRIPT_SANDBOXING'] = 'NO'\n" +
                                  "      config.build_settings['EXCLUDED_ARCHS[sdk=iphonesimulator*]'] = 'arm64'\n" +
                                  "      config.build_settings['STRIP_INSTALLED_PRODUCT'] = 'YES'\n" +
                                  "      config.build_settings['DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'\n" +
                                  "      \n" +
                                  "      # FBAudienceNetwork specific fix for builtin-collectSignature error\n" +
                                  "      if target.name.include? 'FBAudienceNetwork'\n" +
                                  "        config.build_settings['CODE_SIGNING_ALLOWED'] = 'NO'\n" +
                                  "      end\n" +
                                  "      \n" +
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
                                  "        config.build_settings['ENABLE_USER_SCRIPT_SANDBOXING'] = 'NO'\n" +
                                  "      end\n" +
                                  "    end\n" +
                                  "  end\n" +
                                  "end\n";

        // Append our block at the very end to ensure it takes precedence
        podfileContent += "\n" + postInstallBlock;

        File.WriteAllText(podfilePath, podfileContent);
        Debug.Log("[IOSBuildPostProcess] Podfile updated with aggressive FBAudienceNetwork signature fixes.");
        
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

}
#endif


