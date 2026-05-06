using Photon.Deterministic;
using Quantum;
using Quantum.Collections;
using Quantum.Core;
using Quantum.Profiling;
using System;
using System.Drawing.Drawing2D;

namespace Quantum {
    public unsafe partial struct Bot {
        //AI Plans:
        //TOTEST: It Will Not Target Powerups If It Already Has Powerups
        //TODO: Be Able To Properly Use Shell
        //Other Powerup Later
        //TODO: Allow Current Placement In The Game To Influence The Game
        //TODO: Advanced AI, When Idling Make Them not Overlap A Star Space
        //TODO: Tile Detection To Get Powerups
        //TODO: Add Advanced Vertical Navigation
        //TODO: make it not use groundpound if there is an obstical in the way torwards the target
        //TODO: Take Advantage Of Slopes & Slope Speed (check the floorangle of the player)
        //TODO: Walljumps (like in pipes)
        //TODO: use raycasts to check if it's falling into a pit

        //TODO: Swimming
        //TODO: Pipe Entering
        //TODO: Spinner Usage
        //TODO: Constantly Jump On Ice
        //TODO: Enemy Avoidence
        //TODO: Carryables Functionality

        private void SetAvoid(Frame f, EntityRef marioEntity, FPVector2 Pos, int i) {
            if (i > AvoidType) {
                AvoidType = (byte) i; //0 = nada, 1 = Grounded Enemy, 2 = Hazard Above, 3 = Weak Player, 4 = Powerful Player
                Avoid = Pos;
            }
        }

        public Input HandleAi(Frame f, EntityRef marioEntity) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleAi");
            //ref var inputs = ref filter.Inputs;
            Input inputs = new Input();
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var Reserve = f.FindAsset(mario->ReserveItem);
            MarioPlayerPhysicsInfo physics = f.FindAsset(mario->PhysicsAsset);
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            if (!Activated) {
                Activated = true;
                //f.Global->RealPlayers++;
                mario->CurrentPowerupState = PowerupState.FireFlower;
            }

            //Loose Values
            bool Attack = false, Turnaround = false, AvoidThis = false, AbovePit = false, ATargetBellow = false; //, SnappyBack = false;
            bool LackingPowerups = mario->CurrentPowerupState <= PowerupState.Mushroom /*|| mario->CurrentPowerupState <= PowerupState.JumpSuit*/ || (Reserve != null && Reserve.StatePriority < 2);
            FPVector2 marioPos = transform->Position;
            FP TargetFoeStars = 0;

            //Get The Target Location
            FP distance = 9999;
            FP distanceModifier = 0;
            FPVector2 Spot = new FPVector2(mario->FacingRight ? 999999 : -999999, 999999), posA = Spot, posB = Spot, avoA = Spot, avoB = Spot;
            Target = Avoid = Spot;
            AvoidType = 0;

            //Check For Nearby Stars --- Done ---
            var stars = f.Filter<BigStar>();
            while (stars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot, out posA, out posB);
                FP tempDistance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                if (tempDistance < distance) {
                    distance = tempDistance;
                    Target = Spot;
                    distanceModifier = FPMath.Min(3 - distance);
                }
            }

            //Check For Nearby Players
            var players = f.Filter<MarioPlayer>();
            while (players.NextUnsafe(out EntityRef entity, out MarioPlayer* marioPlayer)) {
                Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot, out posA, out posB);
                FP tempDistance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                if (tempDistance < distance - distanceModifier) {
                    FP vel = f.Unsafe.GetPointer<PhysicsObject>(entity)->Velocity.X;
                    var marioFoe = f.Unsafe.GetPointer<MarioPlayer>(entity);
                    if (mario == marioFoe) //Hey... That's Me!
                        continue;

                    if (Personality == 3) {
                        SetAvoid(f, marioEntity, Spot, 4);
                        Attack = distance < 7;
                    } else if (distance < 9 && (marioFoe->CurrentPowerupState == PowerupState.MegaMushroom || marioFoe->IsStarmanInvincible)) { //Avoid Mega Players At All Costs
                        SetAvoid(f, marioEntity, Spot, 4);
                    } else if (marioFoe->IsPropellerFlying || marioFoe->IsSpinnerFlying) { //Man Darn Propeller Spammers, Avoid Them
                        SetAvoid(f, marioEntity, Spot, 2);
                    } else if (marioFoe->GamemodeData.StarChasers->Stars == 0) { //This Player Isn't Worth The Hassle
                        SetAvoid(f, marioEntity, Spot, 3);
                    } else if ((TargetFoeStars < marioFoe->GamemodeData.StarChasers->Stars) && mario->GetTeam(f) != marioFoe->GetTeam(f) && ((vel > 0 && posA.X - posB.X > 0) || (vel < 0 && posA.X - posB.X < 0) || Personality == 2 || distanceModifier <= (posA.Y + 2 > posB.Y ? -2 : 0))) { // && tempDistance < distance + (marioFoe->Stars - mario->Stars)) {
                        ATargetBellow = posA.Y - posB.Y > 1 && FPMath.Abs(posA.X - posB.X) < Constants._0_40;
                        if (mario->CurrentPowerupState > PowerupState.Mushroom) {
                            TargetFoeStars = marioFoe->GamemodeData.StarChasers->Stars;
                            distance = tempDistance;
                            Target = Spot;
                            Attack = marioFoe->InvincibilityFrames <= 15 && !marioFoe->IsInKnockback;
                        }
                    }
                }
            }

            //Check For Powerups & Coins
            //TODO: Give Massive Priority Increase To Catchup Powerups
            if (LackingPowerups) {
                var powerups = f.Filter<Powerup>();
                while (powerups.NextUnsafe(out EntityRef entity, out Powerup* powerup)) {
                    Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                    QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot, out posA, out posB);
                    FP tempDistance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                    if (tempDistance < distance - (mario->CurrentPowerupState <= PowerupState.Mushroom ? distanceModifier : 0) && tempDistance < 3) {
                        distance = tempDistance;
                        Target = Spot;
                        Attack = false;
                    }
                }
                /* 
                //Coins Are Weird To Check For
                //            var coins = f.Filter<Coin>();
                //            while (coins.NextUnsafe(out EntityRef entity, out Coin* coin)) {
                //              Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                //              QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot, out posA, out posB);
                //              FP tempDistance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                //              if (tempDistance < distance && tempDistance < 1 && !f.Unsafe.GetPointer<Coin>(entity)->IsCollected) {
                //                  distance = tempDistance;
                //                  mario->Target = Spot;
                //              }
                //            }
                */
            }
            //Check For Tiles
            //TODO: Checking For Tiles
            //TODO: Allow Powerup & Coin Block Tiles To Gain Some Priority If Lacking Powerups
            //TODO: Check For Tiles In Front Of The Player To Jump Early
            //TODO: If Target Bellow Tiles But The Tiles Bellow Aren't Bricks Then Don't gp, Tell it To Walk Around

            if (physicsObject->IsTouchingCeiling)
                BotWallJumping = 0;
            else
                QuantumUtils.Decrement(ref BotWallJumping);

            //Pit Check
            AbovePit = BotWallJumping > 0 || !PhysicsObjectSystem.Raycast(f, stage, marioPos, FPVector2.Down, 18, out var hit);

            //Determine Target Difference
            QuantumUtils.UnwrapWorldLocations(stage, marioPos, Target, out posA, out posB);
            FPVector2 Diffrence = new FPVector2(posA.X - posB.X, posA.Y - posB.Y);
            QuantumUtils.UnwrapWorldLocations(stage, marioPos, Avoid, out avoA, out avoB);
            FPVector2 AvoidDif = new FPVector2(avoA.X - avoB.X, avoA.Y - avoB.Y);
            QuantumUtils.UnwrapWorldLocations(stage, posB, avoB, out FPVector2 tempA, out FPVector2 tempB);
            FPVector2 TarvoidDif = new FPVector2(tempA.X - tempB.X, tempA.Y - tempB.Y);

            //LeftRightInputs
            if (BotWallJumping > 0 || AbovePit || (AvoidType == 2 && FPMath.Abs(avoA.X - avoB.X) < 1)) { //Keep Last Inputs
                inputs.Left = (bool) !PressingRight;
                inputs.Right = (bool) PressingRight;
            } else if (AvoidType == 4 || (AvoidType == 3 && FPMath.Abs(TarvoidDif.X) < 2)) { //Avoid Danger
                inputs.Left = avoB.X > avoA.X;
                inputs.Right = !inputs.Left;
                PressingRight = inputs.Right == true;
            } else if (FPMath.Abs(physicsObject->Velocity.X) <= 7) { //Input Nothing For Slope Speed
                inputs.Left = posB.X < posA.X;
                inputs.Right = !inputs.Left;
                PressingRight = inputs.Right == true;
            }

            //Gp To Turn Around Quickly If Target is In Opposite Direction Far Away Enough
            Turnaround = (physicsObject->Velocity.X > 3 && Diffrence.X > 1) || (physicsObject->Velocity.X < -3 && Diffrence.X < -1);

            //Handle Jump
            if (physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)
                inputs.Jump = false;
            if ((!physicsObject->IsTouchingGround && physicsObject->WasTouchingGround && Diffrence.Y < 0 /*Constants._0_40*/)
                || (mario->IsWallsliding && Diffrence.Y < Constants._0_40) || (!physicsObject->WasTouchingGround && physicsObject->Velocity.Y > -1) || physicsObject->IsUnderwater
                || (physicsObject->IsTouchingGround && ((FPMath.Abs(Diffrence.X) > Diffrence.Y && !(AvoidType == 2 && FPMath.Abs(TarvoidDif.X) < 1)) || (mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall)))
                ) {
                inputs.Jump = true;
                if (mario->IsWallsliding) {
                    BotWallJumping = (byte) 30;
                    PressingRight = !PressingRight;
                }
            }

            //Up Input
            if ((mario->IsGroundpounding && Diffrence.Y < FP._0_20)
             || (mario->CurrentPowerupState == PowerupState.HammerSuit && Diffrence.Y < -1)
             || (Turnaround && mario->IsGroundpounding && physicsObject->Velocity.Y < -Constants._0_40))
                inputs.Up = true;

            //Gp If Bellow
            if (!AbovePit && (((Diffrence.Y > FP._0_50 && (FPMath.Abs(Diffrence.X) < Constants._0_40) || ATargetBellow)
              && physicsObject->Velocity.Y < 3 && !physicsObject->IsTouchingGround)
              || (Turnaround && !inputs.Up))) {
                inputs.Left = false;
                inputs.Right = false;
                inputs.Down = true;
                //mario->GroundpoundCooldownFrames = 1;
            }
            //Crouch/Slide
            if (physicsObject->IsTouchingGround && physicsObject->Velocity.X != 0 && (physicsObject->IsOnSlideableGround || FPMath.Abs(physicsObject->Velocity.X) > 7)) {
                inputs.Down = true;
                inputs.Jump = FPMath.Abs(physicsObject->Velocity.X) > 7;
            }

            //Projectile Powerups
            QuantumUtils.Decrement(ref BotAtkCooldown);
            if (Attack && BotAtkCooldown <= 0
              && ((mario->CurrentPowerupState == PowerupState.FireFlower && Diffrence.Y > -2)
              || (mario->CurrentPowerupState == PowerupState.IceFlower && Diffrence.Y > -2)
              || (mario->CurrentPowerupState == PowerupState.HammerSuit && Diffrence.Y < 2)
              || (mario->CurrentPowerupState == PowerupState.PropellerMushroom && Diffrence.Y < 0)
              || (mario->CurrentPowerupState == PowerupState.FireFlower && Diffrence.Y > -2))) {
                inputs.PowerupAction = true;
                BotAtkCooldown = (byte) f.RNG->Next(3, 45);
            }

            //Always Sprint
            inputs.Sprint = true;

            //Take out Reserve
            //TODO: Get Rid Of Bad Powerups (Ex: Mini & Jumpsuit)
            if (mario->CurrentPowerupState <= PowerupState.Mushroom
              || (Personality == 2 && (mario->CurrentPowerupState == PowerupState.BlueShell || mario->CurrentPowerupState == PowerupState.PropellerMushroom))
              || (Personality == 3 && (mario->CurrentPowerupState == PowerupState.FireFlower || mario->CurrentPowerupState == PowerupState.IceFlower)))
                if (Reserve != null)
                    MarioPlayerSystem.BotReserve(f, marioEntity);

            return inputs;
        }
    }
}