using System.ComponentModel;
using MarcusW.VncClient;
using SkiaSharp;

namespace DisControl.Bot;

public static class Conversions
{
    /// <summary>
    /// Converts a Avalonia PixelFormat to a <see cref="PixelFormat"/>.
    /// </summary>
    /// <param name="avaloniaPixelFormat">Value to convert.</param>
    /// <returns>The conversion result.</returns>
    public static PixelFormat GetPixelFormat(SKColorType avaloniaPixelFormat)
        => avaloniaPixelFormat switch {
            SKColorType.Rgb565 => new PixelFormat("Skia kRGB_565_SkColorType", 16, 16, false, true, false, 31, 63, 31, 0, 11, 5, 0, 0),
            SKColorType.Rgba8888 => new PixelFormat("Skia kRGBA_8888_SkColorType", 32, 32, false, true, true, 0xFF, 0xFF, 0xFF, 0xFF, 0, 8, 16, 24),
            SKColorType.Bgra8888 => new PixelFormat("Skia kBGRA_8888_SkColorType", 32, 32, false, true, true, 0xFF, 0xFF, 0xFF, 0xFF, 16, 8, 0, 24),
            _ => throw new InvalidEnumArgumentException(nameof(avaloniaPixelFormat), (int)avaloniaPixelFormat, typeof(SKColorType))
        };
}