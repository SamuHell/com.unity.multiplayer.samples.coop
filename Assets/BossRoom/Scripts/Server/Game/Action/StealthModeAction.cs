using MLAPI;
using MLAPI.Spawning;
using UnityEngine;

namespace BossRoom.Server
{
    /// <summary>
    /// Causes the character to become hidden to enemies and other players. Notes:
    /// - Stealth starts after the ExecTimeSeconds has elapsed. If they are attacked during the Exec time, stealth is aborted.
    /// - Stealth ends when the player attacks or is damaged.
    /// </summary>
    public class StealthModeAction : Action
    {
        private bool m_IsStealthStarted = false;
        private bool m_IsStealthEnded = false;

        public StealthModeAction(ServerCharacter parent, ref ActionRequestData data) : base(parent, ref data) { }

        public override bool Start()
        {
            m_Parent.NetState.RecvDoActionClientRPC(Data);

            // not allowed to walk while going stealthy!
            var movement = m_Parent.GetComponent<ServerCharacterMovement>();
            if (!movement.IsPerformingForcedMovement())
            {
                movement.CancelMove();
            }    
            return true;
        }

        public override bool ShouldBecomeNonBlocking()
        {
            return TimeRunning >= Description.ExecTimeSeconds;
        }

        public override bool Update()
        {
            if (TimeRunning >= Description.ExecTimeSeconds && !m_IsStealthStarted && !m_IsStealthEnded)
            {
                // start actual stealth-mode... NOW!
                m_IsStealthStarted = true;
                m_Parent.NetState.IsStealthy.Value = 1;
            }
            return !m_IsStealthEnded;
        }

        public override void Cancel()
        {
            EndStealth();
        }

        public override void OnGameplayActivity(GameplayActivity activityType)
        {
            // we break stealth after using an attack. (Or after being hit, which could happen during exec time before we're stealthed, or even afterwards, such as from an AoE attack)
            if (activityType == GameplayActivity.UsingAttackAction || activityType == GameplayActivity.AttackedByEnemy)
            {
                EndStealth();
            }
        }

        private void EndStealth()
        {
            if (!m_IsStealthEnded)
            {
                m_IsStealthEnded = true;
                if (m_IsStealthStarted)
                {
                    m_Parent.NetState.IsStealthy.Value = 0;
                }

                // note that we cancel the ActionFX here, and NOT in Cancel(). That's to handle the case where someone
                // presses the Stealth button twice in a row: "end this Stealth action and start a new one". If we cancelled
                // all actions of this type in Cancel(), we'd end up cancelling both the old AND the new one, because
                // the new one would already be in the clients' actionFX queue.
                m_Parent.NetState.RecvCancelActionsByTypeClientRpc(Description.ActionTypeEnum);
            }
        }

    }
}
