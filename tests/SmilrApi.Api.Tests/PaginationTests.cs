using SmilrApi.Api.Rendering;

namespace SmilrApi.Api.Tests;

public class PaginationTests
{
    [Fact]
    public void Single_page_yields_no_numbers()
    {
        Assert.Empty(FindPageRenderer.BuildPageNumbers(1, 1));
    }

    [Fact]
    public void Zero_pages_yields_no_numbers()
    {
        Assert.Empty(FindPageRenderer.BuildPageNumbers(1, 0));
    }

    [Fact]
    public void Small_total_shows_every_page_no_ellipsis()
    {
        // totalPages=5, window=2 from page 1 only directly covers 1-3; page 4 sits in the one-page gap
        // before the seeded last page (5), so it's shown directly rather than collapsed to an ellipsis.
        Assert.Equal(
            new int?[] { 1, 2, 3, 4, 5 },
            FindPageRenderer.BuildPageNumbers(1, 5));
    }

    [Fact]
    public void One_hidden_page_is_shown_directly_instead_of_an_ellipsis()
    {
        // currentPage=5 leaves exactly page 2 between the window (3-7) and the seeded first page (1) —
        // an ellipsis representing a single page is pointless, so it's shown directly.
        Assert.Equal(
            new int?[] { 1, 2, 3, 4, 5, 6, 7, null, 10 },
            FindPageRenderer.BuildPageNumbers(5, 10));
    }

    [Fact]
    public void Two_or_more_hidden_pages_collapse_to_an_ellipsis_on_both_sides()
    {
        // currentPage=7, totalPages=20, window=2 -> window covers 5-9. Left gap (1..5) hides pages 2-4
        // (3 pages); right gap (9..20) hides pages 10-19 (10 pages) — both genuinely worth collapsing.
        Assert.Equal(
            new int?[] { 1, null, 5, 6, 7, 8, 9, null, 20 },
            FindPageRenderer.BuildPageNumbers(7, 20));
    }

    [Fact]
    public void First_page_shows_ellipsis_only_on_the_right()
    {
        Assert.Equal(
            new int?[] { 1, 2, 3, null, 10 },
            FindPageRenderer.BuildPageNumbers(1, 10));
    }

    [Fact]
    public void Last_page_shows_ellipsis_only_on_the_left()
    {
        Assert.Equal(
            new int?[] { 1, null, 8, 9, 10 },
            FindPageRenderer.BuildPageNumbers(10, 10));
    }

    [Fact]
    public void Window_reaching_first_page_has_no_gap_or_ellipsis_on_that_side()
    {
        // currentPage=3, window=2 -> window covers 1-5, already touching the seeded first page (1), so
        // there's no gap on the left at all, only the usual ellipsis on the right toward page 10.
        Assert.Equal(
            new int?[] { 1, 2, 3, 4, 5, null, 10 },
            FindPageRenderer.BuildPageNumbers(3, 10));
    }
}
