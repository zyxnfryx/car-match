#import <UIKit/UIKit.h>
// 0 = light impact, 1 = medium impact, 2 = success notification.
extern "C" void LotLoopHaptic(int style)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (style == 2) {
            UINotificationFeedbackGenerator *n = [[UINotificationFeedbackGenerator alloc] init];
            [n notificationOccurred:UINotificationFeedbackTypeSuccess];
            return;
        }
        UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:(style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleLight)];
        [g prepare];
        [g impactOccurred];
    });
}
