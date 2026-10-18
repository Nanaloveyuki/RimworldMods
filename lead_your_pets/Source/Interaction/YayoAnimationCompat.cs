using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace LeadYourPet
{
    public static class YayoAnimationCompat
    {
        private static readonly Type PawnDataUtilityType = AccessTools.TypeByName("YayoAnimation.Data.PawnDataUtility");
        private static readonly Type PawnDrawDataType = AccessTools.TypeByName("YayoAnimation.Data.PawnDrawData");
        private static readonly FieldInfo DrawDataDictionaryField = PawnDataUtilityType?.GetField("DrawDataDictionary", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo ValueField = typeof(System.Runtime.CompilerServices.StrongBox<>).MakeGenericType(PawnDrawDataType ?? typeof(object)).GetField("Value");
        private static readonly FieldInfo AngleOffsetField = PawnDrawDataType?.GetField("angleOffset");
        private static readonly FieldInfo PosOffsetField = PawnDrawDataType?.GetField("posOffset");
        private static readonly FieldInfo NextUpdateTickField = PawnDrawDataType?.GetField("nextUpdateTick");
        private static readonly FieldInfo JobNameField = PawnDrawDataType?.GetField("jobName");

        public static bool Active => DrawDataDictionaryField != null && PawnDrawDataType != null;

        public static void Apply(Pawn pawn, Vector3 posOffset, float angleOffset, int nextUpdateTick, string jobName)
        {
            if (!Active || pawn == null)
            {
                return;
            }

            object box = GetOrCreateBox(pawn);
            if (box == null)
            {
                return;
            }

            object data = ValueField.GetValue(box);
            if (data == null)
            {
                data = Activator.CreateInstance(PawnDrawDataType);
                ValueField.SetValue(box, data);
            }

            PosOffsetField?.SetValue(data, posOffset);
            AngleOffsetField?.SetValue(data, angleOffset);
            NextUpdateTickField?.SetValue(data, nextUpdateTick);
            JobNameField?.SetValue(data, jobName ?? pawn.CurJob?.def?.defName);
        }

        public static void Clear(Pawn pawn)
        {
            Apply(pawn, Vector3.zero, 0f, int.MinValue, null);
        }

        private static object GetOrCreateBox(Pawn pawn)
        {
            object dictObj = DrawDataDictionaryField.GetValue(null);
            if (dictObj == null)
            {
                return null;
            }

            MethodInfo tryGetValue = dictObj.GetType().GetMethod("TryGetValue");
            object[] args = { pawn, null };
            bool found = (bool)tryGetValue.Invoke(dictObj, args);
            if (found)
            {
                return args[1];
            }

            Type strongBoxType = typeof(System.Runtime.CompilerServices.StrongBox<>).MakeGenericType(PawnDrawDataType);
            object data = Activator.CreateInstance(PawnDrawDataType);
            object box = Activator.CreateInstance(strongBoxType, data);
            MethodInfo tryAdd = dictObj.GetType().GetMethod("TryAdd");
            tryAdd.Invoke(dictObj, new[] { (object)pawn, box });
            return box;
        }
    }
}
