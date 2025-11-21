using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    public unsafe partial struct Boss {
        public void BossHarmed(Frame f, EntityRef thisEntity, KnockbackStrength Damage, bool longiframes) {
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
                //Ai, Wittle Down hp
                byte total = Damage switch {
                    KnockbackStrength.Groundpound => 6,
                    KnockbackStrength.FireballBump => 1,
                    KnockbackStrength.Normal => 3,
                    _ => 0,
                };

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
            physicsObject->Velocity.X = 0;
            physicsObject->IsTouchingGround = false;
            if (physicsObject->Velocity.Y < 0)
                physicsObject->Velocity.Y /= 2;

            f.Events.PlayBossHitSound(thisEntity);
        }

        public void MakeBossControllable(Frame f, EntityRef MarioEntity) {
            ControllerPlayer = MarioEntity;
        }
    }
}