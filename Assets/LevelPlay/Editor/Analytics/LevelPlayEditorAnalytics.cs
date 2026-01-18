using System;
using System.Collections.Generic;

namespace Unity.Services.LevelPlay.Editor
{
    class EditorAnalyticsService : IEditorAnalyticsService
    {
        const string k_EventName = "editorgameserviceeditor";
        const int k_EventVersion = 1;
        const string k_ServicesCorePackageName = "com.unity.services.core";

        private readonly IEditorAnalyticsSender m_EditorAnalyticsSender;
        private readonly string m_PackageVersion = Constants.k_AnnotatedPackageVersion;
        private readonly Queue<QueuedEvent> m_EventsQueue = new Queue<QueuedEvent>();
        private bool m_ServicesCoreIsReady;

        public void Initialize()
        {
            LevelPlayPackmanQuerier.instance.CheckIfPackageIsInstalledWithUpm(k_ServicesCorePackageName,
                coreIsInstalled =>
                {
                    SetServicesCoreIsReady(coreIsInstalled);
                });
        }

        internal void SetServicesCoreIsReady(bool isReady)
        {
            m_ServicesCoreIsReady = isReady;
            if (m_ServicesCoreIsReady)
            {
                while (m_EventsQueue.Count > 0)
                {
                    var eventEntry = m_EventsQueue.Dequeue();
                    SendEventWithBody(eventEntry.Name, eventEntry.Body);
                }
            }
        }

        internal EditorAnalyticsService(IEditorAnalyticsSender editorAnalyticsSender)
        {
            m_EditorAnalyticsSender = editorAnalyticsSender;
        }

        public void SendEventEditorClick(string component, string action)
        {
            SendEvent(k_EventName,
                new EventBody()
                {
                    component = component,
                    action = action,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendInstallAdapterEvent(string adapterName,
            string newVersion, string currentVersion)
        {
            SendEvent(k_EventName,
                new EventBody()
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.Install + "_" + adapterName.Replace("_", "-") + "_" + newVersion,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendUpdateAdapterEvent(string adapterName,
            string newVersion, string currentVersion)
        {
            SendEvent(k_EventName,
                new EventBody()
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.Update + "_" + adapterName.Replace("_", "-") + "_" + newVersion,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendUninstallAdapterEvent(string adapterName, string currentVersion)
        {
            SendEvent(k_EventName,
                new EventBody()
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.Uninstall + "_" + adapterName.Replace("_", "-") + "_" + currentVersion,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendUpdateAllAdaptersEvent()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.UpdateAllAdapters,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendNewSession(string packageType)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = packageType,
                    action = LevelPlayAction.NewSession,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendInstallPackage(string component)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.Install,
                    component = component,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendInstallLPSDKEvent(string newVersion)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.Install + "_Ironsource_" + newVersion,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendUpdateLPSDKEvent(string newVersion, string currentVersion)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = LevelPlayAction.Update + "_Ironsource_" + newVersion,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion,
                });
        }

        public void SendMdrEvent(string action)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = action,
                    component = LevelPlayComponent.MDR,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendCreateApps()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.CreateApps,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendUpdateAll()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.UpdateAll,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendCopyAppKey()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.CopyAppKey,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendCopyAdUnit()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.CopyAdUnitId,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendNavigateApps()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.NavigateAppKeys,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendCloseSettings()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.CloseSettings,
                    component = LevelPlayComponent.ProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendProjectBound()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.ProjectBound,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendProjectNotBound()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.ProjectNotBound,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendProjectNotMapped()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.ProjectNotMapped,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendUserUnauthorizedToCreate()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.UserDenyCreate,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendUserUnauthorizedToRead()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.UserDenyView,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendUpdateAvailable()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.DisplayUpdateAll,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendCreateAppsButtonDisplayed()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.DisplayCreateApps,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendFailedToCreateApps()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.FailCreateApp,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendFailedToCreateAdUnits()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.FailCreateAdUnit,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendAdUnitsAvailable()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.AdUnitsAvailable,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendAdUnitsNotAvailable()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.AdUnitsNotAvailable,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendAppsNotAvailable()
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = LevelPlayAction.AppsNotAvailable,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendOpenDashboard(string action)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    action = action,
                    component = LevelPlayComponent.SystemProjectSettings,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendInteractWithSkanIdCheckBox(bool action)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.LevelPlayNetworkManager,
                    action = action ? LevelPlayAction.EnableSkAdNetworkId : LevelPlayAction.DisableSkAdNetworkId,
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendFailedToAddSkAdNetworkId(string adapterName)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.PostBuild,
                    action = $"{LevelPlayAction.FailedToAddSkanId}_{adapterName}",
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        public void SendInstantiateGameObject(string adFormat)
        {
            SendEvent(k_EventName,
                new EventBody
                {
                    component = LevelPlayComponent.GameObject,
                    action = $"{LevelPlayAction.Instantiate}_{adFormat}",
                    package = Constants.k_PackageAnalyticsIdentifier,
                    package_ver = m_PackageVersion
                });
        }

        private void SendEvent(string eventName, EventBody body)
        {
            if (!m_ServicesCoreIsReady)
            {
                m_EventsQueue.Enqueue(new QueuedEvent {Name = eventName, Body = body, });
            }
            else
            {
                SendEventWithBody(eventName, body);
            }
        }

        public void SendEventWithBody(string eventName, object body)
        {
            m_EditorAnalyticsSender.SendEventWithLimit(eventName, body, k_EventVersion);
        }

        internal static class LevelPlayComponent
        {
            public const string TopMenuAdsMediation = "TopMenu_AdsMediation";
            public const string ServicesMenuAdsMediation = "ServicesMenu_AdsMediation";
            public const string TopMenuDeveloperSettings = "TopMenu_DeveloperSettings";
            public const string ServicesMenuDeveloperSettings = "ServicesMenu_DeveloperSettings";
            public const string LevelPlayNetworkManager = "LevelPlay_Network_Manager";

            public const string MDR = "MDR";

            public const string UpmPackage = "upm";
            public const string UnityPackage = ".unitypackage";

            public const string PostBuild = "Post_Build";

            public const string GameObject = "GameObject";

            public const string ProjectSettings = "Project_Settings";
            public const string SystemProjectSettings = "System_Project_Settings";
        }

        internal static class LevelPlayAction
        {
            public const string OpenAppsAndAdUnits = "Open_AppsAndAdUnits";
            public const string OpenChangelog = "Open_SDKChangelog";
            public const string OpenLevelPlayMediationSettings = "Open_LevelPlayMediationSettings";
            public const string OpenMediatedNetworkSettings = "Open_MediatedNetworkSettings";
            public const string OpenNetworkManager = "Open_NetworkManager";
            public const string OpenDocumentation = "Open_Documentation";
            public const string OpenTroubleShootingGuide = "Open_TroubleShootingGuide";

            public const string ClickButton_UpdatePackage = "ClickButton_UpdatePackage";

            public const string NewSession = "NewSession";
            public const string Install = "Install";
            public const string Update = "Update";
            public const string Uninstall = "Uninstall";
            public const string UpdateAllAdapters = "UpdateAllAdapters";

            public const string EnableSkAdNetworkId = "Enable_SkAdNetworkId";
            public const string DisableSkAdNetworkId = "Disable_SkAdNetworkId";

            public const string FailedToAddSkanId = "FailedToAddSkanId";

            public const string DragAndDrop = "DragAndDrop";
            public const string Instantiate = "Instantiate";

            public const string MDRWindowDisplayed = "Display_Import_Window";
            public const string MDRImport = "Click_Import";
            public const string MDRIgnore = "Click_Ignore";
            public const string MDRCancel = "Click_Cancel";

            public const string CreateApps = "Create_Apps";
            public const string UpdateAll = "Update_All";
            public const string CopyAppKey = "Copy_AppKey";
            public const string CopyAdUnitId = "Copy_AdUnitId";
            public const string NavigateAppKeys = "Navigate_AppKeys";
            public const string CloseSettings = "Close_Settings";

            public const string ProjectBound = "Project_Bound";
            public const string ProjectNotBound = "Project_Not_Bound";
            public const string UserDenyCreate = "User_Deny_Create";
            public const string UserDenyView = "User_Deny_View";
            public const string DisplayUpdateAll = "Display_Update_All";
            public const string DisplayCreateApps = "Display_Create_Apps";
            public const string FailCreateApp = "Fail_Create_App";
            public const string FailCreateAdUnit = "Fail_Create_AdUnit";

            public const string AdUnitsAvailable = "Ad_Units_Available";
            public const string AdUnitsNotAvailable = "Ad_Units_Not_Available";
            public const string AppsNotAvailable = "Apps_Not_Available";
            public const string ProjectNotMapped = "Project_Not_Mapped";

            public const string OpenDashboardNotMapped = "Open_Dashboard_Not_Mapped";
            public const string OpenDashboardWithNoApps = "Open_Dashboard_With_No_Apps";
            public const string OpenDashboardWithNoAdUnits = "Open_Dashboard_With_No_AdUnits";

        }
    }

    internal class QueuedEvent
    {
        internal string Name;
        internal object Body;
    }

    [Serializable]
    internal class EventBody
    {
        public string action;
        public string component;
        public string package;
        public string package_ver;
    }

    internal class EventBodyComparer : IEqualityComparer<EventBody>
    {
        public bool Equals(EventBody one, EventBody two)
        {
            if (object.ReferenceEquals(one, two))
            {
                return true;
            }

            if (one == null || two == null)
            {
                return false;
            }

            if (!one.action.Equals(two.action))
            {
                return false;
            }

            if (!one.component.Equals(two.component))
            {
                return false;
            }

            if (!one.package.Equals(two.package))
            {
                return false;
            }

            if (!one.package_ver.Equals(two.package_ver))
            {
                return false;
            }

            return true;
        }

        public int GetHashCode(EventBody eventBody)
        {
            return eventBody.action.GetHashCode() ^ eventBody.component.GetHashCode() ^
                eventBody.package.GetHashCode() ^ eventBody.package_ver.GetHashCode();
        }
    }
}
