namespace Unity.Services.LevelPlay.Editor
{
    interface IEditorAnalyticsSender
    {
        void SendEventWithLimit(string eventName, object body, int version);
    }
}
