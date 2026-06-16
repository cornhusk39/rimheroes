using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RimHeroes
{
    /// <summary>Quest node + part for the capstone reward: when the quest's success signal fires (the
    /// stranger survived his stay), mark one of the two capstone dungeons on the world map carrying the
    /// hero's class weapon. The hero's class is passed in via the slate by the launcher.</summary>
    public class QuestNode_RH_MarkCapstone : QuestNode
    {
        public override bool TestRunInt(Slate slate) => true;

        public override void RunInt()
        {
            var part = new QuestPart_RH_MarkCapstone
            {
                inSignal = QuestGen.slate.Get<string>("inSignal"),
                heroClass = QuestGen.slate.Get<HeroClassDef>("heroClass")
            };
            QuestGen.quest.AddPart(part);
        }
    }

    public class QuestPart_RH_MarkCapstone : QuestPart
    {
        public string inSignal;
        public HeroClassDef heroClass;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag == inSignal)
                CapstoneQuest.MarkCapstoneDungeon(heroClass);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Defs.Look(ref heroClass, "heroClass");
        }
    }
}
