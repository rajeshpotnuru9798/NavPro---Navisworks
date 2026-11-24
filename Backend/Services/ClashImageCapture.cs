using System;
using System.Drawing;
using System.IO;

namespace NavPro.Backend.Services
{
    /// <summary>Bounds finalizable native image wrappers retained by the Clash API during large exports.</summary>
    internal sealed class ClashImageCapture : IDisposable
    {
        internal const int CleanupInterval = 128;
        private readonly Action reclaim;
        private int pending;
        private bool started;
        public int RecoveredImages { get; private set; }

        internal ClashImageCapture(Action reclaim = null)
        {
            // API 21 TestsImageForResult and its conversion helper leave internal image/DIB
            // wrappers for finalization. Disposing only the returned bitmap is insufficient.
            // Keep this workaround local to bulk image export; never use private SDK handles.
            this.reclaim = reclaim ?? (() => { GC.Collect(); GC.WaitForPendingFinalizers(); });
        }

        public string Capture(Func<Bitmap> render, out string note)
        {
            if (render == null) throw new ArgumentNullException(nameof(render));
            if (!started || pending >= CleanupInterval) Reclaim();
            started = true;
            note = null;
            try
            {
                try { return Encode(render); }
                catch (InvalidOperationException)
                {
                    // A previous export can already have exhausted the process GDI quota.
                    // Retry this same result once, after reclaiming finalizable SDK resources.
                    Reclaim();
                    try { string data = Encode(render); RecoveredImages++; return data; }
                    catch (Exception ex) { note = "Image unavailable after resource cleanup and retry: " + ex.Message; return null; }
                }
                catch (Exception ex) { note = "Image unavailable: " + ex.Message; return null; }
            }
            finally { pending++; }
        }

        private static string Encode(Func<Bitmap> render)
        {
            using (var bitmap = render())
            {
                if (bitmap == null) throw new InvalidOperationException("Navisworks returned no image for this clash.");
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    return Convert.ToBase64String(stream.ToArray());
                }
            }
        }

        private void Reclaim() { reclaim(); pending = 0; }
        public void Dispose() { if (started) { Reclaim(); started = false; } }
    }
}
