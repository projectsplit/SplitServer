using CSharpFunctionalExtensions;
using SplitServer.Commands;
using SplitServer.Models;
using SplitServer.Queries.Models;
using SplitServer.Repositories;
using SplitServer.Requests;
using SplitServer.Services;

namespace SplitServer.Tests;

/// <summary>
/// The app sends an expense or transfer it filed offline until it hears back, so the same one can
/// reach the server twice, or twice at once. What these pin down is that it is stored exactly once,
/// and that every send of it gets the answer the first one would have had.
/// </summary>
public class ClientIdsTests
{
    private const string UserId = "me";
    private const string ExpenseId = "8f2b7c1a-3d4e-4f5a-9b6c-7d8e9f0a1b2c";
    private const string MemberId = "member-me";
    private const string TransferA = "0b1c2d3e-4f50-4617-8293-a4b5c6d7e8f9";
    private const string TransferB = "1c2d3e4f-5061-4728-93a4-b5c6d7e8f90a";

    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Without_an_id_from_the_app_the_server_picks_a_fresh_one()
    {
        var first = ClientIds.Resolve(null);
        var second = ClientIds.Resolve(null);

        Assert.True(Guid.TryParse(first.Value, out _));
        Assert.NotEqual(first.Value, second.Value);
    }

    [Fact]
    public void An_id_from_the_app_is_kept_and_written_the_way_server_ids_are()
    {
        var result = ClientIds.Resolve("{8F2B7C1A-3D4E-4F5A-9B6C-7D8E9F0A1B2C}");

        Assert.Equal(ExpenseId, result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Anything_but_a_real_guid_is_refused(string clientId)
    {
        var result = ClientIds.Resolve(clientId);

        Assert.Equal(ClientIds.InvalidIdError, result.Error);
    }

    [Fact]
    public async Task An_id_nothing_is_stored_under_is_free()
    {
        var repository = new FakeExpensesRepository();

        var result = await ClientIds.IsAlreadyStored<Expense, PersonalExpense>(repository, ExpenseId, UserId, default);

        Assert.False(result.Value);
    }

    [Fact]
    public async Task The_creators_own_earlier_send_counts_as_stored()
    {
        var repository = new FakeExpensesRepository(PersonalExpense(ExpenseId, creatorId: UserId));

        var result = await ClientIds.IsAlreadyStored<Expense, PersonalExpense>(repository, ExpenseId, UserId, default);

        Assert.True(result.Value);
    }

    [Fact]
    public async Task An_id_held_by_someone_elses_expense_is_a_clash()
    {
        var repository = new FakeExpensesRepository(PersonalExpense(ExpenseId, creatorId: "someone-else"));

        var result = await ClientIds.IsAlreadyStored<Expense, PersonalExpense>(repository, ExpenseId, UserId, default);

        Assert.Equal(ClientIds.TakenIdError, result.Error);
    }

    [Fact]
    public async Task An_id_held_by_another_kind_of_expense_is_a_clash()
    {
        // A group expense's creator is a member id, never a user id, so only the kind can tell
        // these apart when the ids happen to coincide.
        var repository = new FakeExpensesRepository(PersonalExpense(ExpenseId, creatorId: UserId));

        var result = await ClientIds.IsAlreadyStored<Expense, GroupExpense>(repository, ExpenseId, UserId, default);

        Assert.Equal(ClientIds.TakenIdError, result.Error);
    }

    [Fact]
    public async Task Sending_the_same_expense_twice_stores_it_once()
    {
        var repository = new FakeExpensesRepository();
        var handler = PersonalHandler(repository);

        var first = await handler.Handle(Command(ExpenseId), default);
        var second = await handler.Handle(Command(ExpenseId), default);

        Assert.Equal(ExpenseId, first.Value.ExpenseId);
        Assert.Equal(ExpenseId, second.Value.ExpenseId);
        Assert.Equal(ExpenseId, Assert.Single(repository.Stored).Id);
    }

    [Fact]
    public async Task A_send_that_loses_the_race_to_its_twin_still_succeeds()
    {
        // Both sends checked the id before either wrote: the twin's insert has landed, so this
        // one's insert hits the duplicate id after its own check found nothing there.
        var repository = new FakeExpensesRepository(PersonalExpense(ExpenseId, creatorId: UserId))
        {
            LookupsToMiss = 1
        };

        var result = await PersonalHandler(repository).Handle(Command(ExpenseId), default);

        Assert.Equal(ExpenseId, result.Value.ExpenseId);
        Assert.Single(repository.Stored);
    }

    [Fact]
    public async Task An_expense_sent_without_an_id_is_stored_under_a_fresh_one()
    {
        var repository = new FakeExpensesRepository();

        var result = await PersonalHandler(repository).Handle(Command(null), default);

        Assert.Equal(Assert.Single(repository.Stored).Id, result.Value.ExpenseId);
        Assert.NotEqual(ExpenseId, result.Value.ExpenseId);
    }

    [Fact]
    public async Task An_id_another_user_holds_is_refused_and_leaves_their_expense_alone()
    {
        var theirs = PersonalExpense(ExpenseId, creatorId: "someone-else");
        var repository = new FakeExpensesRepository(theirs);

        var result = await PersonalHandler(repository).Handle(Command(ExpenseId), default);

        Assert.Equal(ClientIds.TakenIdError, result.Error);
        Assert.Equal(theirs, Assert.Single(repository.Stored));
    }

    [Fact]
    public void A_settle_up_keeps_its_order_and_gets_ids_for_the_transfers_without_one()
    {
        var result = ClientIds.ResolveAll([TransferA, null]);

        Assert.Equal(TransferA, result.Value[0]);
        Assert.True(Guid.TryParse(result.Value[1], out _));
    }

    [Fact]
    public void A_settle_up_with_one_bad_id_is_refused_whole()
    {
        var result = ClientIds.ResolveAll([TransferA, "not-a-guid"]);

        Assert.Equal(ClientIds.InvalidIdError, result.Error);
    }

    [Fact]
    public async Task The_members_own_earlier_transfer_counts_as_stored()
    {
        var repository = new FakeTransfersRepository(Settled(TransferA, MemberId));

        var result = await ClientIds.IsAlreadyStored<Transfer, GroupTransfer>(repository, TransferA, MemberId, default);

        Assert.True(result.Value);
    }

    [Fact]
    public async Task A_transfer_id_held_by_another_member_is_a_clash()
    {
        var repository = new FakeTransfersRepository(Settled(TransferA, "member-someone-else"));

        var result = await ClientIds.IsAlreadyStored<Transfer, GroupTransfer>(repository, TransferA, MemberId, default);

        Assert.Equal(ClientIds.TakenIdError, result.Error);
    }

    [Fact]
    public async Task A_new_settle_up_stores_every_transfer()
    {
        var repository = new FakeTransfersRepository();

        var result = await SettleUp(repository, Settled(TransferA, MemberId), Settled(TransferB, MemberId));

        Assert.Equal(2, result.Value);
        Assert.Equal(2, repository.Stored.Count);
    }

    [Fact]
    public async Task Resending_a_settle_up_that_is_in_stores_nothing()
    {
        var a = Settled(TransferA, MemberId);
        var b = Settled(TransferB, MemberId);
        var repository = new FakeTransfersRepository(a, b);

        var result = await SettleUp(repository, a, b);

        // Nothing stored means nothing to notify anyone about a second time.
        Assert.Equal(0, result.Value);
        Assert.Equal(2, repository.Stored.Count);
    }

    [Fact]
    public async Task A_settle_up_that_got_part_of_the_way_is_finished_by_its_resend()
    {
        var a = Settled(TransferA, MemberId);
        var b = Settled(TransferB, MemberId);
        var repository = new FakeTransfersRepository(a);

        var result = await SettleUp(repository, a, b);

        Assert.Equal(1, result.Value);
        Assert.Equal([TransferA, TransferB], repository.Stored.Select(x => x.Id).Order());
    }

    [Fact]
    public async Task A_settle_up_with_an_id_someone_else_holds_stores_none_of_it()
    {
        var theirs = Settled(TransferA, "member-someone-else");
        var repository = new FakeTransfersRepository(theirs);

        var result = await SettleUp(repository, Settled(TransferA, MemberId), Settled(TransferB, MemberId));

        Assert.Equal(ClientIds.TakenIdError, result.Error);
        Assert.Equal(theirs, Assert.Single(repository.Stored));
    }

    [Fact]
    public async Task A_settle_up_that_loses_the_race_to_its_twin_still_succeeds()
    {
        // Both sends looked before either wrote: the twin's batch has landed, so this one's insert
        // hits the duplicate ids after its own look found nothing there.
        var a = Settled(TransferA, MemberId);
        var b = Settled(TransferB, MemberId);
        var repository = new FakeTransfersRepository(a, b) { LookupsToMiss = 1 };

        var result = await SettleUp(repository, a, b);

        Assert.Equal(0, result.Value);
        Assert.Equal(2, repository.Stored.Count);
    }

    private static Task<Result<int>> SettleUp(ITransfersRepository repository, params GroupTransfer[] transfers) =>
        ClientIds.InsertMissing<Transfer, GroupTransfer>(repository, transfers, MemberId, default);

    private static GroupTransfer Settled(string id, string creatorId) =>
        new()
        {
            Id = id,
            Created = Now,
            Updated = Now,
            GroupId = "group",
            CreatorId = creatorId,
            SenderId = "member-a",
            ReceiverId = "member-b",
            Amount = 10m,
            Currency = "EUR",
            Description = "Settled Debt",
            Occurred = Now
        };

    private static CreatePersonalExpenseCommandHandler PersonalHandler(IExpensesRepository repository) =>
        new(repository, null!, new ValidationService(), new UserLabelService(new FakeUserLabelsRepository()));

    private static CreatePersonalExpenseCommand Command(string? expenseId) =>
        new()
        {
            UserId = UserId,
            ExpenseId = expenseId,
            Amount = 12.5m,
            Currency = "EUR",
            Description = "Lunch",
            Occurred = Now,
            Labels = [new LabelRequestItem { Text = "food", Color = "red" }],
            Location = null
        };

    private static PersonalExpense PersonalExpense(string id, string creatorId) =>
        new()
        {
            Id = id,
            Created = Now,
            Updated = Now,
            Occurred = Now,
            CreatorId = creatorId,
            Amount = 12.5m,
            Description = "Lunch",
            Currency = "EUR",
            Location = null,
            Labels = []
        };
}

/// <summary>
/// Holds expenses by id and refuses a second insert under the same one, as the unique _id does in
/// Mongo. <see cref="LookupsToMiss"/> makes the next lookups find nothing, which is how a write that
/// lands between a handler's check and its own insert looks from inside the handler.
/// </summary>
file class FakeExpensesRepository : IExpensesRepository
{
    private readonly List<Expense> _expenses;

    public FakeExpensesRepository(params Expense[] expenses) => _expenses = [.. expenses];

    public IReadOnlyList<Expense> Stored => _expenses;

    public int LookupsToMiss { get; set; }

    public Task<Maybe<Expense>> GetById(string id, CancellationToken ct)
    {
        if (LookupsToMiss > 0)
        {
            LookupsToMiss--;
            return Task.FromResult(Maybe<Expense>.None);
        }

        var expense = _expenses.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(expense is not null ? expense : Maybe<Expense>.None);
    }

    public Task<Result> Insert(Expense entity, CancellationToken ct)
    {
        if (_expenses.Any(x => x.Id == entity.Id))
        {
            return Task.FromResult(Result.Failure("Insert failed E11000 duplicate key error"));
        }

        _expenses.Add(entity);

        return Task.FromResult(Result.Success());
    }

    public Task<IList<Expense>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> InsertMany(IEnumerable<Expense> entities, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(Expense entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Expense updatedEntity, CancellationToken ct) => throw new NotSupportedException();

    public Task<List<GroupExpense>> GetByGroupId(string groupId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupExpense>> GetGroupExpensesByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupExpense>> GetNonGroupExpensesByUserId(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Expense>> GetPersonalExpensesByUserId(string userId, List<string> memberIds, CancellationToken ct, DateTime? startDate = null, DateTime? endDate = null) => throw new NotSupportedException();
    public Task<Dictionary<string, int>> GetLabelCounts(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> ClearRecurringExpenseId(string recurringExpenseId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupExpense>> GetGroupExpensesByMemberIds(List<string> memberIds, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsInAnyExpense(string groupId, string memberId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> LabelIsInUse(string groupId, string labelId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> UserLabelInUse(string labelText, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupExpense>> Search(string groupId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupExpense>> GetNonGroupByUserId(string userId, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> GetPersonalByUserId(string userId, List<string> memberIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<Expense>> SearchPersonalByUserId(string userId, List<string> memberIds, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? labels, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupExpense>> SearchNonGroup(string userId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? participantIds, string[]? payerIds, string[]? labelIds, int pageSize, DateTime? occurred, DateTime? created, PaginationDirection direction, bool inclusive, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<long> CountByCreatorId(string creatorId, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeletePersonalByCreatorId(string creatorId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
}

file class FakeUserLabelsRepository : IUserLabelsRepository
{
    private readonly List<UserLabel> _labels = [];

    public Task<List<UserLabel>> GetByUserId(string userId, CancellationToken ct) =>
        Task.FromResult(_labels.Where(x => x.UserId == userId).ToList());

    public Task<Result> InsertMany(IEnumerable<UserLabel> entities, CancellationToken ct)
    {
        _labels.AddRange(entities);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Maybe<UserLabel>> GetById(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IList<UserLabel>> GetByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Insert(UserLabel entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Upsert(UserLabel entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(UserLabel updatedEntity, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// Holds transfers by id and refuses a batch with an id already stored, as the unique _id does in
/// Mongo. <see cref="LookupsToMiss"/> makes the next lookups find nothing, which is how a twin's
/// write that lands between a handler's look and its own insert appears from inside the handler.
/// </summary>
file class FakeTransfersRepository : ITransfersRepository
{
    private readonly List<Transfer> _transfers;

    public FakeTransfersRepository(params Transfer[] transfers) => _transfers = [.. transfers];

    public IReadOnlyList<Transfer> Stored => _transfers;

    public int LookupsToMiss { get; set; }

    public Task<Maybe<Transfer>> GetById(string id, CancellationToken ct)
    {
        if (LookupsToMiss > 0)
        {
            LookupsToMiss--;
            return Task.FromResult(Maybe<Transfer>.None);
        }

        var transfer = _transfers.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(transfer is not null ? transfer : Maybe<Transfer>.None);
    }

    public Task<IList<Transfer>> GetByIds(IList<string> ids, CancellationToken ct)
    {
        if (LookupsToMiss > 0)
        {
            LookupsToMiss--;
            return Task.FromResult<IList<Transfer>>([]);
        }

        return Task.FromResult<IList<Transfer>>(_transfers.Where(x => ids.Contains(x.Id)).ToList());
    }

    public Task<Result> Insert(Transfer entity, CancellationToken ct) => InsertMany([entity], ct);

    public Task<Result> InsertMany(IEnumerable<Transfer> entities, CancellationToken ct)
    {
        var batch = entities.ToList();

        if (batch.Any(x => _transfers.Any(y => y.Id == x.Id)))
        {
            return Task.FromResult(Result.Failure("Insert failed E11000 duplicate key error"));
        }

        _transfers.AddRange(batch);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> Upsert(Transfer entity, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Delete(string id, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> Update(Transfer updatedEntity, CancellationToken ct) => throw new NotSupportedException();

    public Task<List<GroupTransfer>> GetByGroupId(string groupId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> GetByUserId(string userId, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> GetAllByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> GetAllByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByGroupId(string groupId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> GetAllByMemberIds(List<string> memberIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> ExistsInAnyTransfer(string groupId, string memberId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<GroupTransfer>> Search(string groupId, string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? receiverIds, string[]? senderIds, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<NonGroupTransfer>> SearchNonGroup(string? searchTerm, DateTime? minTime, DateTime? maxTime, string[]? receiverIds, string[]? senderIds, int pageSize, DateTime? maxOccurred, DateTime? maxCreated, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<string>> GetNonGroupUserIdsByUserId(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<Result> DeleteByIds(IList<string> ids, CancellationToken ct) => throw new NotSupportedException();
}
