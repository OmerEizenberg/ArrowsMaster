#if UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[InitializeOnLoad]
public class IOSBuildPostProcess : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    static IOSBuildPostProcess()
    {
        ConfigureBuildEnvironment();
        ConfigureIOSResolverForWorkspace();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        EnsureCocoaPodsAvailableOrThrow();
    }

    private static void ConfigureBuildEnvironment()
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
    }

    private static void ConfigureIOSResolverForWorkspace()
    {
        try
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.FullName.Contains("Google.IOSResolver")) continue;

                var type = assembly.GetType("Google.IOSResolver");
                if (type == null) continue;

                // Workspace integration creates Unity-iPhone.xcworkspace (required for Firebase / AppLovin MAX pods).
                SetStaticBool(type, "PodfileGenerationEnabled", true);

                var integrationMethod = type.GetProperty("CocoapodsIntegrationMethodPref",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (integrationMethod != null)
                {
                    var workspaceEnum = System.Enum.Parse(integrationMethod.PropertyType, "Workspace");
                    integrationMethod.SetValue(null, workspaceEnum);
                }

                // Let our post-process run the final pod install after Podfile tweaks.
                SetStaticBool(type, "SkipPodInstallWhenUsingWorkspaceIntegration", true);

                Debug.Log("[IOSBuildPostProcess] iOS Resolver configured for Xcode workspace integration.");
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[IOSBuildPostProcess] Could not configure iOS Resolver: " + e.Message);
        }
    }

    private static void SetStaticBool(System.Type type, string propertyName, bool value)
    {
        var property = type.GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (property != null && property.PropertyType == typeof(bool))
        {
            property.SetValue(null, value);
        }
    }

    // High order to ensure it runs after EDM4U and other processors
    [PostProcessBuild(2000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        PatchSingularUnityWrapper(pathToBuiltProject);
        UpdatePodfile(pathToBuiltProject);
        UpdateXcodeProject(pathToBuiltProject);
        FixWebKitFrameworkCasing(pathToBuiltProject);
        FixFBLPromisesPrivacyBundle(pathToBuiltProject);
        EnsureWorkspaceExistsOrThrow(pathToBuiltProject);
    }

    private static void PatchSingularUnityWrapper(string pathToBuiltProject)
    {
        string wrapperPath = Path.Combine(
            pathToBuiltProject,
            "Libraries/singular-unity-package/SingularSDK/Plugins/iOS/SingularUnityWrapper.mm");

        if (!File.Exists(wrapperPath))
        {
            Debug.LogWarning("[IOSBuildPostProcess] SingularUnityWrapper.mm not found; skipping main-thread patch.");
            return;
        }

        string content = File.ReadAllText(wrapperPath);
        if (content.Contains("dispatch_get_main_queue"))
        {
            Debug.Log("[IOSBuildPostProcess] SingularUnityWrapper.mm already patched for main-thread delivery.");
            return;
        }

        const string oldSendSdkMessage =
            "static void sendSdkMessage(const char *methodName, NSString *param) {\n" +
            "    const char* str = [param UTF8String];\n" +
            "    char* result = (char*)malloc(strlen(str)+1);\n" +
            "    strcpy(result,str);\n" +
            "    \n" +
            "    UnitySendMessage(\"SingularSDKObject\", methodName, result);\n" +
            "}";

        const string newSendSdkMessage =
            "static void sendSdkMessage(const char *methodName, NSString *param) {\n" +
            "    const char* str = [param UTF8String];\n" +
            "    if (str == NULL) {\n" +
            "        return;\n" +
            "    }\n" +
            "    size_t length = strlen(str);\n" +
            "    char* result = (char*)malloc(length + 1);\n" +
            "    if (result == NULL) {\n" +
            "        return;\n" +
            "    }\n" +
            "    memcpy(result, str, length + 1);\n" +
            "\n" +
            "    void (^deliverMessage)(void) = ^{\n" +
            "        UnitySendMessage(\"SingularSDKObject\", methodName, result);\n" +
            "    };\n" +
            "\n" +
            "    if ([NSThread isMainThread]) {\n" +
            "        deliverMessage();\n" +
            "    } else {\n" +
            "        dispatch_async(dispatch_get_main_queue(), deliverMessage);\n" +
            "    }\n" +
            "}";

        if (!content.Contains(oldSendSdkMessage))
        {
            Debug.LogWarning("[IOSBuildPostProcess] SingularUnityWrapper.mm format changed; main-thread patch not applied.");
            return;
        }

        content = content.Replace(oldSendSdkMessage, newSendSdkMessage);

        const string oldDeferredHandler =
            "    void RegisterDeferredDeepLinkHandler_(){\n" +
            "        [Singular registerDeferredDeepLinkHandler:^(NSString *deeplink) {\n" +
            "            if(deeplink != NULL){\n" +
            "                const char* str = [deeplink UTF8String];\n" +
            "                char* result = (char*)malloc(strlen(str)+1);\n" +
            "                strcpy(result,str);\n" +
            "                UnitySendMessage(\"SingularSDKObject\", \"DeepLinkHandler\", result);\n" +
            "            }else{\n" +
            "                UnitySendMessage(\"SingularSDKObject\", \"DeepLinkHandler\", \"\");\n" +
            "            }\n" +
            "        }];\n" +
            "    }";

        const string newDeferredHandler =
            "    void RegisterDeferredDeepLinkHandler_(){\n" +
            "        [Singular registerDeferredDeepLinkHandler:^(NSString *deeplink) {\n" +
            "            if(deeplink != NULL){\n" +
            "                sendSdkMessage(\"DeepLinkHandler\", deeplink);\n" +
            "            }else{\n" +
            "                sendSdkMessage(\"DeepLinkHandler\", @\"\");\n" +
            "            }\n" +
            "        }];\n" +
            "    }";

        if (content.Contains(oldDeferredHandler))
        {
            content = content.Replace(oldDeferredHandler, newDeferredHandler);
        }
        else
        {
            Debug.LogWarning("[IOSBuildPostProcess] Singular deferred deep link handler format changed; patch not applied.");
        }

        File.WriteAllText(wrapperPath, content);
        Debug.Log("[IOSBuildPostProcess] Patched SingularUnityWrapper.mm to deliver UnitySendMessage on the main thread.");
    }

    private const string WorkspaceName = "Unity-iPhone.xcworkspace";

    private static string ResolvePodExecutable()
    {
        string[] podPaths = { "/opt/homebrew/bin/pod", "/usr/local/bin/pod", "pod" };
        foreach (var path in podPaths)
        {
            if (path == "pod" || File.Exists(path))
            {
                return path;
            }
        }
        return "pod";
    }

    private static bool IsPodAvailable()
    {
        string pod = ResolvePodExecutable();
        if (pod != "pod" && !File.Exists(pod)) return false;

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"export PATH=\\\"/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin\\\" && \\\"{pod}\\\" --version\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureCocoaPodsAvailableOrThrow()
    {
        if (IsPodAvailable()) return;

        throw new BuildFailedException(
            "CocoaPods is not installed on this Mac. Unity iOS builds for this project need " +
            "'pod install' to generate Unity-iPhone.xcworkspace.\n\n" +
            "Install CocoaPods, then rebuild:\n" +
            "  brew install cocoapods\n" +
            "  # or: sudo gem install cocoapods\n\n" +
            "Verify with: pod --version");
    }

    private static void EnsureWorkspaceExistsOrThrow(string pathToBuiltProject)
    {
        string workspacePath = Path.Combine(pathToBuiltProject, WorkspaceName);
        if (Directory.Exists(workspacePath))
        {
            Debug.Log("[IOSBuildPostProcess] Open this file in Xcode: " + workspacePath);
            return;
        }

        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        string podfileHint = File.Exists(podfilePath)
            ? "A Podfile was generated but 'pod install' did not create the workspace."
            : "No Podfile was found in the Xcode export folder.";

        throw new BuildFailedException(
            WorkspaceName + " was not created after the iOS build.\n" +
            podfileHint + "\n\n" +
            "Install CocoaPods if needed (brew install cocoapods), then run:\n" +
            "  cd \"" + pathToBuiltProject + "\"\n" +
            "  pod install\n\n" +
            "Always open " + WorkspaceName + " in Xcode, not Unity-iPhone.xcodeproj.");
    }

    /// <summary>
    /// Singular SDK post-build adds "Webkit.framework" (wrong casing). Xcode requires WebKit.framework.
    /// </summary>
    private static void FixWebKitFrameworkCasing(string pathToBuiltProject)
    {
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        if (!File.Exists(projectPath)) return;

        string content = File.ReadAllText(projectPath);
        if (!content.Contains("Webkit.framework")) return;

        content = content.Replace("Webkit.framework", "WebKit.framework");
        File.WriteAllText(projectPath, content);
        Debug.Log("[IOSBuildPostProcess] Corrected WebKit.framework casing in project.pbxproj.");
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

        // AdMob application ID (required for AppLovin MAX AdMob mediation adapter on iOS)
        rootDict.SetString("GADApplicationIdentifier", "ca-app-pub-2980983758149509~7869782198");

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
            project.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @loader_path/Frameworks");
        }

        // --- NEW FIX: S11 - Robust Validation & Runtime Fix ---
        // We only strip executables from REAL resource bundles (.bundle).
        // We DO NOT touch .framework folders (like FBAudienceNetwork) as they need their executables 
        // to load correctly at runtime when using dynamic linkage.
        string scriptBody = "# Search for Info.plist files inside the app bundle\n" +
                            "find \"${TARGET_BUILD_DIR}\" -name \"Info.plist\" | while read -r PLIST; do\n" +
                            "    # 1. Fix Resource Bundles (.bundle folders)\n" +
                            "    if [[ \"$PLIST\" == *.bundle/* ]]; then\n" +
                            "        echo \"Fixing Resource Bundle: $PLIST\"\n" +
                            "        /usr/libexec/PlistBuddy -c \"Delete :CFBundleExecutable\" \"$PLIST\" > /dev/null 2>&1 || true\n" +
                            "        /usr/libexec/PlistBuddy -c \"Set :CFBundlePackageType BNDL\" \"$PLIST\" > /dev/null 2>&1 || true\n" +
                            "    fi\n" +
                            "    \n" +
                            "    # 2. Specific fix for FBLPromises Privacy Bundle (often mispackaged)\n" +
                            "    if [[ \"$PLIST\" == *\"FBLPromises_Privacy.bundle/Info.plist\" ]]; then\n" +
                            "        echo \"Fixing FBLPromises Privacy Bundle: $PLIST\"\n" +
                            "        /usr/libexec/PlistBuddy -c \"Delete :CFBundleExecutable\" \"$PLIST\" > /dev/null 2>&1 || true\n" +
                            "        /usr/libexec/PlistBuddy -c \"Set :CFBundlePackageType BNDL\" \"$PLIST\" > /dev/null 2>&1 || true\n" +
                            "    fi\n" +
                            "done";
        project.AddShellScriptBuildPhase(mainTargetGuid, "Fix Resource Bundles", "/bin/sh", scriptBody);

        project.WriteToFile(projectPath);

        Debug.Log("[IOSBuildPostProcess] Xcode project settings updated successfully (Version S13).");
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

        // List of essential SKAdNetwork IDs (Updated May 2026)
        string[] skanIds = new string[] {
            "mj797d8u6f.skadnetwork",
            "238da6jt44.skadnetwork",
            "8s468mfl3y.skadnetwork",
            "cstr6suwn9.skadnetwork",
            "9t245vhmpl.skadnetwork",
            "tl55sbb4fm.skadnetwork",
            "w9q455wk68.skadnetwork",
            "97r2b46745.skadnetwork",
            "e5fvkxwrpn.skadnetwork",
            "vhf287vqwu.skadnetwork",
            "mqn7fxpca7.skadnetwork",
            "v79kvwwj4g.skadnetwork",
            "5tjdwbrq8w.skadnetwork",
            "9nlqeag3gk.skadnetwork",
            "v72qych5uu.skadnetwork",
            "3qy4746246.skadnetwork",
            "f7s53z58qe.skadnetwork",
            "prcb7njmu6.skadnetwork",
            "lr83yxwka7.skadnetwork",
            "hs6bdukanm.skadnetwork",
            "zmvfpc5aq8.skadnetwork",
            "k674qkevps.skadnetwork",
            "3sh42y64q3.skadnetwork",
            "424m5254lk.skadnetwork",
            "pwa73g5rt2.skadnetwork",
            "6yxyv74ff7.skadnetwork",
            "4dzt52r2t5.skadnetwork",
            "yclnxrl5pm.skadnetwork",
            "2fnua5tdw4.skadnetwork",
            "wzmmz9fp6w.skadnetwork",
            "5f5u5tfb26.skadnetwork",
            "4w7y6s5ca2.skadnetwork",
            "44jx6755aq.skadnetwork",
            "5lm9lj6jb7.skadnetwork",
            "4pfyvq9l8r.skadnetwork",
            "f38h382jlk.skadnetwork",
            "av6w8kgt66.skadnetwork",
            "f73kdq92p3.skadnetwork",
            "5a6flpkh64.skadnetwork",
            "3rd42ekr43.skadnetwork",
            "g6gcrrvk4p.skadnetwork",
            "4fzdc2evr5.skadnetwork",
            "c6k4g5qg8m.skadnetwork",
            "9rd848q2bz.skadnetwork",
            "m8dbw4sv7c.skadnetwork",
            "wg4vff78zm.skadnetwork",
            "glqzh8vgby.skadnetwork",
            "2u9pt9hc89.skadnetwork",
            "7ug5zh24hu.skadnetwork",
            "n9x2a789qt.skadnetwork",
            "s39g8k73mm.skadnetwork",
            "zq492l623r.skadnetwork",
            "mlmmfzh3r3.skadnetwork",
            "klf5c3l5u5.skadnetwork",
            "488r3q3dtq.skadnetwork",
            "xga6mpmplv.skadnetwork",
            "77y3x8wds4.skadnetwork",
            "ppxm28t8ap.skadnetwork",
            "4468km3ulz.skadnetwork",
            "32z4fx6l9h.skadnetwork",
            "a2p9lx4jpn.skadnetwork",
            "a8cz6cu7e5.skadnetwork",
            "22mmun2rn5.skadnetwork",
            "mp6xlyr22a.skadnetwork",
            "uw77j35x4d.skadnetwork",
            "5l3tpt7t6e.skadnetwork",
            "feyaarzu9v.skadnetwork",
            "t38b2kh725.skadnetwork",
            "578prtvx9j.skadnetwork",
            "kbd757ywx3.skadnetwork",
            "x44k69ngh6.skadnetwork",
            "k6y4y55b64.skadnetwork",
            "v9wttpbfk9.skadnetwork",
            "294l99pt4k.skadnetwork",
            "ydx93a7ass.skadnetwork",
            "p78axxw29g.skadnetwork",
            "su67r6k2v3.skadnetwork", // ironSource (added back as essential)
            "ludvb6z3bs.skadnetwork", // ironSource
            "mlmmfth3ar.skadnetwork", // ironSource
            "9rd848q2sf.skadnetwork", // ironSource
            "cj5566h2ga.skadnetwork", // ironSource
            "v9wttpbfk9.skadnetwork", // ironSource
            "n38lu8286q.skadnetwork", // ironSource
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

        // Remove duplicate CocoaPods source that causes "Found multiple specifications" errors.
        // The CDN source (cdn.cocoapods.org) already covers all public pods; the git-based
        // github.com/CocoaPods/Specs source is redundant and creates duplicate spec entries.
        podfileContent = Regex.Replace(podfileContent, @"source\s+'https://github\.com/CocoaPods/Specs'\s*\n?", "");

        // Clean up any previous versions of our fix blocks to avoid conflicts
        podfileContent = Regex.Replace(podfileContent, @"\n# FIX_FB12_S\d+.*?\nend\n", "", RegexOptions.Singleline);
        podfileContent = Regex.Replace(podfileContent, @"use_frameworks!.*?\n", "", RegexOptions.Singleline);

        // Refined post_install block (Version S13)
        // We use dynamic frameworks but force FBAudienceNetwork to be treated as a static framework.
        // This ensures it is linked into the binary (fixing "Library not loaded")
        // and NOT embedded in the Frameworks folder (fixing "Validation failed").
        string postInstallBlock = "\n# FIX_FB12_S13\n" +
                                  "use_frameworks!\n" +
                                  "post_install do |installer|\n" +
                                  "  installer.pod_targets.each do |pod|\n" +
                                  "    if pod.name.include?('FBAudienceNetwork') || pod.name.include?('Facebook')\n" +
                                  "      def pod.static_framework?; true end\n" +
                                  "      puts \"[IOSBuildPostProcess] Forced #{pod.name} to be static framework.\"\n" +
                                  "    end\n" +
                                  "  end\n" +
                                  "  \n" +
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
                                  "      flags = '$(inherited) -enable-experimental-feature AccessLevelOnImport -enable-experimental-feature RegionBasedIsolation -Xfrontend -enable-upcoming-feature -Xfrontend RegionBasedIsolation'\n" +
                                  "      config.build_settings['OTHER_SWIFT_FLAGS'] = flags\n" +
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
        Debug.Log("[IOSBuildPostProcess] Podfile updated with surgical static fix for Facebook (S13).");
        
        RunPodInstall(pathToBuiltProject);
    }

    private static void RunPodInstall(string pathToBuiltProject)
    {
        EnsureCocoaPodsAvailableOrThrow();

        string chosenPod = ResolvePodExecutable();
        Debug.Log("[IOSBuildPostProcess] Running pod install with UTF-8 environment using: " + chosenPod);

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
                return;
            }

            throw new BuildFailedException(
                "[IOSBuildPostProcess] pod install failed with exit code " + process.ExitCode + ".\n" +
                "Error: " + error + "\nOutput: " + output + "\n\n" +
                "Fix CocoaPods, then run manually:\n" +
                "  cd \"" + pathToBuiltProject + "\"\n" +
                "  pod install");
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


