using UnityEngine;

namespace Quantum {
    public unsafe partial struct Boss {
        public void BossHarmed(Frame f, EntityRef thisEntity, KnockbackStrength Power) {
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            byte total = Power switch {
                KnockbackStrength.Groundpound => 8,
                KnockbackStrength.FireballBump => 1,
                KnockbackStrength.Normal => 4,
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
                    f.Signals.BossDeath(thisEntity);
                    break;
                }
            }
            boss->iframes = 180;
        }
    }
}