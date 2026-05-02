using Photon.Deterministic;
using UnityEngine.Diagnostics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.ParticleSystem;
using static UnityEngine.UI.Image;
using Quantum.Collections;
using UnityEngine.UIElements;
using System.Drawing.Drawing2D;

namespace Quantum {
    
    public unsafe class ChainChompSystem : SystemMainThreadEntityFilter<ChainChomp, ChainChompSystem.Filter>, ISignalOnEntityBumped,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {

        private int LungeSpeed = 12, ReturnSpeed = 8;
        private FP ChompTime = FP._1_50, IdleTime = 3, ShortTime = FP._0_50;
        private FP LungeLimit = Constants._2_50;
        private FP TargetLungeLimit = FP._1_50;
        private FP JumpHeight = 2;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public ChainChomp* ChainChomp;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;

            public Hazard* hazard;
        }

        //Hop on a wall bug (idk what to call it sob)
        //make the chainchomp ignore the owner if the post is considered thrown

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<ChainChomp, MarioPlayer>(f, OnChainChompMarioInteraction);

            f.Context.Interactions.Register<ChainChomp, Goomba>(f, OnChompGoombaInteraction);
            f.Context.Interactions.Register<ChainChomp, Koopa>(f, OnChompKoopaInteraction);
            f.Context.Interactions.Register<ChainChomp, Bobomb>(f, OnChompBobombInteraction);
            f.Context.Interactions.Register<ChainChomp, BulletBill>(f, OnChompBillInteraction);
            f.Context.Interactions.Register<ChainChomp, Boo>(f, OnChompBooInteraction);
            f.Context.Interactions.Register<ChainChomp, Podobo>(f, OnChompPPodoboInteraction);
            f.Context.Interactions.Register<ChainChomp, PiranhaPlant>(f, OnChompPlantInteraction);

            f.Context.Interactions.Register<ChainChomp, Boss>(f, OnChompBossInteraction);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var chainchomp = filter.ChainChomp;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var hazard = filter.hazard;
            var entity = filter.Entity;

            if (!f.Exists(chainchomp->Post)) {
                //a chain chomp is on the loose!
                TryHop(6, 8, false);
                if (hazard->DoNotDespawnInPit) {
                    hazard->DoNotDespawnInPit = false;
                    chainchomp->State = ChainChompState.Idle;
                    physicsObject->IsFrozen = false;
                    physicsObject->DisableCollision = false;
                }
                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    chainchomp->FacingRight = physicsObject->IsTouchingLeftWall;
                }
                return;
            }

            var DisPostLength = 3 * (f.Unsafe.GetPointer<ThrowingObject>(chainchomp->Post)->Varient == 1 ? 2 : 1);
            QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(chainchomp->Post)->Position + (FPVector2.Up * FP._0_10), out FPVector2 thisPos, out FPVector2 postPos);
            FP distance = FPVector2.Distance(thisPos, postPos);

            if (distance > DisPostLength) {
                SwitchState(ChainChompState.Return);
            }

            var Owner = f.Unsafe.GetPointer<Holdable>(chainchomp->Post)->Holder;

            QuantumUtils.Decrement(f, ref chainchomp->LastBumpedTimer);

            switch (chainchomp->State) {
            case ChainChompState.Idle:
                if ((chainchomp->ReusableTimer += f.DeltaTime) > (f.Exists(Owner) ? ShortTime : IdleTime) && physicsObject->IsTouchingGround) {
                    //Get A Target
                    Boss.GetClosestPlayer(f, transform->Position, Owner, out var TargetEntity, out var distanceTE);
                    FP limit = DisPostLength + TargetLungeLimit;

                    if (distanceTE > limit) {
                        #region Attack boss Or Enemy Instead, It's Messy Code Dw About It
                        if (Owner != EntityRef.None) {
                            //Try Find A Boss
                            TargetEntity = EntityRef.None;
                            distanceTE = 999;
                            var bosses = f.Filter<Boss>();

                            while (bosses.NextUnsafe(out EntityRef bossEntity, out Boss* boss)) {
                                if (boss->ControllerPlayer == Owner || boss->Dead)
                                    continue;
                                //Find Closest Boss
                                QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(bossEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                                FP e = FPVector2.Distance(ourPos, theirPos);
                                if (e < distanceTE) {
                                    TargetEntity = bossEntity;
                                    distanceTE = e;
                                }
                            }
                            if (distanceTE > limit) {
                                //Try Find A Boss
                                TargetEntity = EntityRef.None;
                                distanceTE = 999;
                                var enemies = f.Filter<Enemy>();

                                while (enemies.NextUnsafe(out EntityRef enemyEntity, out Enemy* enemy)) {
                                    if (!enemy->IsAlive)
                                        continue;
                                    //Find Closest Enemy
                                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(enemyEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                                    FP e = FPVector2.Distance(ourPos, theirPos);
                                    if (e < distanceTE) {
                                        TargetEntity = enemyEntity;
                                        distanceTE = e;
                                    }
                                }
                                if (distanceTE <= limit) {
                                    GotTarget();
                                    break;
                                }
                            } else {
                                GotTarget();
                                break;
                            }
                        }
                        #endregion
                        StopCheck();
                    } else {
                        GotTarget();
                        break;
                    }
                    void GotTarget() {
                        SwitchState(ChainChompState.Prepare);
                        chainchomp->AttackTarget = TargetEntity;
                        GetTargetPosition();
                    }
                    void StopCheck() {
                        //Couldn't Find Target, Check Again In 1 Second
                        chainchomp->ReusableTimer -= ShortTime;
                    }
                }
                TryHop(FP._1_50, JumpHeight, true);
                if (distance > DisPostLength - FP._1_50) { //idk this is kinda a random location for it but it works
                    chainchomp->FacingRight = thisPos.X < postPos.X;
                }
                break;
            case ChainChompState.Prepare:
                GetTargetPosition();

                if ((chainchomp->ReusableTimer += f.DeltaTime) > FP._0_33 && physicsObject->IsTouchingGround) {
                    SwitchState(ChainChompState.Lunge);
                    chainchomp->LastBumpedTimer = FP._0_05;
                    f.Events.ChainChompSound(entity, true);
                }
                TryHop(0, JumpHeight, true);
                break;
            case ChainChompState.Lunge:
                ShiftTargetPos(true);

                var TargetDirection = FPMath.Atan2(chainchomp->TargetPosition.Y - transform->Position.Y, chainchomp->TargetPosition.X - transform->Position.X);
                physicsObject->Velocity = new FPVector2(FPMath.Cos(TargetDirection), FPMath.Sin(TargetDirection)) * LungeSpeed;
                chainchomp->TargetPosition += physicsObject->Velocity * f.DeltaTime;

                FP Cap = LungeLimit + (DisPostLength > 3 ? 3 : 0);

                if (distance > Cap && chainchomp->LastBumpedTimer <= 0) {
                    SwitchState(ChainChompState.Chomp);
                    transform->Position = postPos + ((thisPos - postPos).Normalized * Cap);
                }
                break;
            case ChainChompState.Chomp:
                chainchomp->ReusableTimer += f.DeltaTime;

                ShiftTargetPos(true);

                if (chainchomp->ReusableTimer > ChompTime) {
                    SwitchState(ChainChompState.Return);
                }
                break;
            case ChainChompState.Return:
                if (distance < FP._0_20) {
                    //We've Reached The Post
                    SwitchState(ChainChompState.Idle);
                    transform->Position = postPos;
                    break;
                }
                var TargetDirection2 = FPMath.Atan2(postPos.Y - transform->Position.Y, postPos.X - transform->Position.X);
                physicsObject->Velocity = new FPVector2(FPMath.Cos(TargetDirection2), FPMath.Sin(TargetDirection2)) * (ReturnSpeed + (f.Unsafe.GetPointer<PhysicsObject>(chainchomp->Post)->Velocity.Magnitude * FP._0_50));
                break;
            }

            chainchomp->PreviousPostPosition = postPos;

            void TryHop(FP Speed, FP Jump, bool Ledge) {
                //Basic Movement Behavior
                if (physicsObject->IsTouchingGround) {
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.X = chainchomp->FacingRight ? Speed : -Speed;
                    physicsObject->Velocity.Y = Jump;
                    f.Events.ChainChompSound(entity, false);
                }
                if (Speed != 0) {
                    //Check if We Are near Ground
                    FPVector2 checkPosition = transform->Position - (FPVector2.Down * FP._0_10);
                    if (PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_33, out var hit)) {

                        checkPosition = transform->Position + new FPVector2(FP._0_20 * (chainchomp->FacingRight ? 1 : -1), -FP._0_10);
                        if (Ledge && !PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, Constants._0_66, out var hit2)) {
                            //Ledge Checks
                            chainchomp->FacingRight = !chainchomp->FacingRight;

                        } else if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                            //Wall Checks
                            checkPosition = transform->Position + new FPVector2(0, FP._0_33);
                            if (PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Right * (chainchomp->FacingRight ? 1 : -1), FP._0_50, out var hit3)) {
                                chainchomp->FacingRight = physicsObject->IsTouchingLeftWall;
                            } else {
                                physicsObject->Velocity.Y = 5;
                            }
                        }
                    }
                    physicsObject->Velocity.X = chainchomp->FacingRight ? Speed : -Speed;
                }
            }
            void SwitchState(ChainChompState newstate) {
                //Switch And Setup State
                chainchomp->ReusableTimer = 0;
                chainchomp->State = newstate;
                switch (chainchomp->State) {
                case ChainChompState.Idle:
                    physicsObject->IsFrozen = false;
                    physicsObject->DisableCollision = false;
                    physicsObject->Velocity = FPVector2.Zero;
                    break;
                case ChainChompState.Prepare:
                    physicsObject->IsFrozen = false;
                    physicsObject->DisableCollision = false;
                    physicsObject->Velocity = FPVector2.Zero;
                    break;
                case ChainChompState.Lunge:
                    physicsObject->IsFrozen = false;
                    physicsObject->DisableCollision = true;
                    physicsObject->Velocity = FPVector2.Zero;
                    break;
                case ChainChompState.Chomp:
                    physicsObject->IsFrozen = true;
                    physicsObject->DisableCollision = true;
                    physicsObject->Velocity = FPVector2.Zero;
                    transform->Position = postPos + ((thisPos - postPos).Normalized * LungeLimit);
                    break;
                case ChainChompState.Return:
                    physicsObject->IsFrozen = false;
                    physicsObject->DisableCollision = true;
                    physicsObject->Velocity = FPVector2.Zero;
                    if (distance > DisPostLength) {
                        transform->Position = postPos + ((thisPos - postPos).Normalized * DisPostLength);
                    }
                    chainchomp->TargetPosition = transform->Position;
                    break;
                }
            }
            void ShiftTargetPos(bool ChompToo) {
                chainchomp->TargetPosition += postPos - chainchomp->PreviousPostPosition;
                if (ChompToo)
                    transform->Position += postPos - chainchomp->PreviousPostPosition;
            }
            void GetTargetPosition() {
                QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(chainchomp->AttackTarget)->Position + (FPVector2.Up * FP._0_50), out FPVector2 ourPos, out FPVector2 theirPos);
                chainchomp->TargetPosition = theirPos;
                chainchomp->FacingRight = ourPos.X < theirPos.X;
            }
        }

        public static void TryReturnAfterAttack(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var chainchomp = f.Unsafe.GetPointer<ChainChomp>(thisEntity);
            if (chainchomp->State == ChainChompState.Lunge || chainchomp->State == ChainChompState.Chomp) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
                var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
                var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

                QuantumUtils.UnwrapWorldLocations(f, transform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                chainchomp->ReusableTimer = 3;
                chainchomp->State = ChainChompState.Idle;
                physicsObject->IsFrozen = false;
                physicsObject->DisableCollision = false;
                physicsObject->Velocity = new FPVector2(damageDirection.X < 0 ? -3 : 3, 6);
                chainchomp->TargetPosition = transform->Position;
            }
        }

        #region Interactions

        public static void OnChainChompMarioInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            f.Unsafe.TryGetPointer<MarioPlayer>(otherEntity, out var mario);
            if (mario->IsDead) {
                return;
            }
            var chainchomp = f.Unsafe.GetPointer<ChainChomp>(thisEntity);
            bool PartOfPost = f.Exists(chainchomp->Post);
            if (chainchomp->State == ChainChompState.Return || (f.Exists(chainchomp->Post) && f.Unsafe.GetPointer<Holdable>(chainchomp->Post)->Holder == otherEntity)) {
                return;
            }

            #region SetValues
            var marphys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool FromRight = damageDirection.X < 0;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;
            bool Kill = mario->InstakillsEnemies(marphys, false);
            #endregion

            if (Kill) {
                //Kill Chomp
            } else if (chainchomp->State == ChainChompState.Lunge || chainchomp->State == ChainChompState.Chomp || !PartOfPost) {
                if (mario->DoKnockback(f, otherEntity, FromRight, PartOfPost ? 2 : 1, KnockbackStrength.CollisionBump, thisEntity)) {
                    FPVector2 particlePos = (theirPos + ourPos) / 2;
                    f.Events.PlayKnockbackEffect(otherEntity, thisEntity, KnockbackStrength.CollisionBump, particlePos);
                    TryReturnAfterAttack(f, thisEntity, otherEntity);
                    chainchomp->LastBumped = otherEntity;
                    chainchomp->LastBumpedTimer = 1;
                }
            } else if (!(chainchomp->LastBumped == otherEntity && chainchomp->LastBumpedTimer > 0)) {
                chainchomp->LastBumped = otherEntity;
                chainchomp->LastBumpedTimer = 1;
                if (chainchomp->State != ChainChompState.Prepare)
                    chainchomp->FacingRight = FromRight;

                marphys->Velocity.X = FPMath.Min(FPMath.Abs(marphys->Velocity.X), 4) * (FromRight ? -1 : 1);
                if (attackedFromAbove) {
                    mario->DoEntityBounce = true;
                } else {
                    FPVector2 particlePos = (theirPos + ourPos) / 2;
                    f.Events.PlayKnockbackEffect(otherEntity, thisEntity, KnockbackStrength.CollisionBump, particlePos);
                }
            }
        }


        public static void OnChompGoombaInteraction(Frame f, EntityRef chompEntity, EntityRef goombaEntity) {
            f.Unsafe.GetPointer<Goomba>(goombaEntity)->Kill(f, goombaEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, goombaEntity);
        }
        public static void OnChompKoopaInteraction(Frame f, EntityRef chompEntity, EntityRef koopaEntity) {
            f.Unsafe.GetPointer<Koopa>(koopaEntity)->Kill(f, koopaEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, koopaEntity);
        }
        public static void OnChompBobombInteraction(Frame f, EntityRef chompEntity, EntityRef bobombEntity) {
            f.Unsafe.GetPointer<Bobomb>(bobombEntity)->Kill(f, bobombEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, bobombEntity);
        }
        public static void OnChompBillInteraction(Frame f, EntityRef chompEntity, EntityRef billEntity) {
            f.Unsafe.GetPointer<BulletBill>(billEntity)->Kill(f, billEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, billEntity);
        }
        public static void OnChompBooInteraction(Frame f, EntityRef chompEntity, EntityRef booEntity) {
            f.Unsafe.GetPointer<Boo>(booEntity)->Kill(f, booEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, booEntity);
        }
        public static void OnChompPPodoboInteraction(Frame f, EntityRef chompEntity, EntityRef podoboEntity) {
            f.Unsafe.GetPointer<Podobo>(podoboEntity)->Kill(f, podoboEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, podoboEntity);
        }
        public static void OnChompPlantInteraction(Frame f, EntityRef chompEntity, EntityRef plantEntity) {
            f.Unsafe.GetPointer<PiranhaPlant>(plantEntity)->Kill(f, plantEntity, chompEntity, EnemyKillReason.Special);
            TryReturnAfterAttack(f, chompEntity, plantEntity);
        }
        public static void OnChompBossInteraction(Frame f, EntityRef chompEntity, EntityRef bossEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->Dead)
                return;
            var chainchomp = f.Unsafe.GetPointer<ChainChomp>(chompEntity);
            if (chainchomp->State == ChainChompState.Return) {
                return;
            }

            #region SetValues
            var marphys = f.Unsafe.GetPointer<PhysicsObject>(bossEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(bossEntity);
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(chompEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(chompEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool FromRight = damageDirection.X < 0;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;
            #endregion

            if (chainchomp->State == ChainChompState.Lunge || chainchomp->State == ChainChompState.Chomp) {
                boss->BossHarmed(f, bossEntity, !FromRight, KnockbackStrength.FireballBump, false);
                TryReturnAfterAttack(f, chompEntity, bossEntity);
            } else if (!(chainchomp->LastBumped == bossEntity && chainchomp->LastBumpedTimer > 0)) {
                chainchomp->LastBumped = bossEntity;
                chainchomp->LastBumpedTimer = 1;
                if (chainchomp->State != ChainChompState.Prepare)
                    chainchomp->FacingRight = FromRight;

                boss->BossBump(f, bossEntity, !FromRight, KnockbackStrength.FireballBump);
                FPVector2 particlePos = (theirPos + ourPos) / 2;
                f.Events.PlayKnockbackEffect(bossEntity, chompEntity, KnockbackStrength.CollisionBump, particlePos);
            }
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out LemmyBall* chainchomp)) {

                return;
            }

            f.Events.PlayComboSound(entity, 0);
            physicsObject->IsTouchingGround = false;
            physicsObject->Velocity.Y = 5;
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out ChainChomp* chainchomp)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            chainchomp->Post = owner;

            if (f.Unsafe.TryGetPointer(owner, out Hazard* ownerhazard)) {
                hazard->BaseLifeTime = hazard->LifeTime = ownerhazard->LifeTime;
            }
        }
        #endregion
    }
}
