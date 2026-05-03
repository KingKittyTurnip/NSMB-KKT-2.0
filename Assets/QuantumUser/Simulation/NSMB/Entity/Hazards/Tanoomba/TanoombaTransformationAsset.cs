using Microsoft.SqlServer.Server;
using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Quantum {
    public partial class TanoombaTransformationAsset : AssetObject {


        public TanoombaFormData[] ListOfTransforms;

        [Serializable]
        public class TanoombaFormData {
            [Header("---GenericInfo---")]
            public string Name; //name
            public TanoombaFormSpawnType SpawnType; //how do we spawn
            public AssetRef<EntityPrototype> comparePrototype; //if none we skip the check, otherwise we check if this matches a object in the hazardlist, poweruplist, or currentstage
            public FP ChanceWeight = 1;
            public TanoombaFormMoveData MoveData;
            public TanoombaFormModelData ModelData;

            [Serializable]
            public class TanoombaFormMoveData {
                public TanoombaFormMovementType MovementType;
                //gravity
                public FPVector2 Gravity;
                public FP TerminalVelocity = -8;
                //x speed
                public bool RequiresGroundToAffectVelocity = true;
                public FP MaxSpeed = 0;
                public FP Acceleration = 10; //10 basically means moves instantly
                public FP Deceleration = 10; //10 basically means go back to normal speed instantly
                //y speed
                public bool BounceIsThrust = true;
                public FP Bounciness = 0; //0-1
                //other
                public bool FollowPlayer = false;
                public bool HopsUpBlocks = false;
                public bool MoveThroughTerrain = false;
            }

            [Serializable]
            public class TanoombaFormModelData {
                public TanoombaFormFlipType FlipType;
                public bool UsesLeaf;
                public Vector3 LeafLocation;
                public bool UsesTail;
                public Vector3 TailLocation;
                [Space]
                //the id of a list in the tanoomba animator that controls the visibility of said model, an id of -1 will use sprites instead
                public int ModelId = -1;
                [Space]
                //we cycle through the sprites one at a time
                public Vector3 Offset;
                public float FPS;
                public Sprite[] sprites;
                public bool UsesWavey = false;
                [Space]
                public TanoombaFormExtraSoundType soundType;
            }
        }

        public enum TanoombaFormMovementType : byte {
            Static, //Doesn't Move At all
            Basic, //affected by physics
            PropellerMushroom, //uses the propeller mushroom path
            BubbleFlowerBubble, //uses the bubble flower path
            LemmyBall, //unique physics
        }
        public enum TanoombaFormSpawnType : byte {
            AwayFromPlayers, //Spawns Away From Players

            //specificspawnRules
            SpawnsAtStarSpawn, //spawns at a star spawn
            SpawnsAtHazardSpawn, //spawns at a hazardspawn
            SpawnsAtCoinAndReplace, //replaces a coin in the stage, if there are no coins we skip this option
            SpawnsAtTileAndReplace, //replaces a tile

            //only if coins are enabled
            AwayAndCoinsEnabled, //replaces a coin in the stage, if there are no coins we skip this option
            AwayAndBillBlasters, //Can also spawns if a BillBlaser exists
            AwayAtAPipe, //Appears at a pipe, for PiranhaPlant transformation
        }
        public enum TanoombaFormExtraSoundType : byte {
            None,
            WallBump,
            CoinLand,
            GroundpoundSound,
            MetalPipeSound,
        }
        public enum TanoombaFormFlipType : byte {
            AlwaysRight,
            AlwaysLeft,
            FromFacing,
            FromFacingReversed,
        }
    }
}