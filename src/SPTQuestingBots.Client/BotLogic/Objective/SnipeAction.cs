using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;

namespace SPTQuestingBots.BotLogic.Objective
{
    public class SnipeAction : AmbushAction
    {
        /// <summary>
        /// Snipers use a slightly higher pose than ambushers (crouch vs deep crouch)
        /// to maintain better weapon accuracy while still exploiting PoseVisibilityCoef.
        /// </summary>
        protected override float AmbushPose => 0.6f;

        public SnipeAction(BotOwner _BotOwner)
            : base(_BotOwner, false) { }
    }
}
