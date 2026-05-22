namespace Assets.Scripts.LiveOps.Missions
{
    public static class MissionDescriptions
    {
        public static string GetDescription(MissionDefinition definition)
        {
            if (definition == null) return string.Empty;
            if (!string.IsNullOrEmpty(definition.DescriptionOverride))
                return definition.DescriptionOverride;

            switch (definition.Type)
            {
                case MissionType.CompleteLevels:
                    return $"Complete {definition.TargetCount} Levels";
                case MissionType.WatchAds:
                    return $"Watch {definition.TargetCount} Ads";
                case MissionType.WinLevelsInARow:
                    return $"Win {definition.TargetCount} Levels in a Row";
                case MissionType.MakePurchase:
                    return definition.TargetCount <= 1 ? "Make a Purchase" : $"Make {definition.TargetCount} Purchases";
                case MissionType.CompleteChallengeLevels:
                    return $"Complete {definition.TargetCount} Challenge Levels";
                default:
                    return "Complete the task";
            }
        }
    }
}
