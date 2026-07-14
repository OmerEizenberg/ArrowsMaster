namespace LiftEngine
{
    public enum LiftEngineAdFormat
    {
        Banner = 0,
        Interstitial = 1,
        Rewarded = 2
    }

    public enum LiftEngineMediationPlatform
    {
        None = 0,
        AppLovinMax = 1,
        LevelPlay = 2
    }

    public enum LiftEngineEnvironment
    {
        Staging = 0,
        Production = 1,
        Custom = 2
    }

    public enum LiftEngineInitializationStatus
    {
        NotInitialized = 0,
        Success = 1,
        Failed = 2
    }

    public enum AdPrewarmState
    {
        Idle = 0,
        Optimizing = 1,
        Loading = 2,
        Ready = 3,
        Failed = 4
    }
}
