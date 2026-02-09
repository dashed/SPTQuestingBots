using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.HiveMind
{
    public class BotHiveMindCanSprintToObjectiveSensor : BotHiveMindAbstractSensor
    {
        public BotHiveMindCanSprintToObjectiveSensor()
            : base(true) { }

        public override void Update(Action<BotOwner> additionalAction = null)
        {
            Action<BotOwner> updateFromObjectiveManager = new Action<BotOwner>(
                (bot) =>
                {
                    Components.BotObjectiveManager objectiveManager = bot.GetObjectiveManager();
                    bool value;
                    if (objectiveManager != null)
                    {
                        value = objectiveManager.CanSprintToObjective();
                    }
                    else
                    {
                        value = defaultValue;
                    }

                    botState[bot] = value;
                    ECS.BotEntityBridge.UpdateSensor(BotHiveMindSensorType.CanSprintToObjective, bot, value);
                }
            );

            base.Update(updateFromObjectiveManager);
        }
    }
}
