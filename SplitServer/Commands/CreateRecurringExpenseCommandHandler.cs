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
    private readonly RecurringExpenseValidator _validator;

    public CreateRecurringExpenseCommandHandler(
        IRecurringExpensesRepository recurringExpensesRepository,
        IUserPreferencesRepository userPreferencesRepository,
        RecurringExpenseValidator validator)
    {
        _recurringExpensesRepository = recurringExpensesRepository;
        _userPreferencesRepository = userPreferencesRepository;
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

        var writeResult = await _recurringExpensesRepository.Insert(template, ct);

        if (writeResult.IsFailure)
        {
            return writeResult.ConvertFailure<CreateRecurringExpenseResponse>();
        }

        return new CreateRecurringExpenseResponse
        {
            RecurringExpenseId = template.Id,
            FirstOccurrence = firstOccurrence
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
