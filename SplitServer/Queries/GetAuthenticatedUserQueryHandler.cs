﻿using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
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

    private const string DefaultCurrency = "EUR";
    private const string DefaultTimeZone = "Europe/Athens";
    private readonly Coordinates _defaultCoordinates;

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
        _defaultCoordinates = new Coordinates { Latitude = 37.96667, Longitude = 23.71667 };
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

        var currency = userPreferencesMaybe.HasValue ? userPreferencesMaybe.Value.Currency ?? DefaultCurrency : DefaultCurrency;
        var timeZone = userPreferencesMaybe.HasValue ? userPreferencesMaybe.Value.TimeZone ?? DefaultTimeZone : DefaultTimeZone;
        var recentGroupId = userActivityMaybe.HasValue ? userActivityMaybe.Value.RecentGroupId : null;

        return new GetAuthenticatedUserResponse
        {
            UserId = user.Id,
            Username = user.Username,
            HasNewerNotifications = notificationsCount > 0,
            Currency = currency,
            TimeZone = timeZone,
            TimeZoneCoordinates = _timeZoneService.CreateCoordinatesFromTimeZone(timeZone).GetValueOrDefault(_defaultCoordinates),
            RecentGroupId = recentGroupId,
        };
    }
}