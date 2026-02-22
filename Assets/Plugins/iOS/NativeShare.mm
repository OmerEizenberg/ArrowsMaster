#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" {
    void _NativeShare_Share(const char* text, const char* url) {
        NSString *shareText = [NSString stringWithUTF8String:text];
        NSString *shareURLStr = [NSString stringWithUTF8String:url];
        
        NSMutableArray *items = [NSMutableArray arrayWithObject:shareText];
        if (shareURLStr.length > 0) {
            NSURL *shareURL = [NSURL URLWithString:shareURLStr];
            if (shareURL) {
                [items addObject:shareURL];
            }
        }
        
        UIActivityViewController *activityViewController = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        
        UIViewController *rootViewController = [UIApplication sharedApplication].keyWindow.rootViewController;
        
        // For iPad, we need to provide a source view
        if (UI_USER_INTERFACE_IDIOM() == UIUserInterfaceIdiomPad) {
            activityViewController.popoverPresentationController.sourceView = rootViewController.view;
            activityViewController.popoverPresentationController.sourceRect = CGRectMake(rootViewController.view.bounds.size.width/2, rootViewController.view.bounds.size.height/2, 0, 0);
            activityViewController.popoverPresentationController.permittedArrowDirections = 0;
        }
        
        [rootViewController presentViewController:activityViewController animated:YES completion:nil];
    }
}
