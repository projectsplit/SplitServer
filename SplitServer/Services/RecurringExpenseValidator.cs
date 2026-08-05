using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Services;

/// <summary>
/// Checks a template the way the matching create-expense command would check the expense it will
/// produce. Nothing is written on the day a template is saved any more, so this is the only chance
/// to tell the user their split is wrong while they are still looking at it — otherwise the first
/// occurrence would fail weeks later, against a form they no longer have open.
/// </summary>
public class RecurringExpenseValidator
{
    private readonly PermissionService _permissionService;
    private readonly ValidationService _validationService;
    private readonly ConnectionService _connectionService;

    public RecurringExpenseValidator(
        PermissionService permissionService,
        ValidationService validationService,
        ConnectionService connectionService)
    {
        _permissionService = permissionService;
        _validationService = validationService;
        _connectionService = connectionService;
    }

    public async Task<Result> Validate(RecurringExpense template, CancellationToken ct)
    {
        var scheduleResult = RecurrenceCalculator.Validate(template.Schedule);

        if (scheduleResult.IsFailure)
        {
            return scheduleResult;
        }

        switch (template)
        {
            case GroupRecurringExpense t:
            {
                var permissionResult = await _permissionService.VerifyGroupAction(t.UserId, t.GroupId, ct);

                if (permissionResult.IsFailure)
                {
                    return Result.Failure(permissionResult.Error);
                }

                return _validationService.ValidateExpense(
                    permissionResult.Value.group,
                    t.Payments,
                    t.Shares,
                    t.Amount,
                    t.Currency);
            }

            case NonGroupRecurringExpense t:
            {
                var expenseResult = _validationService.ValidateNonGroupExpense(
                    t.Payments,
                    t.Shares,
                    t.Amount,
                    t.Currency,
                    t.UserId);

                if (expenseResult.IsFailure)
                {
                    return expenseResult;
                }

                var participantUserIds = t.Payments.Select(x => x.UserId)
                    .Concat(t.Shares.Select(x => x.UserId))
                    .ToList();

                return await _connectionService.VerifyCanSplitWith(t.UserId, participantUserIds, ct);
            }

            default:
                return _validationService.ValidatePersonalExpense(template.Amount, template.Currency);
        }
    }
}
