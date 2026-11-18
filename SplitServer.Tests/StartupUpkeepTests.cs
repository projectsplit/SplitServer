using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// The runner for the work done on every boot. It sits between starting the process and answering
/// the first request, so what is pinned is that it can neither stop the app from starting nor keep
/// it waiting for a database that is not there.
/// </summary>
public class StartupUpkeepTests
{
    private static (string What, Func<Task> Step) Step(string what, List<string> ran, Exception? fails = null) =>
        (what, () =>
        {
            ran.Add(what);

            return fails is null ? Task.CompletedTask : Task.FromException(fails);
        });

    [Fact]
    public async Task Every_step_runs_in_order()
    {
        var ran = new List<string>();

        var completed = await StartupUpkeep.RunInOrder([Step("one", ran), Step("two", ran), Step("three", ran)]);

        Assert.Equal(new[] { "one", "two", "three" }, ran);
        Assert.Equal(new[] { "one", "two", "three" }, completed);
    }

    /// <summary>
    /// The app works without any one of these, so a step that fails is no reason not to start, and
    /// no reason to skip the ones after it.
    /// </summary>
    [Fact]
    public async Task A_step_that_fails_neither_stops_the_app_starting_nor_the_steps_after_it()
    {
        var ran = new List<string>();

        var completed = await StartupUpkeep.RunInOrder(
        [
            Step("one", ran),
            Step("two", ran, fails: new InvalidOperationException("an index with that name already exists")),
            Step("three", ran),
        ]);

        Assert.Equal(new[] { "one", "two", "three" }, ran);
        Assert.Equal(new[] { "one", "three" }, completed);
    }

    /// <summary>
    /// A timeout is what a database that cannot be reached looks like. Every later step would wait
    /// the same half minute to learn the same thing, and the app would start that much later for it.
    /// </summary>
    [Fact]
    public async Task Once_the_database_cannot_be_reached_the_remaining_steps_are_not_made_to_wait_for_it()
    {
        var ran = new List<string>();

        var completed = await StartupUpkeep.RunInOrder(
        [
            Step("one", ran),
            Step("two", ran, fails: new TimeoutException("A timeout occurred after 30000ms selecting a server")),
            Step("three", ran),
            Step("four", ran),
        ]);

        Assert.Equal(new[] { "one", "two" }, ran);
        Assert.Equal(new[] { "one" }, completed);
    }

    [Fact]
    public async Task A_step_that_throws_before_it_has_even_started_is_handled_the_same()
    {
        var ran = new List<string>();

        var completed = await StartupUpkeep.RunInOrder(
        [
            ("throws", () => throw new InvalidOperationException("no such service")),
            Step("after", ran),
        ]);

        Assert.Equal(new[] { "after" }, completed);
    }
}
