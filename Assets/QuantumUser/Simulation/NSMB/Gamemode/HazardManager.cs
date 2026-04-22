using Microsoft.SqlServer.Server;
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

            public RuleDefault[] BaseRulesList;
            //This Is All Of The Base Values Of Each Rule
            [Serializable]
            public class RuleDefault {
                //Game Will Not Start If:
                //No Gamemode Is Active
                //All Players Are On The Same Team
                //There Is No Way To Win (ex. stars spawnable count set to 0 and Hazards can't spawn a star)

                public string Name = "Ruleset";
                public string Description = "";
                public GameRulesPrototype DefaultRules;
            }

            public HazardDefault[] ListOfAvalibleObjects;
            [Header("GamemodeSpecific Objects")]
            public HazardDefault bigstarBase;
            public HazardDefault purplecoinBase;
            public HazardDefault starcoinBase;
            public HazardDefault kingbobombBase;
            public HazardDefault loosecoinBase;
            public HazardDefault oneupBase;
            public HazardDefault clockBase;

            [Serializable]
            public class HazardDefault {
                [Header("---Generic Info---")]
                public string Name;
                public AssetRef<EntityPrototype> entityPrototype;
                public Sprite Icon;
                public ObjectPrimaryType type;
                public CategoreyObject categorey;
                [Header("---Subdata---")]
                public bool Teamable; //set to 255 to "disable" for the object
                public bool CanSpawnAsHazard = true; //decides if it works with randomly spawning
                public bool Hefty; //spawns as a hefty
                public ItemChanceType SpawnChance; //chance as a item 
                public BulbSpawnType BulbSpawnType = BulbSpawnType.basic; //if bulb uses this ability again it will remove the old object
                public BulbCooldown BulbCooldown = BulbCooldown.normal; //the cooldown for the bulb powerup
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
        public string[] valuenames;
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

    public enum Frequency : byte {
        instant = 0,
        faster = 2,
        fast = 6,
        normal = 10,
        lung = 20,
        longer = 30,
        minute = 60,
    }

    public enum DespawnTime : int {
        fastest = 5,
        faster = 10,
        fast = 40,
        normal = 80,
        lung = 120,
        longer = 200,
        never = -1,
    }
    public enum BulbCooldown : byte {
        basicallynothing, //1 second
        fast, //2 seconds
        normal, //4
        slow, //6
        entirenormalstarspawn, //10
    }
    public enum BulbSpawnType : byte {
        basic,
        replaceOld,
        onlyOnce, //10, hardcoded to only spawn once (until it's gone)
        Nope, //not a option
    }
    #endregion
}