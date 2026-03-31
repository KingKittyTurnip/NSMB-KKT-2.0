using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public unsafe class PowerupAsset : CoinItemAsset, ISoundOverrideProvider {

    public int MaxMatchingPowerStates = 0;
    public int MaxMatchingReserveState = 0;
    public PowerupState State;

    public bool SoundPlaysEverywhere;
    public SoundEffect SoundEffect = SoundEffect.Player_Sound_PowerupCollect;

#if QUANTUM_UNITY
    public UnityEngine.Sprite ReserveSprite;
#endif

    public bool AvoidPlayers;
    public FP Speed;
    public FP BounceStrength;
    public FP TerminalVelocity;
    public FP BumpedFromBelowVelocity = Constants._5_50;

    public bool FollowAnimationCurve;
    public FPAnimationCurve AnimationCurveX;
    public FPAnimationCurve AnimationCurveY;

    public sbyte StatePriority = -1, ItemPriority = -1;
    public bool EnterReserveIfOverridden = true;
    public AssetRef<PowerupAsset> OnDamagedAsset; //this being null means mario dies, otherwise his powerupstate becomes this when damaged

    public SoundEffectOverride[] SfxOverrides;

    [Header("PowerupState Info")]
    public AssetRef<EntityPrototype> ProjectilePrototype;
    public PlayerSize SizeType = PlayerSize.Tall;

    //we'd prefer verious attributes tied to the powerup

    //public bool BreaksBricks = true;
    //public bool CanHoldItems = true;
    //public bool IsLightweight;
    //public bool DestroyesEverything;
    //public bool InvincibleState;

    //public bool HasShell;
    //public bool HasPropeller;
    //public bool HasHammerCrouch;

    //might as well use an enum since size is more player dependant
    public enum PlayerSize : byte {
        Small = 0,
        Tiny = 1,
        Tall = 2,
        
    }


    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
        base.Loaded(resourceManager, allocator);

        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public virtual int CountPlayersWithState(Frame f) {
        int playersWithPower = 0;
        foreach ((_, var otherPlayer) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            // check if another player matches the powerUP state
            if (otherPlayer->CurrentPowerupAsset == this) {
                playersWithPower++;
            }
        }
        return playersWithPower;
    }

    public virtual int CountPlayersWithReserve(Frame f) {
        int playersWithPower = 0;
        foreach ((_, var otherPlayer) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            // check if the powerUP asset in reserve matches us
            if (otherPlayer->ReserveItem == this) {
                playersWithPower++;
            }
        }
        return playersWithPower;
    }

    public override bool CanSpawn(Frame f, bool fromRouletteBlock) {
        if (!base.CanSpawn(f, fromRouletteBlock)) {
            return false;
        }

        if (MaxNumberOfItems > 0 && CountItemsExisting(f) >= MaxNumberOfItems) {
            return false;
        }

        if (MaxMatchingPowerStates > 0 && CountPlayersWithState(f) >= MaxMatchingPowerStates) {
            return false;
        }

        if (MaxMatchingReserveState > 0 && CountPlayersWithReserve(f) >= MaxMatchingReserveState) {
            return false;
        }

        return true;
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }

    public virtual unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

        // Reserve if it's the same item
        if (mario->CurrentPowerupAsset == this) {
            mario->SetReserveItem(f, this);
            return PowerupReserveResult.KeepOldReserveNew;
        }

        var previousPowerup = f.FindAsset(mario->CurrentPowerupAsset);
        sbyte currentPowerupStatePriority = previousPowerup != null ? previousPowerup.StatePriority : (sbyte) -1;

        // Reserve if we have a higher priority item
        if (currentPowerupStatePriority > ItemPriority) {
            mario->SetReserveItem(f, this);
            return PowerupReserveResult.KeepOldReserveNew;
        }

        OnCollected(f, marioEntity);

        //kill
        mario->PreviousPowerupState = mario->CurrentPowerupState;
        mario->CurrentPowerupState = State;

        mario->PreviousPowerupAsset = mario->CurrentPowerupAsset;
        mario->CurrentPowerupAsset = this;

        mario->IsPropellerFlying = false;
        mario->UsedPropellerThisJump = false;
        mario->IsDrilling &= mario->IsSpinnerFlying;
        mario->PropellerLaunchFrames = 0;
        mario->IsInShell = false;

        if (previousPowerup != null && previousPowerup.EnterReserveIfOverridden) {
            if (mario->CurrentPowerupState != PowerupState.NoPowerup) {
                mario->SetReserveItem(f, previousPowerup);
            }
            return PowerupReserveResult.CollectNewReserveOld;
        } else {
            return PowerupReserveResult.CollectNewIgnoreOld;
        }
    }

    protected virtual void OnCollected(Frame f, EntityRef entity) { }
}