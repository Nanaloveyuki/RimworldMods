using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_RequestFoodDefsTests : IDisposable
    {
        readonly List<ThingDef> previousDefs;
        readonly ThingDef meal;
        readonly ThingDef hardtack;
        readonly Map map;

        public RHAH_RequestFoodDefsTests()
        {
            RimWorldAssemblies.EnsureResolved();
            previousDefs = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
            meal = Def("MealSimple");
            hardtack = Def(RHAH_RequestRules.HardtackDefName);
            map = Uninitialized<Map>();
            map.listerThings = new ListerThings(ListerThingsUse.Global, null);
        }

        public void Dispose()
        {
            DefDatabase<ThingDef>.Clear();
            for (int i = 0; i < previousDefs.Count; i++)
            {
                DefDatabase<ThingDef>.Add(previousDefs[i]);
            }
        }

        [Fact]
        public void SimpleMealRequestAcceptsBothFoods()
        {
            List<ThingDef> defs = new List<ThingDef>();

            RHAH_RequestRules.AppendAcceptedDefs(RHAH_RequestKind.SimpleMeal, defs);

            Assert.Equal(new[] { hardtack, meal }, defs);
        }

        [Fact]
        public void OtherRequestKindsDoNotAcceptHardtack()
        {
            List<ThingDef> defs = new List<ThingDef>();

            RHAH_RequestRules.AppendAcceptedDefs(RHAH_RequestKind.FineMeal, defs);

            Assert.DoesNotContain(hardtack, defs);
        }

        [Fact]
        public void HardtackAloneIsCountedAndChargedOnePiecePerUnit()
        {
            Stock(hardtack, 5);

            Assert.Equal(5, Stock());
            Assert.True(RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, 3));
            Assert.Equal(2, Stock());
            Assert.False(RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, 3));
            Assert.Equal(2, Stock());
        }

        [Fact]
        public void MixedDeliverySumsBothFoodsAndChargesOnlyOnce()
        {
            Stock(meal, 2);
            Stock(hardtack, 3);

            Assert.Equal(5, Stock());
            Assert.True(RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, 4));
            Assert.Equal(1, Stock());
        }

        [Fact]
        public void SimpleMealOnlyStockStillWorks()
        {
            Stock(meal, 4);

            Assert.Equal(4, Stock());
            Assert.True(RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, 4));
            Assert.Equal(0, Stock());
        }

        [Fact]
        public void MissingHardtackLeavesSimpleMealOnly()
        {
            DefDatabase<ThingDef>.Clear();
            for (int i = 0; i < previousDefs.Count; i++)
            {
                DefDatabase<ThingDef>.Add(previousDefs[i]);
            }

            DefDatabase<ThingDef>.Add(meal);

            List<ThingDef> defs = new List<ThingDef>();
            RHAH_RequestRules.AppendAcceptedDefs(RHAH_RequestKind.SimpleMeal, defs);
            Assert.Equal(new[] { meal }, defs);

            Stock(hardtack, 3);
            Assert.Equal(0, Stock());
            Assert.False(RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, 1));
        }

        int Stock()
        {
            return RHAH_ChoiceRuntime.Stock(map, RHAH_RequestKind.SimpleMeal);
        }

        void Stock(ThingDef def, int count)
        {
            StockFood food = new StockFood { def = def, stackCount = count, thingIDNumber = 1000 + count };
            Field(typeof(Thing), "mapIndexOrState").SetValue(food, (sbyte)0);
            Dictionary<ThingDef, List<Thing>> lists =
                (Dictionary<ThingDef, List<Thing>>)Field(typeof(ListerThings), "listsByDef").GetValue(map.listerThings);
            if (!lists.TryGetValue(def, out List<Thing> items))
            {
                lists[def] = items = new List<Thing>();
            }

            items.Add(food);
        }

        // Destroy 只退场 不碰地图登记 否则替身 Thing 会走进原版的掉落与选择器分支
        sealed class StockFood : ThingWithComps
        {
            public override Thing SplitOff(int count)
            {
                if (count >= stackCount)
                {
                    return base.SplitOff(count);
                }

                stackCount -= count;
                return new StockFood { def = def, stackCount = count, thingIDNumber = thingIDNumber + 10000 };
            }

            public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
            {
                ForceSetStateToUnspawned();
            }
        }

        static ThingDef Def(string defName)
        {
            ThingDef existing = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (existing != null)
            {
                return existing;
            }

            ThingDef def = Uninitialized<ThingDef>();
            def.defName = defName;
            def.category = ThingCategory.Item;
            DefDatabase<ThingDef>.Add(def);
            return def;
        }

        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        static FieldInfo Field(Type type, string name) => AccessTools.Field(type, name) ?? throw new InvalidOperationException(type.Name + "." + name);
    }
}
