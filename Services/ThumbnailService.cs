using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ArchiveViewer.Services;

public static class ThumbnailService
{
    private const int MaxThumbPixels = 960;
    private const int JpegQuality = 85;

    // Background color for compositing transparent images (matches theme #1e1e2e)
    private static readonly byte BgB = 46, BgG = 30, BgR = 30;

    public static byte[]? GenerateThumbnailBytes(byte[] imageData)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            double scale = Math.Min((double)MaxThumbPixels / frame.PixelWidth, (double)MaxThumbPixels / frame.PixelHeight);
            if (scale > 1) scale = 1;

            BitmapSource source = frame;
            if (Math.Abs(scale - 1.0) > 0.01)
                source = new TransformedBitmap(frame, new ScaleTransform(scale, scale));

            // If image has alpha channel, composite onto dark background using pixel manipulation
            // (avoids RenderTargetBitmap/DrawingVisual which require STA thread)
            if (source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32)
            {
                var w = source.PixelWidth;
                var h = source.PixelHeight;
                var stride = w * 4;
                var pixels = new byte[stride * h];
                source.CopyPixels(pixels, stride, 0);

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int a = pixels[i + 3];
                    if (a == 255) continue;
                    if (a == 0)
                    {
                        pixels[i] = BgB; pixels[i + 1] = BgG; pixels[i + 2] = BgR; pixels[i + 3] = 255;
                    }
                    else
                    {
                        // Alpha blend: result = src * alpha + bg * (1 - alpha)
                        int invA = 255 - a;
                        pixels[i]     = (byte)((pixels[i]     * a + BgB * invA) / 255);
                        pixels[i + 1] = (byte)((pixels[i + 1] * a + BgG * invA) / 255);
                        pixels[i + 2] = (byte)((pixels[i + 2] * a + BgR * invA) / 255);
                        pixels[i + 3] = 255;
                    }
                }

                source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = JpegQuality };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var outMs = new MemoryStream();
            encoder.Save(outMs);
            return outMs.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? CreateDisplayThumbnail(byte[] thumbData, int thumbSize, string orient)
    {
        try
        {
            using var ms = new MemoryStream(thumbData);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            int maxW, maxH;
            if (orient == "portrait")
            {
                maxW = thumbSize * 2 / 3;
                maxH = thumbSize;
            }
            else
            {
                maxW = thumbSize;
                maxH = thumbSize * 2 / 3;
            }

            double scale = Math.Min((double)maxW / frame.PixelWidth, (double)maxH / frame.PixelHeight);
            if (scale > 1) scale = 1;

            if (Math.Abs(scale - 1.0) < 0.01)
                return frame;

            var transformed = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            var wb = new WriteableBitmap(transformed);
            wb.Freeze();
            return wb;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? LoadFullImage(byte[] imageData)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }
}
