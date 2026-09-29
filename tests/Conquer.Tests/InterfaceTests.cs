using Conquer.Client.Screens;

namespace Conquer.Tests;

public class InterfaceTests
{
    [Fact]
    public void NamesSortInSpanishAlphabeticalOrder()
    {
        string[] names = ["Zaragoza", "ñandú", "Ávila", "Nápoles", "oca", "Écija", "avena", "Nzé"];
        var sorted = names.OrderBy(NationView.SpanishSortKey, StringComparer.Ordinal).ToArray();
        Assert.Equal(["avena", "Ávila", "Écija", "Nápoles", "Nzé", "ñandú", "oca", "Zaragoza"], sorted);
    }
}
