namespace Quantum {
    public unsafe class EnterablePipeSystem : SystemSignalsOnly {

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<EnterablePipe, MarioPlayer>(f, OnPipeMarioInteraction);
        }

        public static void OnPipeMarioInteraction(Frame f, EntityRef pipeEntity, EntityRef marioEntity) {
            var pipe = f.Unsafe.GetPointer<EnterablePipe>(pipeEntity);
            if (!pipe->IsEnterable) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var currentPowerup = f.FindAsset(mario->CurrentPowerupAsset);
            if (pipe->IsMiniOnly && currentPowerup.Form != PowerupAsset.PlayerForm.Mini) {
                return;
            }

            if (mario->IsCrouchedInShell(currentPowerup) || mario->IsInKnockback || mario->IsStuckInBlock
                || currentPowerup.Form == PowerupAsset.PlayerForm.Mega || mario->MegaMushroomEndFrames > 0) {
                return;
            }

            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            Input input = default;
            if (mario->PlayerRef.IsValid) {
                input = *f.GetPlayerInput(mario->PlayerRef);
            }

            if (pipe->IsCeilingPipe) {
                if (!marioPhysicsObject->IsTouchingCeiling || !input.Up.IsDown) {
                    return;
                }
            } else {
                if (!marioPhysicsObject->IsTouchingGround || !input.Down.IsDown) {
                    return;
                }
            }

            mario->EnterPipe(f, marioEntity, pipeEntity);
        }
    }
}