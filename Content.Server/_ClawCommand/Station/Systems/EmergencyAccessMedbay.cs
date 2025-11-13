using Content.Server.Chat.Systems;
using Content.Server.DeltaV.Cabinet;
using Content.Server._ClawCommand.Station.Components;
using Content.Server.DeltaV.Station.Events;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access;
using Content.Shared.DeltaV.CCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Content.Server.Doors.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Access.Systems;

namespace Content.Server._ClawCommand.Station.Systems;

public sealed class EmergencyAccessMedbayStateSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AirlockSystem _airlockSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] protected readonly ILogManager _logManager = default!;

    public ISawmill _sawmill { get; private set; } = default!;

    private bool _eaEnabled = true;
    private float _acoDelay = 600;
    private int _maxDoctorsForEA = 1;

    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("fullscreen");

        SubscribeLocalEvent<EmergencyAccessMedbayStateComponent, PlayerJobAddedEvent>(OnPlayerJobAdded);
        SubscribeLocalEvent<EmergencyAccessMedbayStateComponent, PlayerJobsRemovedEvent>(OnPlayerJobsRemoved);

        base.Initialize();
    }
    private float _timePassed = 0;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _timePassed += frameTime;
        if (_timePassed < _acoDelay) // Avoid timing issues. No need to run before _acoDelay is reached anyways.
            return;


        var query = EntityQueryEnumerator<EmergencyAccessMedbayStateComponent>();
        while (query.MoveNext(out var station, out var captainState))
        {

            if (captainState.DoctorCount <= _maxDoctorsForEA)
                HandleNoDoctors(station, captainState, _timePassed);
            else
            {
                if (captainState.IsAAInPlay)
                {
                    captainState.IsAAInPlay = false;
                    _chat.DispatchStationAnnouncement(station, Loc.GetString(captainState.RevokeACOMessage), colorOverride: Color.Yellow);

                    var qquery = EntityQueryEnumerator<AirlockComponent>();
                    while (qquery.MoveNext(out var airlockID, out var airlockComp))
                    {

                        if (_accessReaderSystem.GetMainAccessReader(airlockID, out var mainReader))
                        {

                            if (mainReader.AccessLists.Any(list => list.Contains("Medical")))
                            {

                                _airlockSystem.SetEmergencyAccess((airlockID, airlockComp), false);
                            }
                        }

                    }
                }
            }
        }
    }

    private void OnPlayerJobAdded(Entity<EmergencyAccessMedbayStateComponent> ent, ref PlayerJobAddedEvent args)
    {
        if (args.JobPrototypeId == "ChiefMedicalOfficer" ||
         args.JobPrototypeId == "MedicalDoctor" ||
          args.JobPrototypeId == "Chemist" ||
          args.JobPrototypeId == "Paramedic")
        {
            ent.Comp.DoctorCount += 1;
        }
    }

    private void OnPlayerJobsRemoved(Entity<EmergencyAccessMedbayStateComponent> ent, ref PlayerJobsRemovedEvent args)
    {
        if (!TryComp<StationJobsComponent>(ent, out var stationJobs))
            return;
        if (!args.PlayerJobs.Contains("ChiefMedicalOfficer") ||
        !args.PlayerJobs.Contains("MedicalDoctor") ||
        !args.PlayerJobs.Contains("Chemist") ||
        !args.PlayerJobs.Contains("Paramedic")) // If the player that left was a captain we need to check if there are any captains left
            return;
        if (stationJobs.PlayerJobs.Any(playerJobs => playerJobs.Value.Contains("ChiefMedicalOfficer")) ||
        stationJobs.PlayerJobs.Any(playerJobs => playerJobs.Value.Contains("MedicalDoctor")) ||
       stationJobs.PlayerJobs.Any(playerJobs => playerJobs.Value.Contains("Chemist")) ||
       stationJobs.PlayerJobs.Any(playerJobs => playerJobs.Value.Contains("Paramedic"))
       ) // We check the PlayerJobs if there are any cpatins left
            return;
        ent.Comp.DoctorCount -= 1;
        if (ent.Comp.DoctorCount < 0)
            ent.Comp.DoctorCount = 0;
    }


    /// <summary>
    /// Handles cases for when there is no captain
    /// </summary>
    /// <param name="station"></param>
    /// <param name="EmergencyAccessMedbayState"></param>
    private void HandleNoDoctors(Entity<EmergencyAccessMedbayStateComponent?> station, EmergencyAccessMedbayStateComponent captainState, float currentTime)
    {

        if (CheckUnlockAA(captainState, currentTime))
        {
            captainState.IsAAInPlay = true;
            _chat.DispatchStationAnnouncement(station, Loc.GetString(captainState.AAUnlockedMessage), colorOverride: Color.Yellow);

            // Extend access of spare id lockers to command so they can access emergency AA
            var query = EntityQueryEnumerator<AirlockComponent>();
            while (query.MoveNext(out var airlockID, out var airlockComp))
            {

                if (_accessReaderSystem.GetMainAccessReader(airlockID, out var mainReader))
                {

                    if (mainReader.AccessLists.Any(list => list.Contains("Medical")))
                    {

                        _airlockSystem.SetEmergencyAccess((airlockID, airlockComp), true);
                    }
                }

            }
        }
    }

    /// <summary>
    /// Checks the conditions for if AA should be unlocked
    /// If time is null its condition is ignored
    /// </summary>
    /// <param name="EmergencyAccessMedbayState"></param>
    /// <returns>True if conditions are met for AA to be unlocked, False otherwise</returns>
    private bool CheckUnlockAA(EmergencyAccessMedbayStateComponent captainState, float? currentTime)
    {
        if (captainState.IsAAInPlay || !_eaEnabled)
            return false;
        return currentTime == null || currentTime > _acoDelay;
    }
}
