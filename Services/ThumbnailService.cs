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

    /// <summary>サムネイル枠（幅, 高さ）。カード側の表示サイズ計算と必ず同じ式を使う。</summary>
    public static (int W, int H) ThumbBox(int thumbSize, string orient) =>
        orient == "portrait"
            ? (thumbSize * 2 / 3, thumbSize)
            : (thumbSize, thumbSize * 2 / 3);

    public static byte[]? GenerateThumbnailBytes(byte[] imageData, string? fileName = null)
    {
        try
        {
            var source = DecodeToBox(imageData, MaxThumbPixels, MaxThumbPixels);
            if (source == null) return null;

            source = CompositeOnBackground(source);

            var encoder = new JpegBitmapEncoder { QualityLevel = JpegQuality };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var outMs = new MemoryStream();
            encoder.Save(outMs);
            return outMs.ToArray();
        }
        catch (Exception ex)
        {
            // デバッグ用ログ（異常調査時にコメント解除）
            // System.Diagnostics.Debug.WriteLine($"[ThumbnailService] GenerateThumbnailBytes failed: {ex}");
            // try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "archiveviewer_thumb_error.log"), $"[{DateTime.Now:HH:mm:ss}] file={fileName} {ex}\n---\n"); } catch { }
            return null;
        }
    }

    /// <summary>
    /// キャッシュ済みサムネイルJPEGを表示サイズでデコードする。
    /// srcW/srcH はキャッシュ自体の画素数（thumbSizeに依存しない固有サイズ）で、
    /// カード側がデコードなしでズームするための表示サイズ計算に使う。
    /// </summary>
    public static BitmapSource? CreateDisplayThumbnail(byte[] thumbData, int thumbSize, string orient, out int srcW, out int srcH)
    {
        srcW = srcH = 0;
        try
        {
            var (maxW, maxH) = ThumbBox(thumbSize, orient);
            var bmp = DecodeToBox(thumbData, maxW, maxH, out srcW, out srcH);
            if (bmp != null && srcW == 0)
            {
                srcW = bmp.PixelWidth;
                srcH = bmp.PixelHeight;
            }
            return bmp;
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

    // ======== DECODE CORE ========

    private static BitmapSource? DecodeToBox(byte[] data, int maxW, int maxH)
        => DecodeToBox(data, maxW, maxH, out _, out _);

    /// <summary>
    /// 長辺が maxW×maxH の枠に収まるようデコードする。DecodePixel* を指定することで
    /// WIC がデコード段階で縮小するため（JPEG では DCT の 1/2・1/4・1/8 スケールが効く）、
    /// 等倍デコード＋TransformedBitmap より大幅に速くピークメモリも小さい。
    /// 枠より小さい画像は拡大しない。
    /// </summary>
    private static BitmapSource? DecodeToBox(byte[] data, int maxW, int maxH, out int srcW, out int srcH)
    {
        srcW = srcH = 0;

        if (TryReadPixelSize(data, out int w, out int h))
        {
            srcW = w;
            srcH = h;

            double scale = Math.Min((double)maxW / w, (double)maxH / h);
            if (scale >= 1.0) return DecodeWithHint(data, 0, 0);

            // 片側だけ指定するとWIC側がアスペクト比を保つ。丸め差で枠を超えないよう
            // 縮小率を決めている側（長辺側）を指定する。
            return w * maxH >= h * maxW
                ? DecodeWithHint(data, Math.Max(1, (int)Math.Round(w * scale)), 0)
                : DecodeWithHint(data, 0, Math.Max(1, (int)Math.Round(h * scale)));
        }

        // ヘッダから寸法が読めない場合のみ、等倍デコード＋縮小の従来経路に落ちる
        var full = DecodeWithHint(data, 0, 0);
        srcW = full.PixelWidth;
        srcH = full.PixelHeight;
        double s = Math.Min((double)maxW / srcW, (double)maxH / srcH);
        if (s >= 1.0) return full;

        var transformed = new TransformedBitmap(full, new ScaleTransform(s, s));
        transformed.Freeze();
        return transformed;
    }

    /// <summary>ピクセルを展開せずフレームのメタデータだけ読んで寸法を得る。</summary>
    private static bool TryReadPixelSize(byte[] data, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using var ms = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) return false;
            var frame = decoder.Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// BitmapImage 経由でデコードする。BitmapDecoder より寛容で（メタデータが特殊な
    /// JPEG でも通る）、DecodePixel* でデコード時縮小が効く。DPI は 96 に正規化される。
    /// </summary>
    private static BitmapSource DecodeWithHint(byte[] data, int decodeW, int decodeH)
    {
        using var ms = new MemoryStream(data);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        if (decodeW > 0) bi.DecodePixelWidth = decodeW;
        if (decodeH > 0) bi.DecodePixelHeight = decodeH;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    /// <summary>
    /// アルファを持つ画像をテーマ背景色に合成する。
    /// （RenderTargetBitmap/DrawingVisual はSTAスレッドを要求するためピクセル操作で行う）
    /// </summary>
    private static BitmapSource CompositeOnBackground(BitmapSource source)
    {
        bool premultiplied = source.Format == PixelFormats.Pbgra32;
        if (source.Format != PixelFormats.Bgra32 && !premultiplied) return source;

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
            else if (premultiplied)
            {
                // 乗算済みアルファ: result = src + bg * (1 - alpha)
                int invA = 255 - a;
                pixels[i]     = (byte)Math.Min(255, pixels[i]     + BgB * invA / 255);
                pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] + BgG * invA / 255);
                pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] + BgR * invA / 255);
                pixels[i + 3] = 255;
            }
            else
            {
                // ストレートアルファ: result = src * alpha + bg * (1 - alpha)
                int invA = 255 - a;
                pixels[i]     = (byte)((pixels[i]     * a + BgB * invA) / 255);
                pixels[i + 1] = (byte)((pixels[i + 1] * a + BgG * invA) / 255);
                pixels[i + 2] = (byte)((pixels[i + 2] * a + BgR * invA) / 255);
                pixels[i + 3] = 255;
            }
        }

        var composited = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        composited.Freeze();
        return composited;
    }
}
