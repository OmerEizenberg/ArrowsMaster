using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEngine;

/// <summary>
/// Ensures that the mobile build is recognized as a 'Game' by the OS on both Android and iOS.
/// Android: Updates AndroidManifest.xml with android:isGame="true" and android:appCategory="game".
/// iOS: Updates Info.plist with LSApplicationCategoryType = public.app-category.games.
/// </summary>
public class MobileGameOptimizationBuildProcessor : IPostGenerateGradleAndroidProject
{
    // --- Android Implementation ---
    
    // IPostGenerateGradleAndroidProject is called after the Gradle project is generated but before it's built.
    public int callbackOrder => 10; // Run after most common processors

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // For Gradle builds, the manifest is in unityLibrary or launcher. 
        // Unity usually passes the path to the unityLibrary module.
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        
        // Sometimes path is the root, and we need to look into unityLibrary
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(path, "..", "unityLibrary", "src", "main", "AndroidManifest.xml");
        }

        if (!File.Exists(manifestPath))
        {
            Debug.LogError("[MobileGameOptimization] AndroidManifest.xml not found at: " + manifestPath);
            return;
        }

        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(manifestPath);

        XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");

        // Navigate to <application> tag
        XmlNode applicationNode = xmlDoc.SelectSingleNode("/manifest/application", nsManager);

        if (applicationNode != null)
        {
            bool changed = false;

            // Add or update android:isGame="true"
            XmlAttribute isGameAttr = (XmlAttribute)applicationNode.Attributes.GetNamedItem("isGame", "http://schemas.android.com/apk/res/android");
            if (isGameAttr == null)
            {
                isGameAttr = xmlDoc.CreateAttribute("android", "isGame", "http://schemas.android.com/apk/res/android");
                isGameAttr.Value = "true";
                applicationNode.Attributes.Append(isGameAttr);
                changed = true;
            }
            else if (isGameAttr.Value != "true")
            {
                isGameAttr.Value = "true";
                changed = true;
            }

            // Add or update android:appCategory="game"
            XmlAttribute appCategoryAttr = (XmlAttribute)applicationNode.Attributes.GetNamedItem("appCategory", "http://schemas.android.com/apk/res/android");
            if (appCategoryAttr == null)
            {
                appCategoryAttr = xmlDoc.CreateAttribute("android", "appCategory", "http://schemas.android.com/apk/res/android");
                appCategoryAttr.Value = "game";
                applicationNode.Attributes.Append(appCategoryAttr);
                changed = true;
            }
            else if (appCategoryAttr.Value != "game")
            {
                appCategoryAttr.Value = "game";
                changed = true;
            }

            if (changed)
            {
                xmlDoc.Save(manifestPath);
                Debug.Log("[MobileGameOptimization] AndroidManifest.xml updated with 'isGame=true' and 'appCategory=game'.");
            }
        }
        else
        {
            Debug.LogError("[MobileGameOptimization] Could not find <application> tag in AndroidManifest.xml");
        }
    }

    // --- iOS Implementation ---

    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

#if UNITY_IOS
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Set LSApplicationCategoryType to public.app-category.games
        plist.root.SetString("LSApplicationCategoryType", "public.app-category.games");

        // Add GCSupportedGameControllers (as an empty array) to further signal it's a game
        if (!plist.root.values.ContainsKey("GCSupportedGameControllers"))
        {
            plist.root.CreateArray("GCSupportedGameControllers");
        }

        // Save the changes
        plist.WriteToFile(plistPath);
        Debug.Log("[MobileGameOptimization] Info.plist updated with LSApplicationCategoryType = public.app-category.games");
#endif
    }
}
