using Xunit;

public sealed class SetupExperienceTests
{
    [Fact]
    public void OfflineGuideIncludesEveryRequestedPermission()
    {
        foreach (var scope in IdentityHelper.GraphScopes) Assert.Contains(scope, SetupGuide.Markdown);
        Assert.Contains("http://localhost", SetupGuide.Markdown);
        Assert.Contains("Grant admin consent", SetupGuide.Markdown);
        Assert.Contains("Group", SetupGuide.PlainText);
    }

    [Fact]
    public void EmbeddedBrandLoadsWithoutAnExecutableOnDisk()
    {
        using var icon = AppBrand.CreateIcon();
        using var image = AppBrand.CreateLogo();
        Assert.Equal(32, icon.Width);
        Assert.Equal(image.Width, image.Height);
    }

    [Fact]
    public void SetupStartsWithGuideAndKeepsTypedIdsAcrossTabs()
    {
        RunSta(() =>
        {
            using var form = new ConnectionSettingsForm(new AppConfig());
            var tabs = Descendants(form).OfType<System.Windows.Forms.TabControl>().Single();
            Assert.Equal("App registration guide", tabs.SelectedTab!.Text);
            tabs.SelectedIndex = 0;
            var tenant = Descendants(form).OfType<System.Windows.Forms.TextBox>().Single(c => c.AccessibleName == "Directory (tenant) ID");
            tenant.Text = "typed value";
            tabs.SelectedIndex = 1;
            tabs.SelectedIndex = 0;
            Assert.Equal("typed value", tenant.Text);
            Assert.NotNull(form.Icon);
        });
    }

    [Fact]
    public void ExistingConfigurationOpensConnectionTab()
    {
        RunSta(() =>
        {
            using var form = new ConnectionSettingsForm(new AppConfig {
                TenantId = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(), GroupIds = [Guid.NewGuid().ToString()] });
            var tabs = Descendants(form).OfType<System.Windows.Forms.TabControl>().Single();
            Assert.Equal("Connection IDs", tabs.SelectedTab!.Text);
        });
    }

    [Fact]
    public void RenderSetupForLayoutReview()
    {
        // Optional local visual QA; CI still exercises construction in the tests above.
        var output = Environment.GetEnvironmentVariable("TEAMSIO_QA_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        RunSta(() =>
        {
            Directory.CreateDirectory(output);
            using var form = new ConnectionSettingsForm(new AppConfig());
            var tabs = Descendants(form).OfType<System.Windows.Forms.TabControl>().Single();
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            foreach (var tab in new[] { 1, 0 })
            {
                tabs.SelectedIndex = tab;
                form.CreateControl();
                form.PerformLayout();
                System.Windows.Forms.Application.DoEvents();
                using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle with { Width = form.Width, Height = form.Height });
                bitmap.Save(Path.Combine(output, tab == 1 ? "setup-guide.png" : "setup-connection.png"));
            }
        });
    }

    private static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control control) =>
        control.Controls.Cast<System.Windows.Forms.Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Setup UI did not finish in time.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
