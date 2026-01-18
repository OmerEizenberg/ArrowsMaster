using UnityEditor;
using System.Threading.Tasks;
using Unity.Services.LevelPlay.Editor.Analytics;

namespace Unity.Services.LevelPlay.Editor
{
    class OnLoadService : IOnLoadService
    {
        [InitializeOnLoadMethod]
        static async Task InitializeOnLoad()
        {
            var onLoadService = EditorServices.Instance.OnLoadService;
            await onLoadService.OnLoad();
        }

        readonly IEditorAnalyticsService m_EditorAnalyticsService;
        readonly ILevelPlaySdkInstaller m_LevelPlaySdkInstaller;

        internal OnLoadService(IEditorAnalyticsService editorAnalyticsService, ILevelPlaySdkInstaller levelPlaySdkInstaller)
        {
            m_EditorAnalyticsService = editorAnalyticsService;
            m_LevelPlaySdkInstaller = levelPlaySdkInstaller;
        }

        public async Task OnLoad()
        {
            EditorSessionTracker.NewSession();
            m_EditorAnalyticsService.Initialize();
            ServicesCoreDependencyInstaller.InstallServicesCoreIfNotFound();
            MobileDependencyResolverInstaller.InstallPlayServicesResolverIfNeeded();
            EnvironmentVariables.BuildManifestPath();

            await m_LevelPlaySdkInstaller.OnLoad();
        }
    }
}
