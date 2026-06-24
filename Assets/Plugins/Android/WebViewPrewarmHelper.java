package com.everybodygames.arrowsmaster;

import android.app.Activity;
import android.os.Looper;
import android.webkit.WebView;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Prewarms System WebView on the Android UI thread. Unity JNI calls can execute on the render
 * thread (nativeRender), so WebView must never be constructed from C# directly.
 */
public final class WebViewPrewarmHelper {
    private static final long PREWARM_TIMEOUT_SECONDS = 5L;

    private WebViewPrewarmHelper() {
    }

    public static boolean prewarm(Activity activity) {
        if (activity == null) {
            return false;
        }

        if (Looper.myLooper() == Looper.getMainLooper()) {
            return prewarmOnUiThread(activity);
        }

        final CountDownLatch latch = new CountDownLatch(1);
        final AtomicBoolean result = new AtomicBoolean(false);

        activity.runOnUiThread(() -> {
            try {
                result.set(prewarmOnUiThread(activity));
            } finally {
                latch.countDown();
            }
        });

        try {
            if (!latch.await(PREWARM_TIMEOUT_SECONDS, TimeUnit.SECONDS)) {
                return false;
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return false;
        }

        return result.get();
    }

    private static boolean prewarmOnUiThread(Activity activity) {
        WebView webView = null;
        try {
            webView = new WebView(activity);
            return true;
        } catch (Exception ignored) {
            return false;
        } finally {
            if (webView != null) {
                webView.destroy();
            }
        }
    }
}
