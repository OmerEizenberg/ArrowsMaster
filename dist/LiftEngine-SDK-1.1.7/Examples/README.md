# Example LiftEngineSettings

This sample is a blank `LiftEngineSettings` ScriptableObject. Import it, then copy the asset to:

`Assets/Resources/LiftEngineSettings.asset`

That path is required — `Resources.Load("LiftEngineSettings")` will not find it anywhere else.

Fill in:

- LiftEngine **API key** (never use a production key in git)
- MAX ad unit IDs for iOS and Android
- **Environment** = Staging for QA, Production for store builds
- **Auto Initialize** = Off

You can also create the asset from **Window → LiftEngine → Integration Manager → Create Settings Asset**.
