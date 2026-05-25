#import <Foundation/Foundation.h>
#import <objc/runtime.h>

// Mitigates Unity Ads SDK 4.18.x crashes in NetworkTransactionDiagnosticAdapter.convertToNetworkMetricType
// when iOS reports URLSession metric values the SDK does not handle (EXC_BREAKPOINT on NSURLSession-delegate).

static NSURLSessionTaskMetricsResourceFetchType (*g_originalResourceFetchType)(id, SEL);

static NSURLSessionTaskMetricsResourceFetchType guarded_resourceFetchType(id self, SEL _cmd)
{
    NSURLSessionTaskMetricsResourceFetchType type = g_originalResourceFetchType(self, _cmd);
    switch (type)
    {
        case NSURLSessionTaskMetricsResourceFetchTypeNetworkLoad:
        case NSURLSessionTaskMetricsResourceFetchTypeServerPush:
        case NSURLSessionTaskMetricsResourceFetchTypeLocalCache:
            return type;
        default:
            return NSURLSessionTaskMetricsResourceFetchTypeNetworkLoad;
    }
}

static NSArray * (*g_originalNetworkCapabilityTransports)(id, SEL);

static NSArray *guarded_networkCapabilityTransports(id self, SEL _cmd)
{
    if (g_originalNetworkCapabilityTransports == NULL)
        return @[];

    NSArray *transports = g_originalNetworkCapabilityTransports(self, _cmd);
    return transports != nil ? transports : @[];
}

static void swizzleInstanceMethod(Class cls, SEL selector, IMP newIMP, IMP *originalIMPStorage)
{
    Method method = class_getInstanceMethod(cls, selector);
    if (method == NULL)
    {
        return;
    }

    *originalIMPStorage = method_getImplementation(method);
    method_setImplementation(method, newIMP);
}

@interface UnityAdsNetworkMetricsCrashGuard : NSObject
@end

@implementation UnityAdsNetworkMetricsCrashGuard

+ (void)load
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Class metricsClass = NSClassFromString(@"NSURLSessionTaskTransactionMetrics");
        if (metricsClass == Nil)
        {
            return;
        }

        swizzleInstanceMethod(metricsClass,
                              @selector(resourceFetchType),
                              (IMP)guarded_resourceFetchType,
                              (IMP *)&g_originalResourceFetchType);

        SEL transportsSelector = NSSelectorFromString(@"networkCapabilityTransports");
        if (transportsSelector != NULL && [metricsClass instancesRespondToSelector:transportsSelector])
        {
            swizzleInstanceMethod(metricsClass,
                                  transportsSelector,
                                  (IMP)guarded_networkCapabilityTransports,
                                  (IMP *)&g_originalNetworkCapabilityTransports);
        }

        NSLog(@"[UnityAdsNetworkMetricsCrashGuard] Installed URLSession metrics guards.");
    });
}

@end
