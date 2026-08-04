using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class ShellNavigationViewModelTests
{
    [TestMethod]
    public void Constructor_DefaultsToHomeAndListsAllSections()
    {
        var viewModel = new ShellNavigationViewModel();

        Assert.AreEqual(ShellSection.Home, viewModel.CurrentSection);
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>(),
            viewModel.Items.Select(item => item.Section).ToArray());
    }

    [DataTestMethod]
    [DataRow("Ctrl+1", ShellSection.Home)]
    [DataRow("Ctrl+L", ShellSection.Search)]
    [DataRow("Ctrl+2", ShellSection.Playlists)]
    [DataRow("Ctrl+3", ShellSection.Library)]
    [DataRow("Ctrl+4", ShellSection.Downloads)]
    [DataRow("Ctrl+5", ShellSection.Accounts)]
    [DataRow("Ctrl+,", ShellSection.Settings)]
    public void TryHandleShortcut_KnownShortcut_NavigatesToExpectedSection(string shortcut, ShellSection expectedSection)
    {
        var viewModel = new ShellNavigationViewModel();

        var handled = viewModel.TryHandleShortcut(shortcut);

        Assert.IsTrue(handled);
        Assert.AreEqual(expectedSection, viewModel.CurrentSection);
    }

    [TestMethod]
    public void TryHandleShortcut_UnknownShortcut_DoesNotChangeSection()
    {
        var viewModel = new ShellNavigationViewModel();

        var handled = viewModel.TryHandleShortcut("Ctrl+Shift+X");

        Assert.IsFalse(handled);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentSection);
    }
}
