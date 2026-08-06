using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateRecurringExpenseCommandHandler
    : IRequestHandler<CreateRecurringExpenseCommand, Result<CreateRecurringExpenseResponse>>
{
    private readonly IRecurringExpensesRepository _recurringExpensesRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IExpensesRepository _expensesRepository;
    private readonly PermissionService _permissionService;
    private readonly RecurringExpenseValidator _validator;

    public CreateRecurringExpenseCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IExpensesRepository expensesRepository,
        PermissionService permissionService,
        RecurringExpenseValidator validator)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _expensesRepository = expensesRepository;
        _permissionService = permissionService;
        _validator = validator;
    }

    public async Task<Result<CreateRecurringExpenseResponse>> Handle(
        CreateRecurringExpenseCommand command,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(command.UserId, ct);

        var timeZoneId = userPreferencesMaybe.HasValue
            ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone
            : DefaultValues.TimeZone;

        var scheduleValidationResult = RecurrenceCalculator.Validate(command.Schedule);

        if (scheduleValidationResult.IsFailure)
        {
            return scheduleValidationResult.ConvertFailure<CreateRecurringExpenseResponse>();
        }

        // Nothing is created today: the user picked a day and a time, so the first expense lands on
        // the first slot that matches it. Creating one now as well would date spending to a moment
        // they did not choose.
        var firstOccurrence = RecurrenceCalculator.GetFirstOccurrence(now, command.Schedule, timeZoneId);

        var buildResult = Build(command, firstOccurrence, now, timeZoneId);

        if (buildResult.IsFailure)
        {
            return buildResult.ConvertFailure<CreateRecurringExpenseResponse>();
        }

        var template = buildResult.Value;

        var validationResult = await _validator.Validate(template, ct);

        if (validationResult.IsFailure)
        {
            return validationResult.ConvertFailure<CreateRecurringExpenseResponse>();
        }

        // Resolved before the template is written, not after. Refusing the link once the schedule
        // is already stored would leave a series behind that the user was told had failed.
        Expense? expenseToLink = null;

        if (command.LinkExpenseId is not null)
        {
            var linkableResult = await ResolveLinkableExpense(command.LinkExpenseId, command.UserId, ct);

            if (linkableResult.IsFailure)
            {
                return linkableResult.ConvertFailure<CreateRecurringExpenseResponse>();
            }

            expenseToLink = linkableResult.Value;
        }

        var writeResult = await _recurringExpensesRepository.Insert(template, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateRecurringExpenseResponse>();
        }

        if (expenseToLink is not null)
        {
            var stampResult = await _expensesRepository.Update(
                WithSeries(expenseToLink, template.Id, now),
                ct);

            if (stampResult.IsFailure)
            {
                // The user is about to be told this failed, so the template must not stay behind
                // quietly producing expenses. Best effort: if the delete fails too, the retry path
                // at least sees the leftover rather than a phantom.
                await _recurringExpensesRepository.Delete(template.Id, ct);

                return stampResult.ConvertFailure<CreateRecurringExpenseResponse>();
            }
        }

        return new CreateRecurringExpenseResponse
        {
            RecurringExpenseId = template.Id,
            FirstOccurrence = firstOccurrence
        };
    }

    /// <summary>
    /// Resolves the expense a user is turning into a series, if they are allowed to act on it —
    /// the id arrives from the client, so owning the new schedule is not on its own permission to
    /// stamp an arbitrary expense with it.
    /// </summary>
    /// <remarks>
    /// Defers to the same checks the matching edit-expense command uses, rather than restating
    /// them. Whoever may edit an expense may set it repeating, and those rules are not the obvious
    /// ones: a non-group expense is editable by any payer or participant, not just whoever created
    /// it, and a group expense records its creator as a member id, which never equals a user id.
    /// </remarks>
    private async Task<Result<Expense>> ResolveLinkableExpense(
        string expenseId,
        string userId,
        CancellationToken ct)
    {
        var expenseMaybe = await _expensesRepository.GetById(expenseId, ct);

        if (expenseMaybe.HasNoValue)
        {
            return Result.Failure<Expense>($"Expense with id {expenseId} was not found");
        }

        return expenseMaybe.Value switch
        {
            GroupExpense => (await _permissionService.VerifyExpenseAction(userId, expenseId, ct))
                .Map(x => x.expense),
            NonGroupExpense => (await _permissionService.VerifyNonGroupExpenseAction(userId, expenseId, ct))
                .Map(x => (Expense)x.expense),
            _ => (await _permissionService.VerifyPersonalExpenseAction(userId, expenseId, ct))
                .Map(x => x.expense)
        };
    }

    private static Expense WithSeries(Expense expense, string recurringExpenseId, DateTime now)
    {
        return expense switch
        {
            GroupExpense e => e with { RecurringExpenseId = recurringExpenseId, Updated = now },
            NonGroupExpense e => e with { RecurringExpenseId = recurringExpenseId, Updated = now },
            PersonalExpense e => e with { RecurringExpenseId = recurringExpenseId, Updated = now },
            _ => expense
        };
    }

    private static Result<RecurringExpense> Build(
        CreateRecurringExpenseCommand command,
        DateTime firstOccurrence,
        DateTime now,
        string timeZoneId)
    {
        var id = Guid.NewGuid().ToString();

        if (command.GroupId is not null)
        {
            if (command.Payments is null || command.Shares is null)
            {
                return Result.Failure<RecurringExpense>("Payments and shares are required for a group recurring expense");
            }

            return new GroupRecurringExpense
            {
                Id = id,
                Created = now,
                Updated = now,
                UserId = command.UserId,
                Amount = command.Amount,
                Currency = command.Currency,
                Description = command.Description,
                Location = command.Location,
                Labels = command.Labels,
                Schedule = command.Schedule,
                TimeZoneId = timeZoneId,
                AnchorDate = firstOccurrence,
                NextOccurrence = firstOccurrence,
                IsPaused = false,
                LastExpenseId = null,
                LastRunAt = null,
                LastError = null,
                GroupId = command.GroupId,
                Payments = command.Payments,
                Shares = command.Shares
            };
        }

        if (command.NonGroupPayments is not null && command.NonGroupShares is not null)
        {
            return new NonGroupRecurringExpense
            {
                Id = id,
                Created = now,
                Updated = now,
                UserId = command.UserId,
                Amount = command.Amount,
                Currency = command.Currency,
                Description = command.Description,
                Location = command.Location,
                Labels = command.Labels,
                Schedule = command.Schedule,
                TimeZoneId = timeZoneId,
                AnchorDate = firstOccurrence,
                NextOccurrence = firstOccurrence,
                IsPaused = false,
                LastExpenseId = null,
                LastRunAt = null,
                LastError = null,
                Payments = command.NonGroupPayments,
                Shares = command.NonGroupShares
            };
        }

        return new PersonalRecurringExpense
        {
            Id = id,
            Created = now,
            Updated = now,
            UserId = command.UserId,
            Amount = command.Amount,
            Currency = command.Currency,
            Description = command.Description,
            Location = command.Location,
            Labels = command.Labels,
            Schedule = command.Schedule,
            TimeZoneId = timeZoneId,
            AnchorDate = firstOccurrence,
            NextOccurrence = firstOccurrence,
            IsPaused = false,
            LastExpenseId = null,
            LastRunAt = null,
            LastError = null
        };
    }
}
