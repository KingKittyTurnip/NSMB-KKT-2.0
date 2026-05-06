using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe partial struct Boss {
        public void BossHarmed(Frame f, EntityRef thisEntity, bool FromRight, KnockbackStrength Damage, bool longiframes) {
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            if (boss->iframes != 0 || boss->Dead)
                return;

            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player, Just Drop
                f.Signals.OnMarioPlayerDropObjective(boss->ControllerPlayer, Damage == KnockbackStrength.Groundpound ? 2 : 1, EntityRef.None);
                boss->iframes = 120;
            } else {
                byte total = Damage switch {
                    KnockbackStrength.Groundpound => 6,
                    KnockbackStrength.FireballBump => 1,
                    KnockbackStrength.Normal => 3,
                    _ => 0,
                };
                //Ai, Wittle Down hp
                if (hazard->IsHazard) {
                    //if we fight the boss boost it's lifetime relitive to it's total lifetime (using 6 instead of 12 intentionally)
                    hazard->LifeTime += (hazard->BaseLifeTime/6)*total;
                }

                //Decrease It's Health until We Deplete it Or Lost all power
                for (var i = 0; i < total; i++) {
                    if (boss->Health > 0) {
                        boss->Health--;
                    } else {
                        boss->Dead = true;
                        f.Events.BossDeathAnimation(thisEntity);
                        f.Signals.BossDeath(thisEntity);

                        hazard->LifeTime = 130;

                        if (boss->ControllerPlayer != EntityRef.None) {
                            //Controlled By Player
                            var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                            mario->RelieveFromBoss(f, boss->ControllerPlayer);
                        } else {
                            //spawn gamemode objectives
                            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                            var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
                            EntityRef newStarEntity = f.Create(gamemode.BigStarPrototype);
                            var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                            f.Unsafe.GetPointer<Transform2D>(newStarEntity)->Position = f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position;
                            newStar->InitializeMovingStar(f, stage, newStarEntity, boss->FacingRight ? 1 : 2);
                        }
                        return;
                    }
                }
                boss->iframes = (byte)(longiframes ? 25 : 10);
            }
            BossBump(f, thisEntity, FromRight, Damage);
            physicsObject->IsTouchingGround = false;
            if (physicsObject->Velocity.Y < 0)
                physicsObject->Velocity.Y /= 2;

            f.Events.PlayBossHitSound(thisEntity);
        }

        public FP BossBump(Frame f, EntityRef thisEntity, bool FromRight, KnockbackStrength Damage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            FP total = Damage switch {
                KnockbackStrength.Groundpound => 6,
                KnockbackStrength.FireballBump => 3,
                KnockbackStrength.Normal => 3,
                _ => 0,
            };
            total *= (FromRight ? 1 : -1);
            physicsObject->Velocity.X = (physicsObject->Velocity.X/2) + total;

            //event a knockback anim
            return -total;
        }

        public void MakeBossControllable(Frame f, EntityRef MarioEntity) {
            ControllerPlayer = MarioEntity;
        }

        public bool BossCanInteractWithPlayer(Frame f, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            //only interact if neither are dead, if this is a normal boss go ahead, if it's player controlled, wait til iframes are out on both
            return !mario->IsDead && !Dead && (ControllerPlayer == EntityRef.None || (iframes == 0 && mario->DamageInvincibilityFrames == 0));
        }

        public static void GetClosestPlayer(Frame f, FPVector2 OurPosition, EntityRef IgnoreThisPlayer, out EntityRef TargetEntity, out FP distance) {
            TargetEntity = EntityRef.None;
            distance = 999;
            var players = f.Filter<MarioPlayer>();

            while (players.NextUnsafe(out EntityRef playerEntity, out MarioPlayer* mar)) {
                if (mar->IsDead || playerEntity == IgnoreThisPlayer)
                    continue;
                //Find Closest Player
                QuantumUtils.UnwrapWorldLocations(f, OurPosition, f.Unsafe.GetPointer<Transform2D>(playerEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FP e = FPVector2.Distance(ourPos, theirPos);
                if (e < distance) {
                    TargetEntity = playerEntity;
                    distance = e;
                }
            }
        }

        //get who is responsible when dealing which owners
        public EntityRef BossGetOwnerResponsible(EntityRef thisEntity) {
            return ControllerPlayer != EntityRef.None ? ControllerPlayer : thisEntity;
        }

        //interactions
        public bossMarioContactResult BossMarioContact(Frame f, EntityRef thisEntity, EntityRef marioEntity, FPVector2 damageDirection, bool BossAttackingSpecial, bool DontHarmMario = false) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_50;
            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;

            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                //mario is invincible
                BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.Groundpound, true);
                return bossMarioContactResult.SuperHarm;
            } else if (BossAttackingSpecial) {
                //boss is doing some sort of special attack (like a groundpound), lets check it first
                return bossMarioContactResult.Special;
            } else if(attackedFromAbove) {
                //mario stomped us
                mario->DoEntityBounce = mario->CurrentPowerupState == PowerupState.MiniMushroom || !mario->IsGroundpounding;
                mario->IsDrilling = false;
                marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X + ((damageDirection.X > 0 ? 1 : -1) * 3), -5, 5);
                return mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding ? bossMarioContactResult.None : bossMarioContactResult.Harm;
            } else if (damageDirection.Y < -FP._0_10 && !mario->IsInKnockback && !DontHarmMario) {
                //stomp mario
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 1, KnockbackStrength.Normal, BossGetOwnerResponsible(thisEntity));
                return bossMarioContactResult.Above;
            } else if (marioPhysicsObject->IsTouchingGround && !DontHarmMario) {
                //bump against mario
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 1, KnockbackStrength.CollisionBump, ControllerPlayer != EntityRef.None ? ControllerPlayer : thisEntity);
                return bossMarioContactResult.Bump;
            } else if (!mario->IsInKnockback) {
                //airbump with mario
                marioPhysicsObject->Velocity.X = (marioPhysicsObject->Velocity.X/2) + BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
            }
            return bossMarioContactResult.None;
        }

        //USED FOR ANIMATORS, DON'T USE FOR SCRIPTS
        public bool BossAnimator_ShowModel(Frame f) {
            return f.Global->GameState >= GameState.Playing && (ControllerPlayer == EntityRef.None || (!(iframes > 0 && (f.Number * f.DeltaTime.AsFloat) * (iframes <= 0.75f ? 5 : 2) % 0.2f < 0.1f)));
        }
        public float BossAnimator_GetRedness() {
            return ControllerPlayer == EntityRef.None ? Mathf.Min(iframes/10f, 0.85f) : 0f;
        }
    }
}