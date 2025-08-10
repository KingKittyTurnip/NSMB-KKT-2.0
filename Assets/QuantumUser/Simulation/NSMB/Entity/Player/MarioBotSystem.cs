using Photon.Deterministic;
using System;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class MarioBotSystem : SystemMainThreadEntityFilter<MarioBot, MarioPlayerSystem.Filter> {

        //public List<(FP, FP, EntityType)> Targets => ;

        public void MannualSetup(MarioBot* Bot) {
            Bot->Lv = 5;
            Bot->FrameDelay = 5;
            Bot->SightDistance = 13;
            Bot->GroupDistance = FP._1_10;
            Bot->ThrowDelay = FP._0_50;
            //Lv 1 Baby: FrameDelay-50, SightDistence-6, Sprint Isn't Used, Enemies Are Always ignored, Ignores The Fact They Can Walljumps
            //Lv 2 Newbie: FrameDelay-40, SightDistence-8, Sprint Is Only Used For Travel, Enemies Often ignored, Bad At Walljumps
            //Lv 3 Casual: FrameDelay-30, SightDistance-10, Sprint Generally Used, Enemies Occasionally Ignored, Can Use Carryables, Decent At Walljumps
             //Lv 4 Pro: FrameDelay-20, SightDistance-12, Enemies Occasionally Ignored, Can Use Carryables, Good At Walljumps, Abuses Speed Tricks
            //Lv 5 Expert: FrameDelay-10, SightDistance-13, Enemies Never ignored, Uses Star Pridiction To Some Degree, Uses Gp Cancels To Turn Around Sometimes, Uses Walljumps For Quick movement, Tries To Combo, Abuses Speed Tricks
            //Other
            //Lv 6 Master: FrameDelay-3, SightDistence-20, Uses High Level StarPridiction, Uses Gp Cancels To Turn Around, Knows How To Combo, Abuses Speed Tricks, Knows When To Run
            //For Fun!
            //Lv 7 Hacks: FrameDelay-0, SightDistence-99, Uses High Level StarPridiction, Frame Perfect Actions, Knows How To Combo, Abuses Speed Tricks, Knows When To Run
        }

        public override void Update(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
            var Bot = filter.PlayerBot;

            if (Bot->FrameDelay == 0)
                MannualSetup(Bot);

            if (Bot->LastUpdateFrame < f.Number) {
                Bot->LastUpdateFrame = f.Number + Bot->FrameDelay;
                UpdateAi(f, ref filter, stage);
            }

        }

        private void UpdateAi(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
            GetTargets(f, ref filter, stage); //Find The Entities, Then Asign A Priority Value (Priority Changes Based on State And Reserve)
            GetTerrain(f, ref filter, stage); //Find The Solid Terrain
            DecidePath(f, ref filter, stage); //Choose Where To Go (Shell & Propeller Code Here)
            SetInput(f, ref filter, stage); //Set Our Inputs (Fire & Ice Code Here)

        }

        private void GetTargets(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var marioPos = filter.Transform->Position;
            var Bot = filter.PlayerBot;

            f.SimulationConfig.BotTargets.Clear();
            f.SimulationConfig.BotTargets.TrimExcess();

            FPVector2 Spot = new FPVector2(mario->FacingRight ? 999999 : -999999, 999999), posA = Spot, posB = Spot, avoA = Spot, avoB = Spot;
            int Count = 0;

            //Check For Players, Items, Coins, Objectivecoins, And Individual Enemies
            //Stars & Starcoins Ignore SightDistance, Can be Seen From Anywhere Via Minimap
            var stars = f.Filter<BigStar>();
            while (stars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                f.SimulationConfig.BotTargets.Add((Spot.X, Spot.Y, EntityType.Star));
                Count++;
            }
            /*
            var starcoins = f.Filter<StarCoin>();
            while (stars.NextUnsafe(out EntityRef entity, out StarCoin* starcoin)) {
                Targets[Count] = (Spot.X, Spot.Y, EntityType.Star);
                Count++;
            }*/
            //We Can Only Know The Foe's X Pos At All Times.
            var foes = f.Filter<MarioPlayer>();
            while (foes.NextUnsafe(out EntityRef entity, out MarioPlayer* player)) {
                if (player == mario) //Skip ourself
                    continue;
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position, out posA, out posB);
                FP Distance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                f.SimulationConfig.BotTargets.Add((Spot.X, Distance < Bot->SightDistance ? Spot.Y : 9999, EntityType.Player));
                Count++;
            }

            var coins = f.Filter<Coin>();
            while (coins.NextUnsafe(out EntityRef entity, out Coin* coin)) {
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position, out posA, out posB);
                FP Distance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                if (Distance < Bot->SightDistance) {
                    f.SimulationConfig.BotTargets.Add((Spot.X, Spot.Y, EntityType.Coin));
                    Count++;
                }
            }

            //Make Specific Enemies Change The EntityType.
            var enemies = f.Filter<Enemy>();
            while (enemies.NextUnsafe(out EntityRef entity, out Enemy* enemy)) {
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position, out posA, out posB);
                FP Distance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                if (Distance < Bot->SightDistance) {
                    f.SimulationConfig.BotTargets.Add((Spot.X, Spot.Y, EntityType.EnemyStompable));
                    Count++;
                }
            }
        }
        private void GetTerrain(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
        }
        private void DecidePath(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
            FPVector2 marioPos = filter.Transform->Position;
            var Bot = filter.PlayerBot;

            //List<(FP, FP, EntityType)> NewTargets = null;

            for (int i = 0; i < f.SimulationConfig.BotTargets.Count; i++) {
                if (f.SimulationConfig.BotTargets[i].Item3 == EntityType.Star) {
                    QuantumUtils.UnwrapWorldLocations(stage, marioPos, new FPVector2(f.SimulationConfig.BotTargets[i].Item1, f.SimulationConfig.BotTargets[i].Item2), out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos);

                    Bot->inputs.Right = damageDirection.X > 0;
                    Bot->inputs.Left = damageDirection.X < 0;
                    UnityEngine.Debug.Log(damageDirection.X + "" + Bot->inputs.Right.IsDown + Bot->inputs.Left.IsDown);
                    return;
                }
            }

            //f.SimulationConfig.e = NewTargets;
        }
        private void SetInput(Frame f, ref Quantum.MarioPlayerSystem.Filter filter, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var physics = filter.PhysicsObject;
            var Bot = filter.PlayerBot;

            Bot->inputs.Sprint = true;
            if ((physics->WasTouchingGround && !physics->IsTouchingGround) || physics->Velocity.Y > 1 || physics->IsTouchingLeftWall || physics->IsTouchingRightWall) {
                Bot->inputs.Jump = true;
            }
        }

        public enum EntityType : Byte {
            Star, //Includes Starcoin
            ObjectiveCoin,
            Powerup,
            Coin,
            Player,
            Fireball,
            Iceball,
            Hammer,

            Spinner,
            EnterablePipe,
            EnemyStompable, //Goomba, Koopas, Bobomb, Bullet Bill
            EnemyCarryable, //Shells, Lit Bobombs (Not Used For Carrying Objects)
            EnemySturdy, //Spiny
            EnemyGhost, //Boo
            EnemyKooper, //Blue koopa Has A Shell Powerup!
        }
        enum BlockType : Byte {
            Solid,
            Semi, //Includes Cloudplats
            HardBlock, //Includes Solid Entities like pipes and bill blasters
            Brick,
            QuestionCoin, //Includes Invisible Block
            QuestionPowerup,

            SlopeL,
            SlopeR,
            MarioBrosPlat,
            MiniTile,
            CeilingCrush,

        }

        //Used To Add Variety in The Ai
        enum Personality : Byte {
            None, //No Prefrence
            CoinCollecter, //Prefers To Collect Coins And Powerups
            EnemyMauler, //Likes To Kill Enemies More
            EnemyCarrier, //Likes To use koopa Shells & Bobombs often
            Aggressive, //Prefers To Be in Conflict
            Shy, //prefers To Run Away If Possible
            Camper, //Likes Camping High Areas
            Starman, //Prefers To Colect Stars & Starcoins At Any Cost
            MiniChallenge, //Prefers keeping Mini (This is The Rarest Personality)
            Bagger, //Prefers Larger Player Counts And Bags For Mega Then Tries To Win The Game From There
        }


        /*
        private void SetAvoid(ref Filter filter, FPVector2 Pos, int i) {
            var mario = filter.MarioPlayer;
            if (i > mario->AvoidType) {
                mario->AvoidType = (byte) i; //0 = nada, 1 = Grounded Enemy, 2 = Hazard Above, 3 = Weak Player, 4 = Powerful Player
                mario->Avoid = Pos;
            }
        }

        private void HandleAi(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleAi");
            ref var inputs = ref filter.Inputs;
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            var Reserve = f.FindAsset(mario->ReserveItem);

            //Loose Values
            bool Attack = false, Turnaround = false, AvoidThis = false, AbovePit = false, ATargetBellow = false; //, SnappyBack = false;
            bool LackingPowerups = mario->CurrentPowerupState <= PowerupState.Mushroom || mario->CurrentPowerupState <= PowerupState.JumpSuit || (Reserve != null && Reserve.StatePriority < 2);
            FPVector2 marioPos = filter.Transform->Position;
            FP TargetFoeStars = 0;

            //Get The Target Location
            FP distance = 9999;
            FP distanceModifier = 0;
            FPVector2 Spot = new FPVector2(mario->FacingRight ? 999999 : -999999, 999999), posA = Spot, posB = Spot, avoA = Spot, avoB = Spot;
            mario->Target = mario->Avoid = Spot;
            mario->AvoidType = 0;

            //Check For Nearby Stars --- Done ---
            var stars = f.Filter<BigStar>();
            while (stars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                Spot = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                QuantumUtils.UnwrapWorldLocations(stage, marioPos, Spot, out posA, out posB);
                FP tempDistance = FPMath.Sqrt(((posA.X - posB.X) * (posA.X - posB.X)) + ((posA.Y - posB.Y) * (posA.Y - posB.Y)));
                if (tempDistance < distance) {
                    distance = tempDistance;
                    mario->Target = Spot;
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
                    if (mario->Personality == 3) {
                        SetAvoid(ref filter, Spot, 4);
                        Attack = distance < 7;
                    } else if (distance < 9 && (marioFoe->CurrentPowerupState == PowerupState.MegaMushroom || marioFoe->IsStarmanInvincible)) { //Avoid Mega Players At All Costs
                        SetAvoid(ref filter, Spot, 4);
                    } else if (marioFoe->IsPropellerFlying || marioFoe->IsSpinnerFlying) { //Man Darn Propeller Spammers, Avoid Them
                        SetAvoid(ref filter, Spot, 2);
                    } else if (marioFoe->Stars == 0) { //This Player Isn't Worth The Hassle
                        SetAvoid(ref filter, Spot, 3);
                    } else if ((TargetFoeStars < marioFoe->Stars) && mario->GetTeam(f) != marioFoe->GetTeam(f) && ((vel > 0 && posA.X - posB.X > 0) || (vel < 0 && posA.X - posB.X < 0) || mario->Personality == 2 || distanceModifier <= (posA.Y + 2 > posB.Y ? -2 : 0))) { // && tempDistance < distance + (marioFoe->Stars - mario->Stars)) {
                        ATargetBellow = posA.Y - posB.Y > 1 && FPMath.Abs(posA.X - posB.X) < Constants._0_40;
                        if (mario->CurrentPowerupState > PowerupState.Mushroom) {
                            TargetFoeStars = marioFoe->Stars;
                            distance = tempDistance;
                            mario->Target = Spot;
                            Attack = true;
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
                        mario->Target = Spot;
                        Attack = false;
                    }
                }

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

            }
            //Check For Tiles
            //TODO: Checking For Tiles
            //TODO: Allow Powerup & Coin Block Tiles To Gain Some Priority If Lacking Powerups
            //TODO: Check For Tiles In Front Of The Player To Jump Early
            //TODO: If Target Bellow Tiles But The Tiles Bellow Aren't Bricks Then Don't gp, Tell it To Walk Around

            if (physicsObject->IsTouchingCeiling)
                mario->BotWallJumping = 0;
            else
                QuantumUtils.Decrement(ref mario->BotWallJumping);

            //Pit Check
            AbovePit = mario->BotWallJumping > 0 || !PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, marioPos, FPVector2.Down, 18, out var hit);

            //Determine Target Difference
            QuantumUtils.UnwrapWorldLocations(stage, marioPos, mario->Target, out posA, out posB);
            FPVector2 Diffrence = new FPVector2(posA.X - posB.X, posA.Y - posB.Y);
            QuantumUtils.UnwrapWorldLocations(stage, marioPos, mario->Avoid, out avoA, out avoB);
            FPVector2 AvoidDif = new FPVector2(avoA.X - avoB.X, avoA.Y - avoB.Y);
            QuantumUtils.UnwrapWorldLocations(stage, posB, avoB, out FPVector2 tempA, out FPVector2 tempB);
            FPVector2 TarvoidDif = new FPVector2(tempA.X - tempB.X, tempA.Y - tempB.Y);

            //LeftRightInputs
            if (mario->BotWallJumping > 0 || AbovePit || (mario->AvoidType == 2 && FPMath.Abs(avoA.X - avoB.X) < 1)) { //Keep Last Inputs
                inputs.Left = (bool) !mario->PressingRight;
                inputs.Right = (bool) mario->PressingRight;
            } else if (mario->AvoidType == 4 || (mario->AvoidType == 3 && FPMath.Abs(TarvoidDif.X) < 2)) { //Avoid Danger
                inputs.Left = avoB.X > avoA.X;
                inputs.Right = !inputs.Left;
                mario->PressingRight = inputs.Right == true;
            } else if (FPMath.Abs(physicsObject->Velocity.X) <= 7) { //Input Nothing For Slope Speed
                inputs.Left = posB.X < posA.X;
                inputs.Right = !inputs.Left;
                mario->PressingRight = inputs.Right == true;
            }

            //Gp To Turn Around Quickly If Target is In Opposite Direction Far Away Enough
            Turnaround = (physicsObject->Velocity.X > 3 && Diffrence.X > 1) || (physicsObject->Velocity.X < -3 && Diffrence.X < -1);

            //Handle Jump
            if (physicsObject->IsTouchingGround || physicsObject->Velocity.Y < 0)
                inputs.Jump = false;
            if ((!physicsObject->IsTouchingGround && physicsObject->WasTouchingGround && Diffrence.Y < 0 Constants._0_40)
                || (mario->IsWallsliding && Diffrence.Y < Constants._0_40) || (!physicsObject->WasTouchingGround && physicsObject->Velocity.Y > -1) || physicsObject->IsUnderwater
                || (physicsObject->IsTouchingGround && ((FPMath.Abs(Diffrence.X) > Diffrence.Y && !(mario->AvoidType == 2 && FPMath.Abs(TarvoidDif.X) < 1)) || (mario->FacingRight && physicsObject->IsTouchingRightWall) || (!mario->FacingRight && physicsObject->IsTouchingLeftWall)))
                ) {
                inputs.Jump = true;
                if (mario->IsWallsliding) {
                    mario->BotWallJumping = (byte) 30;
                    mario->PressingRight = !mario->PressingRight;
                }
            }

            //Up Input
            if ((mario->IsGroundpounding && Diffrence.Y < Constants._0_20)
             || (mario->CurrentPowerupState == PowerupState.HammerSuit && Diffrence.Y < -1)
             || (Turnaround && mario->IsGroundpounding && physicsObject->Velocity.Y < -Constants._0_40))
                inputs.Up = true;

            //Gp If Bellow
            if (!AbovePit && (((Diffrence.Y > 1 && (FPMath.Abs(Diffrence.X) < Constants._0_40) || ATargetBellow)
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
            QuantumUtils.Decrement(ref mario->BotAtkCooldown);
            if (Attack && mario->BotAtkCooldown <= 0
              && ((mario->CurrentPowerupState == PowerupState.FireFlower && Diffrence.Y > -2)
              || (mario->CurrentPowerupState == PowerupState.IceFlower && Diffrence.Y > -2)
              || (mario->CurrentPowerupState == PowerupState.HammerSuit && Diffrence.Y < 2)
              || (mario->CurrentPowerupState == PowerupState.PropellerMushroom && Diffrence.Y < 0)
              || (mario->CurrentPowerupState == PowerupState.CatSuit && Diffrence.Y > -2))) {
                inputs.PowerupAction = true;
                mario->BotAtkCooldown = (byte) f.RNG->Next(3, 45);
            }

            //Always Sprint
            inputs.Sprint = true;

            //Take out Reserve
            //TODO: Get Rid Of Bad Powerups (Ex: Mini & Jumpsuit)
            if (mario->CurrentPowerupState <= PowerupState.Mushroom || mario->CurrentPowerupState == PowerupState.JumpSuit
              || (mario->Personality == 2 && (mario->CurrentPowerupState == PowerupState.BlueShell || mario->CurrentPowerupState == PowerupState.PropellerMushroom))
              || (mario->Personality == 3 && (mario->CurrentPowerupState == PowerupState.FireFlower || mario->CurrentPowerupState == PowerupState.IceFlower)))
                if (Reserve != null)
                    SpawnReserveItem(f, ref filter);
        }*/
    }
}
