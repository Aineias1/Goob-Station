// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 BombasterDS <deniskaporoshok@gmail.com>
// SPDX-FileCopyrightText: 2025 BombasterDS2 <shvalovdenis.workmail@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.Lightning;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Server._EinsteinEngines.Silicon.Charge;
using Content.Shared._EinsteinEngines.Silicon.DeadStartupButton;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Electrocution;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._EinsteinEngines.Silicon.DeadStartupButton;

public sealed class DeadStartupButtonSystem : SharedDeadStartupButtonSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly SiliconChargeSystem _siliconChargeSystem = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadStartupButtonComponent, OnDoAfterButtonPressedEvent>(OnDoAfter);
        SubscribeLocalEvent<DeadStartupButtonComponent, ElectrocutedEvent>(OnElectrocuted);
        SubscribeLocalEvent<DeadStartupButtonComponent, MobStateChangedEvent>(OnMobStateChanged);

    }

    private void OnDoAfter(EntityUid uid, DeadStartupButtonComponent comp, OnDoAfterButtonPressedEvent args)
    {
        if (args.Handled || args.Cancelled
            || !TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || !_mobState.IsDead(uid, mobStateComponent)
            || !TryComp<MobThresholdsComponent>(uid, out var mobThresholdsComponent)
            || !TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // Check if entity have critical state
        if (_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var criticalThreshold, mobThresholdsComponent)
            && damageable.TotalDamage < criticalThreshold)
        {
            _mobState.ChangeMobState(uid, MobState.Alive, mobStateComponent);
            return;
        }

        // Check if entity have dead state
        if (_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold, mobThresholdsComponent)
            && damageable.TotalDamage < deadThreshold)
        {
            _mobState.ChangeMobState(uid, MobState.Alive, mobStateComponent);
            return;
        }

        _audio.PlayPvs(comp.BuzzSound, uid, AudioHelpers.WithVariation(0.05f, _robustRandom));
        _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-failed", ("target", MetaData(uid).EntityName)), uid);
        Spawn("EffectSparks", Transform(uid).Coordinates);
    }

    private void OnElectrocuted(EntityUid uid, DeadStartupButtonComponent comp, ElectrocutedEvent args)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent)
            || !_mobState.IsDead(uid, mobStateComponent)
            || !_siliconChargeSystem.TryGetSiliconBattery(uid, out var bateria)
            || bateria.CurrentCharge <= 0)
            return;

        _lightning.ShootRandomLightnings(uid, 2, 4);
        _powerCell.TryUseCharge(uid, bateria.CurrentCharge);

    }

    private void OnMobStateChanged(EntityUid uid, DeadStartupButtonComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        _popup.PopupEntity(Loc.GetString("dead-startup-system-reboot-success", ("target", MetaData(uid).EntityName)), uid);
        _audio.PlayPvs(comp.Sound, uid);
    }

}