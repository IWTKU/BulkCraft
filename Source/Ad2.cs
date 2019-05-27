﻿using Harmony;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using System;
using System.Collections.Generic;


namespace Ad2mod
{

    //public class Ad2Settings : ModSettings
    //{
    //    public int defaultThreshold = 20;

    //    public override void ExposeData()
    //    {
    //        Scribe_Values.Look(ref defaultThreshold, "threshold", 20);
    //        base.ExposeData();
    //   }
    //}

    class Ad2Mod : Mod
    {
        //public static Ad2Settings settings;
        public static Ad2Mod instance;

        public Ad2Mod(ModContentPack content) : base(content)
        {
            instance = this;
            //settings = GetSettings<Ad2Settings>();
            //Log.Message("settings.defaultThreshold = " + settings.defaultThreshold.ToString());
        }

        public override string SettingsCategory() => "Bulk craft";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Current.Game == null)
                return;
            List<Bill> bills = Ad2.FindRecipesUses();
            string s = $"Remove modded recipes from save ({bills.Count} found)";
            var w = Text.CalcSize(s).x + 64;
            if (Widgets.ButtonText( new Rect(inRect.x, inRect.y, w, 32), s))
            {
                foreach (Bill bill in bills)
                    bill.billStack.Delete(bill);
                Messages.Message(bills.Count.ToString() + " bills removed", MessageTypeDefOf.NeutralEvent);
            }
        }
    }


    [StaticConstructorOnStartup]
    public class Ad2
    {
        public static Dictionary<RecipeDef, RecipeDef> dict = new Dictionary<RecipeDef, RecipeDef>();

        static Ad2()
        {
            var harmony = HarmonyInstance.Create("com.local.anon.ad2");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            GenRecipes();
        }

        public static List<Bill> FindRecipesUses()
        {
            if (Find.Maps == null)
                throw new Exception("Find.Maps == null");
            var res = new List<Bill>();
            foreach (Map map in Find.Maps)
            {
                foreach (Building_WorkTable wt in map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>())
                {
                    foreach (Bill bill in wt.BillStack)
                    {
                        if (dict.ContainsValue(bill.recipe))
                            res.Add(bill);
                    }
                }
            }
            return res;
        }

        public static RecipeDef MkNewRecipe(RecipeDef rd)
        {
            if (rd.ingredients.Count == 0 || rd.products.Count != 1)
                return null;

            const int FACTOR = 5;
            RecipeDef r = new RecipeDef
            {
                defName = rd.defName + "_5x",
                label = rd.label + " x5",
                description = rd.description + " (x5)",
                jobString = rd.jobString,
                modContentPack = rd.modContentPack,
                workSpeedStat = rd.workSpeedStat,
                efficiencyStat = rd.efficiencyStat,
                fixedIngredientFilter = rd.fixedIngredientFilter,
                productHasIngredientStuff = rd.productHasIngredientStuff,
                workSkill = rd.workSkill,
                workSkillLearnFactor = rd.workSkillLearnFactor,
                skillRequirements = rd.skillRequirements.ListFullCopyOrNull(),
                recipeUsers = rd.recipeUsers.ListFullCopyOrNull(),
                unfinishedThingDef = null, //rd.unfinishedThingDef,
                effectWorking = rd.effectWorking,
                soundWorking = rd.soundWorking,
                allowMixingIngredients = rd.allowMixingIngredients,
                defaultIngredientFilter = rd.defaultIngredientFilter,
            };
            r.products.Add(new ThingDefCountClass(rd.products[0].thingDef, rd.products[0].count * FACTOR));
            List<IngredientCount> new_ingredients = new List<IngredientCount>();
            foreach (var oic in rd.ingredients)
            {
                var nic = new IngredientCount();
                nic.SetBaseCount(oic.GetBaseCount() * FACTOR);
                nic.filter = oic.filter;
                new_ingredients.Add(nic);
            }
            r.ingredients = new_ingredients;
            r.workAmount = rd.WorkAmountTotal(null) * FACTOR;

            Type IVGClass;
            IVGClass = (Type)Traverse.Create(rd).Field("ingredientValueGetterClass").GetValue();
            Traverse.Create(r).Field("ingredientValueGetterClass").SetValue(IVGClass);

            //if (rd.unfinishedThingDef != null)
            //    Log.Message(rd.label + " uses unfinishedThingDef " + rd.unfinishedThingDef.label+"  an it is removed");
            return r;
        }

        public static void GenRecipes()
        {
            const int THRESHOLD = 20;

            var allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            var srcs = new List<RecipeDef>();
            foreach (var recipe in allRecipes)
            {
                if (recipe.products.Count != 1) continue;
                if (recipe.ingredients.Count == 0) continue;
                if (recipe.WorkAmountTotal(null) > THRESHOLD * 60) continue;
                srcs.Add(recipe);
                //Log.Message(recipe.label + "\t" + recipe.defName + "\t" + recipe.WorkAmountTotal(null)/60);
            }
            List<ThingDef> RecipesUsers = new List<ThingDef>();
            foreach (var recipe in srcs)
            {
                var newRecipe = MkNewRecipe(recipe);
                if (newRecipe == null)
                {
                    Log.Warning("newRecipe == null  on "+recipe.label);
                    continue;
                }
                newRecipe.ResolveReferences();
                DefDatabase<RecipeDef>.Add(def: newRecipe);

                dict.Add(recipe, newRecipe);

                //Log.Message(newRecipe.label);
                foreach (var ru in recipe.AllRecipeUsers)
                {
                    if (!RecipesUsers.Contains(ru))
                        RecipesUsers.Add(ru);
                    if (newRecipe.recipeUsers == null)
                        ru.recipes.Add(newRecipe);
                }
            }
            foreach (var ru in RecipesUsers)
                Traverse.Create(ru).Field("allRecipesCached").SetValue(null);
        }
    }

}
