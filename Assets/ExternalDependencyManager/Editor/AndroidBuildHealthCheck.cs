using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class AndroidBuildHealthCheck : EditorWindow
{
    [MenuItem("Tools/Android Build Health Check")]
    public static void ShowWindow() => GetWindow<AndroidBuildHealthCheck>("Build Health");

    private void OnGUI()
    {
        if (GUILayout.Button("Run Full Audit", GUILayout.Height(40))) RunAudit();
    }

    private void RunAudit()
    {
        Debug.ClearDeveloperConsole();
        Debug.Log("<color=cyan><b>Starting Android Build Health Audit...</b></color>");

        CheckGradleProperties();
        CheckPlayerSettings();
        CheckForHiddenDuplicates();
        CheckTemplateVariables();

        Debug.Log("<color=cyan><b>Audit Complete. Check the console for warnings!</b></color>");
    }

    void CheckGradleProperties()
    {
        string path = "Assets/Plugins/Android/gradleTemplate.properties";
        if (!File.Exists(path)) {
            Debug.LogError("MISSING: gradleTemplate.properties. Enable 'Custom Gradle Properties Template' in Player Settings.");
            return;
        }
        string content = File.ReadAllText(path);
        if (!content.Contains("android.useAndroidX=true")) Debug.LogWarning("FIX: Add 'android.useAndroidX=true' to gradleTemplate.properties");
        if (!content.Contains("android.enableJetifier=true")) Debug.LogWarning("FIX: Add 'android.enableJetifier=true' to gradleTemplate.properties");
    }

    void CheckPlayerSettings()
    {
        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
            Debug.LogWarning("MIN SDK: Level 24+ is recommended for modern Firebase/AppLovin.");
        
        if (PlayerSettings.Android.targetSdkVersion < AndroidSdkVersions.AndroidApiLevel34)
            Debug.LogWarning("TARGET SDK: Ensure this is set to 34+ for Google Play 2024/2025 compliance.");

        if (!PlayerSettings.Android.forceSDCardPermission) // Just an example of a common setting
            Debug.Log("INFO: Target Architecture: " + PlayerSettings.Android.targetArchitectures);
    }

    void CheckForHiddenDuplicates()
    {
        string[] allFiles = Directory.GetFiles("Assets", "*.aar", SearchOption.AllDirectories);
        Dictionary<string, string> foundFiles = new Dictionary<string, string>();

        foreach (var file in allFiles)
        {
            string fileName = Path.GetFileName(file);
            if (foundFiles.ContainsKey(fileName))
                Debug.LogError($"DUPLICATE DETECTED: {fileName} exists in {file} AND {foundFiles[fileName]}. DELETE ONE!");
            else
                foundFiles.Add(fileName, file);
        }
    }

    void CheckTemplateVariables()
    {
        string mainTemplate = "Assets/Plugins/Android/mainTemplate.gradle";
        if (File.Exists(mainTemplate))
        {
            string content = File.ReadAllText(mainTemplate);
            if (!content.Contains("configurations.all") && !content.Contains("resolutionStrategy"))
                Debug.LogWarning("RECOMMENDATION: Add a 'resolutionStrategy' block to force version harmony.");
        }
    }
}