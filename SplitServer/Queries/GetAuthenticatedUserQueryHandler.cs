using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.TimeZone;

namespace SplitServer.Queries;

public class GetAuthenticatedUserQueryHandler : IRequestHandler<GetAuthenticatedUserQuery, Result<GetAuthenticatedUserResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IUserActivityRepository _userActivityRepository;
    private readonly IUserPreferencesRepository _userPreferencesRepository;
    private readonly IInvitationsRepository _invitationsRepository;
    private readonly TimeZoneService _timeZoneService;

    public GetAuthenticatedUserQueryHandler(
        IUsersRepository usersRepository,
        IUserActivityRepository userActivityRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IInvitationsRepository invitationsRepository,
        TimeZoneService timeZoneService)
    {
        _usersRepository = usersRepository;
        _userActivityRepository = userActivityRepository;
        _userPreferencesRepository = userPreferencesRepository;
        _invitationsRepository = invitationsRepository;
        _timeZoneService = timeZoneService;
    }

    public async Task<Result<GetAuthenticatedUserResponse>> Handle(GetAuthenticatedUserQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetAuthenticatedUserResponse>($"User with id {query.UserId} was not found");
        }

        var user = userMaybe.Value;

        var userActivityMaybe = await _userActivityRepository.GetById(query.UserId, ct);

        var lastViewedNotification = userActivityMaybe.HasValue
            ? userActivityMaybe.Value.LastViewedNotificationTimestamp ?? DateTime.MinValue
            : DateTime.MinValue;

        var notificationsCount = await _invitationsRepository.CountByReceiverIdAndMinCreated(query.UserId, lastViewedNotification, ct);

        var userPreferencesMaybe = await _userPreferencesRepository.GetById(query.UserId, ct);

        var currency = userPreferencesMaybe.HasValue ? userPreferencesMaybe.Value.Currency ?? DefaultValues.Currency : DefaultValues.Currency;
        var timeZone = userPreferencesMaybe.HasValue ? userPreferencesMaybe.Value.TimeZone ?? DefaultValues.TimeZone : DefaultValues.TimeZone;
        var recentGroupId = userActivityMaybe.HasValue ? userActivityMaybe.Value.RecentGroupId : null;

        return new GetAuthenticatedUserResponse
        {
            UserId = user.Id,
            Username = user.Username,
            HasNewerNotifications = notificationsCount > 0,
            Currency = currency,
            TimeZone = timeZone,
            TimeZoneCoordinates = _timeZoneService.CreateCoordinatesFromTimeZone(timeZone).GetValueOrDefault(DefaultValues.Coordinates),
            RecentGroupId = recentGroupId,
        };
    }
}