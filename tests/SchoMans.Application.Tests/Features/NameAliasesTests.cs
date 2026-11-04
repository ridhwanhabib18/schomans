using SchoMans.Application.Features.Ai;

namespace SchoMans.Application.Tests.Features;

public class NameAliasesTests
{
    private static readonly Guid Rina = Guid.NewGuid();
    private static readonly Guid Oka = Guid.NewGuid();

    [Fact]
    public void Students_get_numbered_aliases_and_keep_them()
    {
        var aliases = new NameAliases();

        Assert.Equal("S1", aliases.For(Rina, "Rina Putri"));
        Assert.Equal("S2", aliases.For(Oka, "Oka Pratama"));
        Assert.Equal("S1", aliases.For(Rina, "Rina Putri"));
    }

    [Fact]
    public void Scrub_replaces_the_full_name_and_each_part_of_it_in_free_text()
    {
        var aliases = new NameAliases();
        aliases.For(Rina, "Rina Putri");
        aliases.For(Oka, "Oka Pratama");

        var scrubbed = aliases.Scrub("Rina Putri helped Oka. rina was late; PUTRI forgot her book, Pratama did not.");

        Assert.Equal("S1 helped S2. S1 was late; S1 forgot her book, S2 did not.", scrubbed);
        Assert.DoesNotContain("Rina", scrubbed);
    }

    [Fact]
    public void Scrub_leaves_other_words_alone()
    {
        var aliases = new NameAliases();
        aliases.For(Rina, "Rina Putri");

        Assert.Equal("Marina went to the arina market.", aliases.Scrub("Marina went to the arina market."));
    }

    [Fact]
    public void Short_name_parts_are_not_scrubbed_by_themselves()
    {
        var aliases = new NameAliases();
        aliases.For(Rina, "Siti Al Zahra");

        // "Al" is shorter than three letters and would hit ordinary words.
        Assert.Equal("S1 said al dente is fine; S1 agreed.", aliases.Scrub("Siti said al dente is fine; Zahra agreed."));
    }

    [Fact]
    public void Restore_puts_the_real_names_back_and_ignores_unknown_aliases()
    {
        var aliases = new NameAliases();
        aliases.For(Rina, "Rina Putri");
        aliases.For(Oka, "Oka Pratama");

        Assert.Equal("Rina Putri and Oka Pratama did well; S9 is unknown; S10 too.", aliases.Restore("S1 and S2 did well; S9 is unknown; S10 too."));
    }

    [Fact]
    public void Aliases_are_replaced_whole_so_S1_does_not_hit_S10()
    {
        var aliases = new NameAliases();
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        for (var i = 0; i < ids.Count; i++)
        {
            aliases.For(ids[i], $"Student Number{i + 1:00}");
        }

        Assert.Equal("Student Number10 and Student Number01", aliases.Restore("S10 and S1"));
    }
}
