using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EFT;
using SPT.Reflection.Patching;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Patches.Spawning
{
    public class SetNewBossPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            MethodInfo methodInfo = typeof(BossGroup)
                .GetMethods()
                .First(m => m.IsUnmapped() && m.HasAllParameterTypesInOrder(new Type[] { typeof(BotOwner) }));

            Controllers.LoggingController.LogInfo("Found method for SetNewBossPatch: " + methodInfo.Name);

            return methodInfo;
        }

        [PatchPrefix]
        protected static bool PatchPrefix(BossGroup __instance, BotOwner boss, List<BotOwner> followers, ref BotOwner ___Boss_1)
        {
            foreach (BotOwner follower in followers)
            {
                follower.BotFollower.BossToFollow = null;
            }

            // Check if any follower was already designated as the new boss
            // (e.g. by BotHiveMindMonitor). If so, set Boss_1 directly and
            // skip the game's method_0 to avoid a second SetBoss() call on
            // a different random follower, which would leave stale state.
            BotOwner designatedBoss = null;
            foreach (BotOwner follower in followers)
            {
                if (follower.Boss.IamBoss && (follower.Profile.Id != boss.Profile.Id))
                {
                    designatedBoss = follower;
                    break;
                }
            }

            if (designatedBoss != null)
            {
                ___Boss_1 = designatedBoss;
                return false; // Skip original — our mod already called SetBoss
            }

            if (followers.Count > 1)
            {
                LoggingController.LogWarning("Could not find a new boss to replace " + boss.GetText());
            }

            // No mod-designated boss — let the game pick one normally
            return true;
        }
    }
}
