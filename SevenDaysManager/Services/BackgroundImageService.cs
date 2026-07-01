using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public static class BackgroundImageService
{
    private const string DefaultUri = "pack://application:,,,/Assets/bg.jpg";

    public static void Apply(string? imagePath)
    {
        BitmapImage? bmp = null;

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // load into memory, release the file handle
                bmp.UriSource   = new Uri(imagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
            }
            catch { bmp = null; } // corrupt/unreadable file — fall back to the bundled default
        }

        Application.Current.Resources["AppBackgroundSource"] =
            bmp ?? new BitmapImage(new Uri(DefaultUri));
    }

    public static void Apply(AppSettings settings) => Apply(settings.BackgroundImagePath);
}
