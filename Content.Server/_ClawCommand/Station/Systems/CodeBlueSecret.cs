using Content.Server.Chat.Systems;
using Content.Server._ClawCommand.Station.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.AlertLevel;

namespace Content.Server._ClawCommand.Station.Systems;

public sealed class CodeBlueSecretSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;

    private float _acoDelay = 300;
    private bool _ran = false;
    public override void Initialize()
    {
        base.Initialize();
    }
    private float _timePassed = 0;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _timePassed += frameTime;
        if (_ran || _timePassed < _acoDelay) // Avoid timing issues. No need to run before _acoDelay is reached anyways.
            return;
        _ran = true;
        if (_ticker.IsGameRuleAdded<SecretRuleComponent>())
        {

            var query = EntityQueryEnumerator<EmergencyAccessMedbayStateComponent>();
            while (query.MoveNext(out var station, out var _))
            {


                if (_alertLevelSystem.GetLevel(station) == "green")
                {
                    _alertLevelSystem.SetLevel(station, "blueAuto", true, true);
                }
            }


        }


    }

}
