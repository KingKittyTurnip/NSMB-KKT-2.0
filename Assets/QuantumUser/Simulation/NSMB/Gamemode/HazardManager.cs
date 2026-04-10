using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Quantum {
    public partial class RulesBaser : AssetObject {

        public DefaultRules Rules;

        [Serializable]
        public class DefaultRules {
            //You Can Customize Powerups From Coins, Items From Roulettes, And Hazards That Spawn

            public RuleDefault BaseRulesList;
            //This Is All Of The Base Values Of Each Rule
            [Serializable]
            public class RuleDefault {
                //Game Will Not Start If:
                //No Gamemode Is Active
                //All Players Are On The Same Team
                //There Is No Way To Win (ex. stars spawnable count set to 0 and Hazards can't spawn a star)

                public GameRulesPrototype DefaultRules;

                //unimplemented but i want to add
                public bool SpawnType;
                public byte StarsSpawnableCount = 1;//0-30, restricted by map spawns
                 public byte StarcoinsSpawnableCount = 1;//0-30, restricted by map spawns
                  public bool AutomaticallyModifyTimerDependingOnLivesAndPlayerCount = true; //Depending On The Player Count and lives it will modify the timer { TIMER = 30 * (PlayerCount * lives) }
                  public byte StartingBobombCount = 1; //0-10, players on the bobomb team from the lobby will count torwards this
                  public bool FirstBombIsKingBobomb = true;
                   public bool IsRoulette = false; //when enabled ? blocks pull from a list
                    public byte HazardCount = 10; //0-30
                    public byte HazardFreq = 10; //0-60
                    public HefPercent HeftyPercentage = HefPercent.twelve; //if the hazards that exist exceed the percent they cannot spawn, the percent is used to spawn the hefties too
                    public byte GlobalTimeTilDespawn = 80; //Multiply value by 60 when applying, base value of 80 (1:20 minutes)
                     public bool TeamLock = false; //Show Prompt To Players Enabling It For The First Time Telling Them To Use It Responsibly 
                     public bool FreindlyFire = false;

                public HazardDefault bigstarBase;
                public HazardDefault purplecoinBase;
                public HazardDefault starcoinBase;
                public HazardDefault kingbobombBase;
                public HazardDefault loosecoinBase;
                public HazardDefault oneupBase;
                public HazardDefault clockBase;
            }

            public HazardDefault[] ListOfAvalibleObjects;

            [Serializable]
            public class HazardDefault {
                [Header("---Generic Info---")]
                public string Name;
                public AssetRef<EntityPrototype> entityPrototype;
                public Sprite Icon;
                public ObjectPrimaryType type;
                public CategoreyObject categorey;
                [Header("---Subdata---")]
                public bool Hefty; //Set As Hefty
                public bool Teamable; //set to 255 to "disable" for the object
                public bool SpawnRandom; //decides if it should spawn randomly
                public bool SpawnFridge; //decides if it should spawn from a fridge
                public bool SpawnBulb; //decides if it should spawn from the ideabulb powerup
                public FP SpawnChance = FP._1;
                public FP WinningBonus = 0;
                public FP LosingBonus = FP._0_50;
                [Header("---SpecialData---")]
                public HValue[] SpecialValues;
            }
        }

        public static List<int> GetAllObjectsOfCategory() {
            return new List<int>();
        }
    }

    #region Enums
    [Serializable]
    public class HValue {
        public string ButtonName = "";
        public ValueType ButtonType = ValueType.toggle;
        public byte BaseValue = 0;
        public IntVector2 ValueRange = new IntVector2(0, 1);
    }
    public enum ValueType {
        toggle, // 0, 1
        counter, //0-255, Buttoninfo is the restriction
        //Type, // 0-255, ButtonInfo Determins
        Refrence, //0-255, Buttoninfo is for the refrence (Used For Refrencing other hazards, value of 255 would do no changes to an object)
    }
    [Flags]
    public enum ObjectPrimaryType : byte {
        Powerup = 1 << 0, //fireflower, blueshell
        Hazard = 1 << 1, //Heavystone, Petey, Spinpipe
        Object = 1 << 2, //Goomba, Koopa
        Bonus = 1 << 3, //Mode Specific Additions
    }
    [Flags]
    public enum ObjectSpawnType : byte {
        SpawnGoal = 1 << 0, //spawns as goal
        SpawnRandom = 1 << 1, //spawns randomly in stage
        SpawnFridge = 1 << 2, //spawns from fridge
        SpawnBulb = 1 << 3, //spawns from ideabulb
    }
    [Flags]
    public enum CategoreyObject {
        //General Powerup
        VannillaPowerup = 1 << 0, //fireflower, blueshell
        KKTPowerup = 1 << 1, //Mushroom, Mini
        Joke = 1 << 2, //Jumpsuit, Doneflower
        //General Hazard
        Weapon = 1 << 10, //heavystone, redpow
        Boss = 1 << 11, //bosses
        WorldModifier = 1 << 12, //spinpipe, fan
        Minigame = 1 << 13, //starball
        //General Other
        Enemies = 1 << 20, //goomba, koopa
        //General Bonus
        Starchasers = 1 << 21, //bigstar
        CoinRunners = 1 << 22, //starcoin, purplecoin box
        BalloonBattle = 1 << 23, //
        BombChasers = 1 << 24, //
        CoinsForPowerup = 1 << 25, //loosecoin
        Lives = 1 << 26, //1up
        Teams = 1 << 27, //teamsoption for every object
        Timer = 1 << 28, //clock
        //Other
        Other = 1 << 99,
    }
    public enum HefPercent : int {
        zero = 0,
        five = 5,
        twelve = 12,
        twentyfive = 25,
        fifty = 50,
        sevendyfive = 75,
        onehundred = 100,
        onefifty = 150,
        twohundred = 200,
        threehundred = 300,
        fivehundred = 500,
        thousand = 1000,
        heftyOnly = -1, //only hefty hazards spawn
    }
    #endregion
}