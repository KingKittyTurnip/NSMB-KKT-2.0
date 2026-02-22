using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe partial struct Boss {
        public void BossHarmed(Frame f, EntityRef thisEntity, bool FromRight, KnockbackStrength Damage, bool longiframes) {
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            if (boss->iframes != 0)
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
    }
}