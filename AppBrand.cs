internal static class AppBrand
{
    public static Icon CreateIcon()
    {
        using var stream = typeof(AppBrand).Assembly.GetManifestResourceStream("TeamsIO.AppIcon")!;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }

    public static Image CreateLogo()
    {
        using var stream = typeof(AppBrand).Assembly.GetManifestResourceStream("TeamsIO.AppLogo")!;
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }
}
