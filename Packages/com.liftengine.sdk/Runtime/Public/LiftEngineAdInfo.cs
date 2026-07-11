namespace LiftEngine
{
    public sealed class LiftEngineAdInfo
    {
        public LiftEngineAdFormat Format { get; internal set; }
        public string AdUnitId { get; internal set; }
        public string NetworkName { get; internal set; }
        public double Revenue { get; internal set; }
        public string AdFormat { get; internal set; }
        public string RevenuePrecision { get; internal set; }
        public string MaxPlacement { get; internal set; }

        internal static LiftEngineAdInfo FromMediation(Mediation.MediationAdInfo info)
        {
            if (info == null)
                return null;

            return new LiftEngineAdInfo
            {
                Format = info.Format,
                AdUnitId = info.AdUnitId,
                NetworkName = info.NetworkName,
                Revenue = info.Revenue,
                AdFormat = info.AdFormat,
                RevenuePrecision = info.RevenuePrecision,
                MaxPlacement = info.MaxPlacement
            };
        }
    }
}
