using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using static Quantum.CommandChangePlayerData;
using static UnityEditor.Progress;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject, IOrderedAsset {

        int IOrderedAsset.Order => Order;

        public string NamePrefix, TranslationKey, DescriptionTranslationKey, DiscordRpcKey;
        public string ObjectiveSymbolPrefix;
        public AssetRef<CoinItemAsset>[] AllCoinItems;
        public AssetRef<CoinItemAsset> FallbackCoinItem;
        public AssetRef<EntityPrototype> LooseCoinPrototype;
        public int Order;
        /*
        public GameRulesPrototype DefaultRules;
        */

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public abstract int GetObjectiveCount(Frame f, PlayerRef player);

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public virtual bool IsFastMusicEnabled(Frame f) {
            ref var rules = ref f.Global->Rules;

            if (rules.ModifierTimerEnabled && f.Global->Timer <= 60) {
                // Timer expiring, panic music
                return true;
            }

            if (rules.ModifierLivesEnabled) {
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
        public PowerupData NEWGetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var items = f.ResolveList(f.Global->Rules.Items);
            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;

            bool CanSpawnJoke = true;
            FP MushroomChance = 1;

            bool StageSpawnsBigItems;
            bool StageSpawnsVerticalItems;

            //pick random chance type
            ItemChanceType chancePick = ItemChanceType.Middling;
            FP totalChance = 0;
            FP highestChance = -999;
            ItemChanceType highestChanceGroup = ItemChanceType.First;
            byte MaxTypes = ((int) ItemChanceType.Invalid);

            //get chances that exist
            List<bool> chanceExists = new List<bool>();
            for (int i = 0; i < MaxTypes; i++) {
                var l = f.ResolveList(items[i].Items);
                //UnityEngine.Debug.Log("lisssssst " + l.Count);
                chanceExists.Add(l.Count > 64);//????
            }

            //pick random chance type
            for (int i = 0; i < MaxTypes; i++) {
                if (chanceExists[i])
                    continue;

                var e = NEWGetSpawnWeight(f, (ItemChanceType) i, ourObjectiveCount);
                totalChance += FPMath.Max(0, e);
                if (e > highestChance) {
                    highestChance = e;
                    highestChanceGroup = (ItemChanceType) i;
                }
                //UnityEngine.Debug.Log((ItemChanceType) i + " " + totalChance);
            }
            //UnityEngine.Debug.Log(totalChance);
            if (totalChance <= 0) {
                //UnityEngine.Debug.Log("powerup pick is at it's LAST RESORT: " + highestChanceGroup);
                //the total of all the chances makes 0, pick the one that is the highest
                chancePick = highestChanceGroup;
            } else {
                FP rand = mario->RNG.Next(0, totalChance);
                for (int ik = 0; ik < MaxTypes; ik++) {
                    if (chanceExists[ik])
                        continue;
                    FP chance = FPMath.Max(0, NEWGetSpawnWeight(f, (ItemChanceType) ik, ourObjectiveCount));

                    if (rand < chance) {
                        chancePick = (ItemChanceType) ik;
                        //UnityEngine.Debug.Log("Powerup pick, type: " + chancePick);
                        break;
                    }

                    rand -= chance;
                }
            }

            //pick a random object with this chance type
            var listOfpowerups = f.ResolveList(items[(int) chancePick].Items);
            PowerupData pick = listOfpowerups[f.RNG->Next(0, listOfpowerups.Count)];

            //UnityEngine.Debug.Log("item: " + pick.Name);

            return pick;
        }
        public FP NEWGetSpawnWeight(Frame f, ItemChanceType j, int ourStars) {

            (FP, FP, FP) SpawmAboveBellowChance = j switch { //A == base chance, B == first bonus, C == last Bonus
                //this set of chances means last place is near guerenteed to get a power item, first place will often get first stuff, sometimes middle stuff
                ItemChanceType.First => new(-FP._0_50, 2, -4), //mini & mushrooms
                ItemChanceType.Middling => new(2, -1, -1),//2nd stage powerups
                ItemChanceType.LastCommon => new(-FP._0_20, 0, 3), //weaker catchup, not guerenteed
                ItemChanceType.LastRare => new(-3, -1, 5), //strong catchup, guerenteed if yur very behind
                ItemChanceType.JokeFirst => new(-FP._0_25, FP._1_50, -4), //doneflower & jumpsuit
                ItemChanceType.JokeMiddle => new(1, -FP._0_50, -FP._0_50), //cake & turnipbasket
                _ => new(0, 0, 0),
                /*
                ItemChanceType.FirstCommon => new(0, 1, -4),
                ItemChanceType.FirstRare => new(FP._0_50, FP._0_50, -1),
                ItemChanceType.Middling => new(1, -FP._0_25, 1),
                ItemChanceType.LastCommon => new(-FP._0_25, 0, Constants._2_50),
                ItemChanceType.LastRare => new(-2, 0, Constants._4_50),

                ItemChanceType.Mushroom => new(FP._1_50, FP._0_50, -2), //new(FP._1_50, 1, -1), old chance, maybe we want this when we add the tpf mushroom mechanic
                ItemChanceType.Vertical => new(2, -FP._0_75, FP._0_50),
                ItemChanceType.Large => new(-2, 0, Constants._4_50),
                ItemChanceType.JokeFirst => new(FP._0_50, 2, -Constants._2_50),
                ItemChanceType.JokeMiddle => new(1, -FP._0_25, 1),
                _ => new(0, 0, 0),*/
            };

            int starsToWin = f.Global->Rules.StarsToWin;

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
            return 0;
        }
        #endregion
    }
}