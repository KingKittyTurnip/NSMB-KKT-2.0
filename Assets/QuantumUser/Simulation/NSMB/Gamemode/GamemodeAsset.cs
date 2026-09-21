using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject, IOrderedAsset {

        int IOrderedAsset.Order => Order;

        public string NamePrefix, TranslationKey, DescriptionTranslationKey, DiscordRpcKey;
        public string ObjectiveSymbolPrefix;
        public AssetRef<CoinItemAsset>[] AllCoinItems;
        public AssetRef<CoinItemAsset> FallbackCoinItem;
        public AssetRef<EntityPrototype> LooseCoinPrototype;
        public int Order;
        
        public GameRulesPrototype DefaultRules;
        

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void OnReturnToRoom(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public virtual int GetObjectiveCount(Frame f, PlayerRef player) {
            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (player != mario->PlayerRef) {
                    continue;
                }

                return GetObjectiveCount(f, mario);
            }

            return -1;
        }

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public virtual bool IsFastMusicEnabled(Frame f) {
            ref var rules = ref f.Global->Rules;

            if (rules.IsTimerEnabled && f.Global->Timer <= 60) {
                // Timer expiring, panic music
                return true;
            }

            if (rules.IsLivesEnabled) {
                // Low on lives. Two cases:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life

                int playersWithOneLife = 0;
                foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                    if (mario->IsValid(f)) {
                        if (mario->Lives == 1) {
                            playersWithOneLife++;
                        }
                    }
                }

                if ((f.Global->RealPlayers <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= f.Global->RealPlayers)) {
                    return true;
                }
            }

            return false;
        }

        public bool CanItemSpawn(Frame f, CoinItemAsset coinItem, bool fromRouletteBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.BannedCoinItems.Contains(coinItem)) {
                return false;
            }

            if (f.Global->Rules.IsCoinItemDisabled(f, coinItem)) {
                return false;
            }

            return coinItem.CanSpawn(f, fromRouletteBlock);
        }

        public virtual CoinItemAsset GetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // "Losing" variable based on ln(x+1)

            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;

            FP totalChance = 0;
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                totalChance += GetItemSpawnWeight(f, coinItem, ourObjectiveCount);
            }

            FP rand = mario->RNG.Next(0, totalChance);
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                FP chance = GetItemSpawnWeight(f, coinItem, ourObjectiveCount);

                if (rand < chance) {
                    return coinItem;
                }

                rand -= chance;
            }

            return f.FindAsset(FallbackCoinItem);
        }

        public abstract FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int ourObjectiveCount);

        public virtual int? GetWinningTeam(Frame f, out int winningObjectiveCount) {
            winningObjectiveCount = 0;
            int? winningTeam = null;
            bool tie = false;
            
            Span<int> teamObjectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);

            for (int i = 0; i < Constants.MaxPlayers; i++) {
                int objectiveCount = teamObjectiveCounts[i];
                if (objectiveCount < 0) {
                    continue;
                } else if (winningTeam == null) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount > winningObjectiveCount) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount == winningObjectiveCount) {
                    tie = true;
                }
            }

            return tie ? null : winningTeam;
        }

        public virtual void GetAllTeamsObjectiveCounts(Frame f, Span<int> teamObjectiveCounts) {
            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                teamObjectiveCounts[i] = -1;
            }

            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (!mario->IsValid(f) || mario->GetTeam(f) is not byte team) {
                    continue;
                }

                if (teamObjectiveCounts[team] == -1) {
                    teamObjectiveCounts[team] = 0;
                }

                if (team < teamObjectiveCounts.Length) {
                    teamObjectiveCounts[team] += GetObjectiveCount(f, mario);
                }
            }
        }

        public virtual int? GetTeamObjectiveCount(Frame f, byte? nullableTeam) {
            if (nullableTeam is not byte team) {
                return null;
            }
            return GetTeamObjectiveCount(f, team);
        }

        public readonly struct ObjectiveStatistics {
            public readonly FP Average;
            public readonly int Min, Max;
        }

        public virtual int GetTeamObjectiveCount(Frame f, byte team) {
            int sum = 0;
            foreach ((_, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (mario->GetTeam(f) != team || !mario->IsValid(f)) {
                    continue;
                }

                sum += GetObjectiveCount(f, mario);
            }

            return sum;
        }

        public virtual int GetFirstPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int max = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > max) {
                    max = objectiveCount;
                }
            }

            return max;
        }

        public virtual int GetLastPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int min = int.MaxValue;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount < min && objectiveCount != -1) {
                    min = objectiveCount;
                }
            }

            return min;
        }

        public virtual FP GetAverageObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < teamObjectives.Length; i++) {
                if (teamObjectives[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            int sum = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > 0) sum += objectiveCount;
            }
            return (FP) sum / aliveTeamCount;
        }

        public virtual EntityRef SpawnLooseCoin(Frame f, FPVector2 position) {
            EntityRef newCoinEntity = f.Create(LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(newCoinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoinEntity);
            coinTransform->Position = position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            return newCoinEntity;
        }


        #region KKT Mod
        public void NEWGetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock, out AssetRef<EntityPrototype> entityPrototype, out byte extraA, out byte extraB, out byte extraC, out byte extraD) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var items = f.ResolveList(f.Global->Rules.Items);
            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;

            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;

            if (f.Global->Rules.EveryItemHasTheSameChance) {
                //ignore all code and just pick anything in the list
                int item = f.RNG->Next(0, items.Count);
                entityPrototype = stuff[items[item].PrototypeRef].entityPrototype;
                extraA = items[item].ExtraSlotA;
                extraB = items[item].ExtraSlotB;
                extraC = items[item].ExtraSlotC;
                extraD = items[item].ExtraSlotD;
                return;
            }

            var marioreserve = f.FindAsset(mario->ReserveItem);
            bool MarioHasJoke = (mario->CurrentPowerupState == PowerupState.Jumpsuit || mario->CurrentPowerupState == PowerupState.Doneflower || (marioreserve != null && (marioreserve.State == PowerupState.Jumpsuit || marioreserve.State == PowerupState.Doneflower)));
            bool CanSpawnJoke = (mario->TimesWithoutAJoke > FPMath.Max((8-f.Global->Rules.CoinsForPowerup)-1, 0) && !MarioHasJoke);
            bool CanSpawnCatchups = !(mario->CurrentPowerupState <= PowerupState.Mushroom || MarioHasJoke);
            bool WontSpawnFirst = mario->CurrentPowerupState == PowerupState.NoPowerup && marioreserve != null && marioreserve.State == PowerupState.NoPowerup;

            Dictionary<ItemChanceType, FP> sortchances = new Dictionary<ItemChanceType, FP>();
            byte MaxTypes = ((int) ItemChanceType.Invalid);

            //sort random chance types
            for (int i = 0; i < MaxTypes; i++) {
                // only spawn jokes if we haven't had one in a while, so players don't constantly get them
                //only spawn catchups if mario has a proper powerup, makes it so yur aren't weak
                //don't spawn first if we have absolutly nothing, makes it feel more fair
                if ((i == (int) ItemChanceType.Joke && !CanSpawnJoke) || 
                    ((i == (int) ItemChanceType.LastRare || i == (int) ItemChanceType.LastCommon) && !CanSpawnCatchups) ||
                    (i == (int) ItemChanceType.First && WontSpawnFirst)) {
                    //sortchances.Add((ItemChanceType) i, FP.MinValue);
                    //do not add
                    UnityEngine.Debug.Log((ItemChanceType) i);
                    continue;
                }

                var e = NEWGetSpawnWeight(f, (ItemChanceType) i, ourObjectiveCount);
                sortchances.Add((ItemChanceType)i, e);
            }

            //pick chance group
            ItemChanceType chancePick = ItemChanceType.First;
            List<(int, byte, byte, byte, byte)> possibleItems = new List<(int, byte, byte, byte, byte)>();
            TryPickChance:
            FP totalChance = 0;

            if (sortchances.Count == 0) {
                UnityEngine.Debug.LogError("where did all the item go~");
                //ok we checked everything just spawn a mushroom sob
                entityPrototype = stuff[0].entityPrototype;
                extraA = 0;
                extraB = 0;
                extraC = 0;
                extraD = 0;
                return;
            } else {
                //randomly pick which chance we want to calculate
                foreach (var i in sortchances) {
                    totalChance += FPMath.Max(0, i.Value);
                }
                if (totalChance <= 0) {
                    //no chance for any item...? pick highest one then
                    FP highestChance = FP.MinValue;
                    foreach (var i in sortchances) {
                        if (i.Value > highestChance) {
                            highestChance = i.Value;
                            chancePick = i.Key;
                        }
                    }
                } else {
                    //pick randomly for any chances above 0
                    FP rand = mario->RNG.Next(0, totalChance);
                    foreach (var i in sortchances) {
                        FP chance = FPMath.Max(0, i.Value);
                        if (rand < chance) {
                            chancePick = i.Key;
                            break;
                        }
                    }
                }
            }
            sortchances.Remove(chancePick); //remove it from the list, in case there is nothing here

            //get items of the chance we picked
            foreach (var i in items) {
                if (stuff[i.PrototypeRef].SpawnChance == chancePick)
                    possibleItems.Add((i.PrototypeRef, i.ExtraSlotA, i.ExtraSlotB, i.ExtraSlotC, i.ExtraSlotC));
            }

            //um, there was no items?
            if (possibleItems.Count == 0) {
                goto TryPickChance;
            }

            //was this a joke?
            if (chancePick == ItemChanceType.Joke || MarioHasJoke) {
                mario->TimesWithoutAJoke = 0;
            } else {
                mario->TimesWithoutAJoke++;
            }

            //pick a random object
            int id = f.RNG->Next(0, possibleItems.Count);
            entityPrototype = stuff[possibleItems[id].Item1].entityPrototype;
            extraA = possibleItems[id].Item2;
            extraB = possibleItems[id].Item3;
            extraC = possibleItems[id].Item4;
            extraD = possibleItems[id].Item5;
        }
        public FP NEWGetSpawnWeight(Frame f, ItemChanceType j, int ourStars) {

            (FP, FP, FP) SpawmAboveBellowChance = j switch { //A == base chance, B == first bonus, C == last Bonus
                //this set of chances means last place is near guerenteed to get a power item, first place will often get first stuff, sometimes middle stuff
                ItemChanceType.First => new(-FP._0_50, 2, -4), //mini & mushrooms
                ItemChanceType.Middling => new(2, -1, -1),//2nd stage powerups
                ItemChanceType.LastCommon => new(-FP._0_20, 0, 3), //weaker catchup, not guerenteed
                ItemChanceType.LastRare => new(-3, -1, 5), //strong catchup, guerenteed if yur very behind
                ItemChanceType.Joke => new(FP._0_50, FP._0_50, -1), //jokes, first place should be able to get them
                _ => new(0, 0, 0),
            };

            int starsToWin = f.Global->Rules.IsStarsEnabled ? f.Global->Rules.StarsToWin : 1;

            FP starsAvg = GetAverageObjectiveCount(f);
            int starsFirstPlace = GetFirstPlaceObjectiveCount(f);
            int starsLastPlace = GetLastPlaceObjectiveCount(f);

            FP avgDiff = ourStars - starsAvg;
            int diffLeader = starsFirstPlace - ourStars;

            int starBand = starsFirstPlace - starsLastPlace;

            FP normLeader = (FP) starsFirstPlace / starsToWin;
            FP normStarAvg = starsAvg / starsToWin;

            // item ranking formulas which is used for determining which items spawn
            FP itemRank = avgDiff - diffLeader / 5 * starBand / starsToWin * (normLeader * starsToWin / 4);

            // being above the average means you get different formula
            FP bonus;
            if (itemRank > 0) {
                FP magni = (starBand + normStarAvg * starsToWin) / starsToWin;
                bonus = SpawmAboveBellowChance.Item2 * FPMath.Log(FPMath.Abs(itemRank) + 1, FP.E) * magni;
            } else {
                FP magni = (starsAvg + starsFirstPlace * FP._0_50) / starsToWin;
                bonus = SpawmAboveBellowChance.Item3 * FPMath.Log(FPMath.Abs(itemRank) + 1, FP.E) * magni;
            }
            return SpawmAboveBellowChance.Item1 + bonus;
        }
        #endregion
    }
}