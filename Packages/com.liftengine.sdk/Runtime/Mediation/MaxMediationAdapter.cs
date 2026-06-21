using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiftEngine.Mediation
{
    internal sealed class MaxMediationAdapter : IMediationAdapter
    {
        private readonly Dictionary<LiftEngineAdFormat, FormatState> _states = new();
        private LiftEngineSettings _settings;
        private bool _bannerCreated;

        private sealed class FormatState
        {
            public bool Loaded;
            public double LastRevenue;
            public string NetworkName;
            public string MaxPlacement;
        }

        public LiftEngineMediationPlatform Platform => LiftEngineMediationPlatform.AppLovinMax;
        public bool IsInitialized { get; private set; }

        public event Action<MediationAdInfo> AdLoaded;
        public event Action<MediationAdError> AdLoadFailed;
        public event Action<MediationAdInfo> AdDisplayed;
        public event Action<MediationAdError> AdDisplayFailed;
        public event Action<MediationAdInfo> AdHidden;
        public event Action<MediationAdInfo> AdClicked;
        public event Action<MediationAdInfo> AdRevenuePaid;
        public event Action<MediationAdInfo> AdRewarded;

        private bool _callbacksSubscribed;

        public void Initialize(LiftEngineSettings settings, Action<bool> onComplete)
        {
            _settings = settings;

            if (IsInitialized)
            {
                onComplete?.Invoke(true);
                return;
            }

            if (!_callbacksSubscribed)
            {
                SubscribeCallbacks();
                _callbacksSubscribed = true;
            }

            void Complete()
            {
                IsInitialized = true;
                onComplete?.Invoke(true);
            }

            if (MaxSdk.IsInitialized())
            {
                Complete();
                return;
            }

            MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxSdkInitialized;

            void OnMaxSdkInitialized(MaxSdkBase.SdkConfiguration config)
            {
                MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxSdkInitialized;
                Complete();
            }

            LiftEngineLogger.Log(
                "Waiting for AppLovin MAX to initialize. Call MaxSdk.InitializeSdk() in your game before LiftEngineSdk.Initialize().");
        }

        private void SubscribeCallbacks()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (id, info) => HandleLoaded(LiftEngineAdFormat.Interstitial, id, info);
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (id, err) => HandleLoadFailed(LiftEngineAdFormat.Interstitial, id, err);
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (id, info) => AdDisplayed?.Invoke(ToInfo(LiftEngineAdFormat.Interstitial, id, info));
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (id, err, info) =>
                AdDisplayFailed?.Invoke(new MediationAdError { Format = LiftEngineAdFormat.Interstitial, AdUnitId = id, Code = (int)err.Code, Message = err.Message });
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (id, info) => AdHidden?.Invoke(ToInfo(LiftEngineAdFormat.Interstitial, id, info));
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += (id, info) => AdClicked?.Invoke(ToInfo(LiftEngineAdFormat.Interstitial, id, info));
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (id, info) => HandleRevenue(LiftEngineAdFormat.Interstitial, id, info);

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (id, info) => HandleLoaded(LiftEngineAdFormat.Rewarded, id, info);
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (id, err) => HandleLoadFailed(LiftEngineAdFormat.Rewarded, id, err);
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (id, info) => AdDisplayed?.Invoke(ToInfo(LiftEngineAdFormat.Rewarded, id, info));
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (id, err, info) =>
                AdDisplayFailed?.Invoke(new MediationAdError { Format = LiftEngineAdFormat.Rewarded, AdUnitId = id, Code = (int)err.Code, Message = err.Message });
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (id, info) => AdHidden?.Invoke(ToInfo(LiftEngineAdFormat.Rewarded, id, info));
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += (id, info) => AdClicked?.Invoke(ToInfo(LiftEngineAdFormat.Rewarded, id, info));
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (id, info) => HandleRevenue(LiftEngineAdFormat.Rewarded, id, info);
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, reward, info) => AdRewarded?.Invoke(ToInfo(LiftEngineAdFormat.Rewarded, id, info));

            MaxSdkCallbacks.Banner.OnAdLoadedEvent += (id, info) => HandleLoaded(LiftEngineAdFormat.Banner, id, info);
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (id, err) => HandleLoadFailed(LiftEngineAdFormat.Banner, id, err);
            MaxSdkCallbacks.Banner.OnAdClickedEvent += (id, info) => AdClicked?.Invoke(ToInfo(LiftEngineAdFormat.Banner, id, info));
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (id, info) => HandleRevenue(LiftEngineAdFormat.Banner, id, info);
        }

        public void AddPayload(LiftEngineAdFormat format, string adUnitId, string payloadKey, string payloadValue)
        {
            if (string.IsNullOrEmpty(payloadKey))
            {
                LiftEngineLogger.LogWarning($"Missing payload key for {format}; skipping mediation payload.");
                return;
            }

            switch (format)
            {
                case LiftEngineAdFormat.Interstitial:
                    MaxSdk.SetInterstitialLocalExtraParameter(adUnitId, payloadKey, payloadValue);
                    break;
                case LiftEngineAdFormat.Rewarded:
                    MaxSdk.SetRewardedAdLocalExtraParameter(adUnitId, payloadKey, payloadValue);
                    break;
                case LiftEngineAdFormat.Banner:
                    if (_bannerCreated)
                        MaxSdk.DestroyBanner(adUnitId);
                    _bannerCreated = false;
                    MaxSdk.SetBannerExtraParameter(adUnitId, payloadKey, payloadValue);
                    break;
            }
        }

        public void ClearPayload(LiftEngineAdFormat format, string adUnitId, string payloadKey)
        {
            AddPayload(format, adUnitId, payloadKey, "0");
        }

        public void RequestLoad(LiftEngineAdFormat format, string adUnitId, string maxPlacement = null)
        {
            ResetLoadState(format, adUnitId);
            SetMaxPlacement(format, maxPlacement);

            switch (format)
            {
                case LiftEngineAdFormat.Interstitial:
                    MaxSdk.LoadInterstitial(adUnitId);
                    break;
                case LiftEngineAdFormat.Rewarded:
                    MaxSdk.LoadRewardedAd(adUnitId);
                    break;
                case LiftEngineAdFormat.Banner:
                    EnsureBannerCreated(adUnitId, ResolveMaxPlacement(format, maxPlacement));
                    break;
            }
        }

        public bool IsReady(LiftEngineAdFormat format, string adUnitId)
        {
            return format switch
            {
                LiftEngineAdFormat.Interstitial => MaxSdk.IsInterstitialReady(adUnitId),
                LiftEngineAdFormat.Rewarded => MaxSdk.IsRewardedAdReady(adUnitId),
                LiftEngineAdFormat.Banner => _bannerCreated && GetState(format).Loaded,
                _ => false
            };
        }

        public bool HasLoadedWithRevenue(LiftEngineAdFormat format, string adUnitId)
        {
            if (!IsReady(format, adUnitId))
                return false;

            if (!_states.TryGetValue(format, out var state) || !state.Loaded)
                return false;

            return state.LastRevenue > 0d;
        }

        public void Show(LiftEngineAdFormat format, string adUnitId, string maxPlacement = null)
        {
            var placement = ResolveMaxPlacement(format, maxPlacement);

            switch (format)
            {
                case LiftEngineAdFormat.Interstitial:
                    if (!string.IsNullOrEmpty(placement))
                        MaxSdk.ShowInterstitial(adUnitId, placement);
                    else
                        MaxSdk.ShowInterstitial(adUnitId);
                    break;
                case LiftEngineAdFormat.Rewarded:
                    if (!string.IsNullOrEmpty(placement))
                        MaxSdk.ShowRewardedAd(adUnitId, placement);
                    else
                        MaxSdk.ShowRewardedAd(adUnitId);
                    break;
                case LiftEngineAdFormat.Banner:
                    EnsureBannerCreated(adUnitId, placement);
                    MaxSdk.ShowBanner(adUnitId);
                    AdDisplayed?.Invoke(new MediationAdInfo
                    {
                        Format = LiftEngineAdFormat.Banner,
                        AdUnitId = adUnitId
                    });
                    break;
            }
        }

        public void HideBanner(string adUnitId)
        {
            if (_bannerCreated)
                MaxSdk.HideBanner(adUnitId);
        }

        public void ResetLoadState(LiftEngineAdFormat format, string adUnitId)
        {
            var previousPlacement = GetState(format).MaxPlacement;
            _states[format] = new FormatState
            {
                MaxPlacement = previousPlacement
            };
        }

        public void DestroyAd(LiftEngineAdFormat format, string adUnitId)
        {
            switch (format)
            {
                case LiftEngineAdFormat.Banner:
                    if (_bannerCreated)
                    {
                        MaxSdk.DestroyBanner(adUnitId);
                        _bannerCreated = false;
                    }
                    break;
                // Interstitial/rewarded: next Load replaces inventory; reset state only.
            }

            ResetLoadState(format, adUnitId);
        }

        private void EnsureBannerCreated(string adUnitId, string maxPlacement = null)
        {
            if (_bannerCreated)
                return;

            MaxSdk.CreateBanner(adUnitId, MaxSdkBase.BannerPosition.BottomCenter);
            if (!string.IsNullOrEmpty(maxPlacement))
                MaxSdk.SetBannerPlacement(adUnitId, maxPlacement);
            MaxSdk.SetBannerBackgroundColor(adUnitId, Color.clear);
            _bannerCreated = true;
        }

        private void HandleLoaded(LiftEngineAdFormat format, string adUnitId, MaxSdkBase.AdInfo info)
        {
            var state = GetState(format);
            state.Loaded = true;
            state.LastRevenue = info?.Revenue ?? 0d;
            state.NetworkName = info?.NetworkName;
            AdLoaded?.Invoke(ToInfo(format, adUnitId, info));
        }

        private void HandleLoadFailed(LiftEngineAdFormat format, string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            GetState(format).Loaded = false;
            AdLoadFailed?.Invoke(new MediationAdError
            {
                Format = format,
                AdUnitId = adUnitId,
                Code = (int)err.Code,
                Message = err.Message
            });
        }

        private void HandleRevenue(LiftEngineAdFormat format, string adUnitId, MaxSdkBase.AdInfo info)
        {
            var state = GetState(format);
            state.LastRevenue = info?.Revenue ?? state.LastRevenue;
            AdRevenuePaid?.Invoke(ToInfo(format, adUnitId, info));
        }

        private void SetMaxPlacement(LiftEngineAdFormat format, string maxPlacement)
        {
            GetState(format).MaxPlacement = string.IsNullOrEmpty(maxPlacement) ? null : maxPlacement;
        }

        private string ResolveMaxPlacement(LiftEngineAdFormat format, string maxPlacement)
        {
            if (!string.IsNullOrEmpty(maxPlacement))
                return maxPlacement;

            return GetState(format).MaxPlacement;
        }

        private FormatState GetState(LiftEngineAdFormat format)
        {
            if (!_states.TryGetValue(format, out var state))
            {
                state = new FormatState();
                _states[format] = state;
            }
            return state;
        }

        private static MediationAdInfo ToInfo(LiftEngineAdFormat format, string adUnitId, MaxSdkBase.AdInfo info)
        {
            return new MediationAdInfo
            {
                Format = format,
                AdUnitId = adUnitId,
                NetworkName = info?.NetworkName,
                Revenue = info?.Revenue ?? 0d,
                AdFormat = info?.AdFormat
            };
        }
    }
}
