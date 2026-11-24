using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using NavPro.Backend.Services;

internal static partial class Program
{
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flag);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);

    // Models the opaque, finalizable native DIB retained inside TestsImageForResult.
    // The returned managed bitmap is a separate resource, exactly as in the inspected API 21 IL.
    private sealed class DeferredImageHandle : System.Runtime.InteropServices.SafeHandle
    {
        internal DeferredImageHandle() : base(IntPtr.Zero, true)
        { using (var image = new Bitmap(8, 8)) SetHandle(image.GetHbitmap()); }
        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle() => DeleteObject(handle);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Bitmap DeferredImage()
    {
        var hidden = new DeferredImageHandle();
        var result = new Bitmap(8, 8);
        GC.KeepAlive(hidden);
        return result;
    }

    private static void DashboardImages()
    {
        int cleanups = 0, calls = 0;
        using (var capture = new ClashImageCapture(() => cleanups++))
        {
            Bitmap returned = null;
            string image = capture.Capture(() => { calls++; returned = new Bitmap(12, 9); return returned; }, out string note);
            using (var stream = new MemoryStream(Convert.FromBase64String(image)))
            using (var decoded = Image.FromStream(stream))
                Check(decoded.Width == 12 && decoded.Height == 9 && note == null, "clash capture creates a decodable JPEG");
            bool disposed = false; try { int width = returned.Width; } catch (ArgumentException) { disposed = true; }
            Check(disposed, "returned native bitmap is disposed after encoding");
            for (int i = 1; i <= ClashImageCapture.CleanupInterval; i++) capture.Capture(() => new Bitmap(2, 2), out _);
            Check(cleanups == 2, "native image resources reclaimed initially and between bounded batches");
            calls = 0;
            image = capture.Capture(() => { if (calls++ == 0) throw new InvalidOperationException("quota"); return new Bitmap(3, 3); }, out note);
            Check(calls == 2 && image != null && note == null && capture.RecoveredImages == 1, "transient native state failure retries the same capture after cleanup");
            calls = 0;
            image = capture.Capture(() => { calls++; throw new InvalidOperationException("still unavailable"); }, out note);
            Check(calls == 2 && image == null && note.Contains("still unavailable") && note.Contains("retry"), "permanent image failure is bounded and retains the native error");
            calls = 0;
            image = capture.Capture(() => { calls++; throw new IOException("storage"); }, out note);
            Check(calls == 1 && image == null && note.Contains("storage"), "unrelated failures are not blindly retried");
            image = capture.Capture(() => null, out note);
            Check(image == null && note.Contains("no image"), "null native image never becomes a misleading thumbnail");
        }
        Check(cleanups >= 6, "remaining native wrappers reclaimed when capture scope closes");

        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            int baseline = GetGuiResources(process.Handle, 0), peak = baseline;
            using (var capture = new ClashImageCapture())
            {
                for (int i = 0; i < 34662; i++)
                {
                    string image = capture.Capture(DeferredImage, out string note);
                    if (image == null || note != null) throw new Exception("Large image capture failed at " + i + ": " + note);
                    if (i % 64 == 0) peak = Math.Max(peak, GetGuiResources(process.Handle, 0));
                }
            }
            int remaining = GetGuiResources(process.Handle, 0);
            Check(peak < baseline + 256 && remaining <= baseline + 8, "34,662 simulated opaque DIB captures keep GDI handles bounded and release the final batch");
        }
    }
}
