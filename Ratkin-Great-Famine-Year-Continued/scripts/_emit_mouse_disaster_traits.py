# -*- coding: utf-8 -*-
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]

def T(id, key, zh, en, color, polarity, slot, chance, histories, events, category, tags, conflicts,
      desc_zh, desc_en, offsets=None, factors=None, hunger=None, social_fight=None, market=None, pain=None,
      thoughts=None):
    item = dict(id=id, key=key, zh=zh, en=en, color=color, polarity=polarity, slot=slot, chance=chance,
                histories=histories, events=events, category=category, tags=tags, conflicts=conflicts,
                desc_zh=desc_zh, desc_en=desc_en)
    if offsets:
        item["offsets"] = offsets
    if factors:
        item["factors"] = factors
    if hunger is not None:
        item["hunger"] = hunger
    if social_fight is not None:
        item["social_fight"] = social_fight
    if market is not None:
        item["market"] = market
    if pain is not None:
        item["pain"] = pain
    if thoughts:
        item["thoughts"] = thoughts
    return item

def repeat_stages(zh_label, zh_desc, en_label, en_desc, moods):
    return [dict(zh_label=zh_label, zh_desc=zh_desc, en_label=en_label, en_desc=en_desc, mood=mood) for mood in moods]

FOOD_MOODS = [-8, -14, -20, -26, -32, -38, -44]
REST_MOODS = [-8, -14, -20]
COLD_MOODS = [-6, -10, -14, -18]

def food_thought(zh_label, zh_desc, en_label, en_desc):
    return dict(suffix="Hunger", worker="ThoughtWorker_NeedFood", valid_while_despawned=True,
                stages=repeat_stages(zh_label, zh_desc, en_label, en_desc, FOOD_MOODS))

def rest_thought(zh_label, zh_desc, en_label, en_desc):
    return dict(suffix="Rest", worker="ThoughtWorker_NeedRest",
                stages=repeat_stages(zh_label, zh_desc, en_label, en_desc, REST_MOODS))

def cold_thought(zh_label, zh_desc, en_label, en_desc):
    return dict(suffix="Cold", worker="ThoughtWorker_Cold",
                stages=repeat_stages(zh_label, zh_desc, en_label, en_desc, COLD_MOODS))

TRAITS = [
    T("T01","HardyLabor","吃苦","Hardy","blue","Positive","Any",0.72,
      ["A001","A002","A005","A068","Y030","Y031"],[],"None",["MouseDisasterWorkSpeed"],["Industriousness"],
      "[PAWN_nameDef]吃得了苦，苦活也能一直干下去。",
      "[PAWN_nameDef] can take hardship, and will keep at unpleasant work.",
      offsets={"WorkSpeedGlobal": 0.15}),
    T("T02","FamineFarmer","农熟","Agrarian","green","Positive","Any",0.70,
      ["A001","A002","A003","A068","Y031"],["O-011","N-030","N-044"],"None",["MouseDisasterFarming"],[],
      "[PAWN_nameDef]对田里的活很熟，种地收割都在行。",
      "[PAWN_nameDef] knows field work well.",
      offsets={"PlantWorkSpeed": 0.20}),
    T("T03","FieldGleaner","拾荒","Foraging","green","Positive","Any",0.68,
      ["A003","A016","Y001","Y018"],["O-008","O-010"],"None",["MouseDisasterForage"],[],
      "[PAWN_nameDef]习惯在边角和废地里翻还能吃的东西。",
      "[PAWN_nameDef] is used to picking through edges and waste ground for anything edible.",
      offsets={"ForagedNutritionPerDay": 0.45, "HuntingStealth": 0.08}),
    T("T04","DisasterHerder","善牧","Husbandry","green","Positive","Any",0.66,
      ["A004","Y007","Y032","Y045"],[],"None",["MouseDisasterAnimals"],[],
      "[PAWN_nameDef]会照看牲口，受惊的活物也肯靠近[PAWN_objective]。",
      "[PAWN_nameDef] is good with animals. Even frightened livestock will come close to [PAWN_objective].",
      factors={"TameAnimalChance": 1.25, "TrainAnimalChance": 1.20}),
    T("T05","WellBearer","能扛","Sturdy","blue","Positive","Any",0.64,
      ["A005","Y033","Y021"],[],"None",["MouseDisasterMove"],["SpeedOffset"],
      "[PAWN_nameDef]肩上压得住分量，搬东西不怎么怕沉。",
      "[PAWN_nameDef] can take a lot of weight without minding the load.",
      offsets={"CarryingCapacity": 12, "MoveSpeed": 0.08}),
    T("T06","MoldSifter","识味","Palate","gold","Positive","Any",0.62,
      ["A007","A029","Y035"],["N-011","N-012"],"None",["MouseDisasterCooking"],[],
      "[PAWN_nameDef]闻得出哪一把粮还能下锅。",
      "[PAWN_nameDef] can tell which grain can still be cooked.",
      offsets={"CookSpeed": 0.18}),
    T("T07","GranaryCarpenter","善建","Handy","blue","Positive","Any",0.64,
      ["A009","A024","Y036","Y042"],[],"None",["MouseDisasterBuild"],[],
      "[PAWN_nameDef]修屋顶、补木架这类活很在行。",
      "[PAWN_nameDef] is handy with roofs and wooden frames.",
      offsets={"ConstructionSpeed": 0.20}),
    T("T08","RagTailor","巧手","Mender","blue","Positive","Any",0.60,
      ["A010","Y037"],[],"None",["MouseDisasterCraft"],[],
      "[PAWN_nameDef]擅长把破布和旧毡缝成还能用的东西。",
      "[PAWN_nameDef] is good at stitching rags and old felt into something usable.",
      offsets={"GeneralLaborSpeed": 0.16}),
    T("T09","HerbReader","识药","Herbal","purple","Positive","Any",0.66,
      ["A012","A030","A060","Y039"],["N-013"],"None",["MouseDisasterMedical"],[],
      "[PAWN_nameDef]认得哪些草还能退热，包扎也还算靠得住。",
      "[PAWN_nameDef] knows which herbs still bring a fever down, and can be trusted with a bandage.",
      offsets={"MedicalTendSpeed": 0.16}),
    T("T10","NightFugitive","夜行","Nocturnal","brown","Positive","Any",0.63,
      ["A014","A017","Y006","Y018"],["O-008","O-009"],"None",["MouseDisasterMove","MouseDisasterStealth"],["SpeedOffset"],
      "[PAWN_nameDef]喜欢夜里走路，暗处也不容易绊倒。",
      "[PAWN_nameDef] prefers to move at night, and doesn't stumble much in the dark.",
      offsets={"MoveSpeed": 0.12, "HuntingStealth": 0.12}),
    T("T11","SnowGuide","识途","Pathwise","green","Positive","Any",0.58,
      ["A017","Y044"],[],"None",["MouseDisasterTemp"],[],
      "[PAWN_nameDef]记得旧路标和被雪盖住的脚印，探路很在行。",
      "[PAWN_nameDef] remembers old trail markers and tracks buried in snow.",
      offsets={"HuntingStealth": 0.10, "MoveSpeed": 0.08}),
    T("T12","DustBearer","驮运","Bearer","blue","Mixed","Any",0.58,
      ["A018","Y045"],["O-012"],"None",["MouseDisasterMove"],[],
      "[PAWN_nameDef]愿意把粮和饮水一直背到下一处营地，路上很少把东西卸下来。",
      "[PAWN_nameDef] will carry grain and water all the way to the next camp, and rarely puts the load down.",
      offsets={"CarryingCapacity": 18, "MoveSpeed": -0.04}),
    T("T13","CampWatcher","警醒","Vigilant","red","Positive","Any",0.60,
      ["A019","Y012","Y046"],["O-014","N-026"],"Siege",["MouseDisasterMood"],["Nerves"],
      "[PAWN_nameDef]不容易慌。营地乱的时候，[PAWN_pronoun]还能把精神绷住。",
      "[PAWN_nameDef] doesn't panic easily. When the camp is in chaos, [PAWN_pronoun] can still keep a clear head.",
      offsets={"MentalBreakThreshold": -0.06}),
    T("T14","AlmsSpeaker","善言","Eloquent","gold","Positive","Any",0.70,
      ["A026","A052","A058","Y016"],["O-004","O-005","O-011","N-027","N-038"],"None",["MouseDisasterSocial"],[],
      "[PAWN_nameDef]会说话，难听的事也能让人听得进去。",
      "[PAWN_nameDef] has a way with words. Even hard news comes out in a way people will listen to.",
      offsets={"NegotiationAbility": 0.16, "SocialImpact": 0.10}),
    T("T15","JunkAppraiser","精明","Shrewd","gold","Positive","Adult",0.62,
      ["A035","A057","A061"],["O-012","O-013","N-014"],"None",["MouseDisasterSocial"],[],
      "[PAWN_nameDef]看得出一袋旧货还能换多少口粮，谈价钱很精。",
      "[PAWN_nameDef] can tell what a sack of old goods is still worth, and is sharp in a trade.",
      offsets={"NegotiationAbility": 0.18}),
    T("T16","GrainMediator","和事","Peaceable","gold","Positive","Any",0.60,
      ["A036","A055","Y016","Y022"],["N-011","N-012","O-001"],"None",["MouseDisasterSocial"],[],
      "[PAWN_nameDef]会在抢夺开始前先把人分开，场面不容易一下子散掉。",
      "[PAWN_nameDef] steps in before a crowd starts grabbing, and can keep a situation from falling apart.",
      offsets={"SocialImpact": 0.14}),
    T("T17","WoundTender","细心","Careful","purple","Positive","Any",0.64,
      ["A030","A046","A060","Y028"],["N-013"],"Plague",["MouseDisasterMedical"],[],
      "[PAWN_nameDef]包扎时手很稳，气味和哭声也很少让[PAWN_objective]停下来。",
      "[PAWN_nameDef] has a steady hand with wounds, and smell or crying rarely stops [PAWN_objective].",
      offsets={"MedicalTendSpeed": 0.20}),
    T("T18","EggKeeper","护幼","Broody","gold","Positive","Any",0.68,
      ["A031","A050","A051","Y005","Y014","Y015"],["O-002","O-003","O-013","N-015"],"None",["MouseDisasterSocial"],[],
      "[PAWN_nameDef]总是把幼崽放在前面。身边的孩子挨饿或病倒时，[PAWN_pronoun]会很难受。",
      "[PAWN_nameDef] puts the young first. If a child nearby goes hungry or falls ill, [PAWN_pronoun] takes it hard.",
      offsets={"SocialImpact": 0.10},
      thoughts=[dict(suffix="YoungInNeed", worker="MouseDisaster.ThoughtWorker_MouseDisasterTraitYoungInNeed",
                     stages=[dict(zh_label="幼崽在饿", zh_desc="身边的幼崽还在挨饿。", en_label="young going hungry", en_desc="A child nearby is still hungry.", mood=-5),
                             dict(zh_label="幼崽病了", zh_desc="身边的幼崽病了或倒下了。", en_label="young in danger", en_desc="A child nearby is sick or downed.", mood=-9)])]),
    T("T19","LeanHunger","耐饿","Abstemious","brown","Positive","Any",0.66,
      ["A014","A054","A068","Y018"],["N-030","N-044"],"None",["MouseDisasterHunger"],["Gourmand"],
      "[PAWN_nameDef]习惯把一顿饭拆成两顿，胃空着也能继续走。",
      "[PAWN_nameDef] is used to splitting one meal into two, and can keep going on an empty stomach.",
      hunger=0.85),
    T("T20","ThickFur","耐寒","Thick-furred","green","Positive","Any",0.55,
      ["A017","Y044"],[],"None",["MouseDisasterTemp"],[],
      "[PAWN_nameDef]比较耐寒。",
      "[PAWN_nameDef] handles the cold well.",
      offsets={"ComfyTemperatureMin": -10}),
    T("T21","FinePaws","灵巧","Deft","blue","Positive","Any",0.56,
      ["A010","A008","Y037","Y008"],[],"None",["MouseDisasterCraft"],[],
      "[PAWN_nameDef]做补袋、穿针这类细活很顺手。",
      "[PAWN_nameDef] has a sure hand with fine work like mending sacks and threading needles.",
      offsets={"GeneralLaborSpeed": 0.12}),
    T("T22","PathScout","机敏","Alert","brown","Positive","Any",0.64,
      ["A021","A023","A074","Y006","Y023"],["N-016","N-019","N-022"],"None",["MouseDisasterStealth"],[],
      "[PAWN_nameDef]对动静很敏感，走路前总要先看路。",
      "[PAWN_nameDef] notices movement early and checks the path before stepping.",
      offsets={"HuntingStealth": 0.18}),
    T("T23","ColumnLead","果断","Decisive","gold","Positive","Adult",0.60,
      ["A023","A074","A055"],["O-001","N-027"],"None",["MouseDisasterSocial","MouseDisasterMove"],[],
      "[PAWN_nameDef]上路前会先把人排好。该谁走、该谁看着幼崽，[PAWN_pronoun]说了就算。",
      "[PAWN_nameDef] lines people up before moving. Who walks first and who watches the young is for [PAWN_pronoun] to decide.",
      offsets={"SocialImpact": 0.08, "MoveSpeed": 0.10}),
    T("T24","ReliefCook","能炊","Culinary","gold","Positive","Any",0.62,
      ["A007","A029","A059","Y035"],["N-011","N-012","O-011"],"None",["MouseDisasterCooking"],[],
      "[PAWN_nameDef]能用很少的粮做出还能分的热食。",
      "[PAWN_nameDef] can turn a little grain into a hot meal that can still be shared.",
      offsets={"CookSpeed": 0.22}),
    T("T25","FamineHunter","好猎","Hunter","red","Positive","Any",0.58,
      ["A042","Y038"],["O-008","O-010"],"None",["MouseDisasterCombat"],["Brawler"],
      "[PAWN_nameDef]习惯近身把猎物按住，靠近时脚步很轻。",
      "[PAWN_nameDef] likes to pin prey up close, and moves quietly on the approach.",
      offsets={"MeleeHitChance": 4, "HuntingStealth": 0.10}),
    T("T26","HungerRage","眼红","Ravenous","red","Negative","Any",0.70,
      ["A041","A049","A038"],["N-030","N-044"],"Theft",["MouseDisasterMood","MouseDisasterCombat"],["Kind"],
      "[PAWN_nameDef]一看见食物就容易红眼。肚子越空，越难按住爪子。",
      "[PAWN_nameDef] sees food and the eyes go red. The emptier the stomach, the harder it is to keep still.",
      social_fight=1.8,
      thoughts=[food_thought("眼红","我还要吃。现在就要。","ravenous","I need food. Now.")]),
    T("T27","FoodSnatcher","抢食","Snatching","red","Negative","Any",0.72,
      ["A041","A053","Y017"],["O-006","O-007","N-031","N-039"],"Theft",["MouseDisasterCombat","MouseDisasterSocial"],["Kind"],
      "[PAWN_nameDef]看不得食物从眼前过去。肚子一空，爪子就会先伸向袋口。",
      "[PAWN_nameDef] can hardly watch food pass by. When hungry, the paws go to the sack first.",
      social_fight=2.0,
      thoughts=[food_thought("抢食","那袋粮不该从我眼前过去。","snatching","That food should not pass me by.")]),
    T("T28","PlagueDread","怕疫","Plague-shy","purple","Negative","Any",0.74,
      ["A044","A045","A070","Y010","Y028"],[],"Plague",["MouseDisasterPlague","MouseDisasterMood"],["Immunity"],
      "[PAWN_nameDef]特别怕疫病。自己生病，或身边有人病倒时，[PAWN_pronoun]会明显不安。",
      "[PAWN_nameDef] is especially afraid of plague. Being sick, or seeing someone nearby fall ill, makes [PAWN_pronoun] uneasy.",
      thoughts=[
        dict(suffix="Sick", worker="ThoughtWorker_Sick",
             stages=[dict(zh_label="怕病", zh_desc="我病了。我怕这不是小病。", en_label="afraid of sickness", en_desc="I am sick. I fear this is not a small illness.", mood=-8)]),
        dict(suffix="NearbyDisease", worker="MouseDisaster.ThoughtWorker_MouseDisasterTraitNearbyDisease",
             stages=[dict(zh_label="怕疫", zh_desc="身边有人生病了。我怕它会传到我身上。", en_label="plague nearby", en_desc="Someone nearby is sick. I fear it will reach me.", mood=-6)]),
      ]),
    T("T29","NightTerrors","易惊","Jumpy","purple","Negative","Any",0.60,
      ["A019","Y012","Y046","Y041"],[],"None",["MouseDisasterMood"],["QuickSleeper"],
      "[PAWN_nameDef]睡眠很浅，一点动静就容易醒。越累越慌。",
      "[PAWN_nameDef] sleeps lightly and wakes at small sounds. Tiredness only makes the panic worse.",
      offsets={"RestRateMultiplier": -0.15, "RestFallRateFactor": 0.12},
      thoughts=[rest_thought("易惊","我太累了，一点声音都让我慌。","jumpy","I am too tired, and every sound makes me start.")]),
    T("T30","FamineSloth","怠惰","Slothful","gray","Negative","Any",0.58,
      ["A006","A032","Y009","Y041"],[],"None",["MouseDisasterWorkSpeed"],["Industriousness"],
      "[PAWN_nameDef]有点懒，能歇着就先歇着。",
      "[PAWN_nameDef] is a little lazy, and will rest if there's a chance to rest.",
      offsets={"WorkSpeedGlobal": -0.15}),
    T("T31","GrainGreed","贪粮","Gluttonous","brown","Negative","Any",0.64,
      ["A037","A038","Y034"],["O-012"],"None",["MouseDisasterHunger"],["Ascetic"],
      "[PAWN_nameDef]面前有粮就停不住嘴，饿着的时候特别难受。",
      "[PAWN_nameDef] can't stop eating while food is in reach, and hunger hits hard.",
      hunger=1.25, offsets={"EatingSpeed": 0.20},
      thoughts=[food_thought("贪粮","我还没吃够。粮还在的时候就该吃掉。","gluttonous","I have not had enough. Eat it while it is still here.")]),
    T("T32","Distrustful","多疑","Distrustful","gray","Negative","Any",0.60,
      ["A020","A067","Y026"],["N-029","N-043"],"None",["MouseDisasterSocial"],["Kind"],
      "[PAWN_nameDef]很难再相信别人。好意也常被[PAWN_objective]看成圈套。",
      "[PAWN_nameDef] has a hard time trusting anyone. Even kindness often looks like a trap to [PAWN_objective].",
      offsets={"SocialImpact": -0.15, "NegotiationAbility": -0.10, "MentalBreakThreshold": 0.05}),
    T("T33","WeakFlesh","体弱","Frail","purple","Negative","Any",0.58,
      ["A011","A044","A046"],[],"Plague",["MouseDisasterHealth"],["Tough"],
      "[PAWN_nameDef]身子很弱，经不起磕碰。",
      "[PAWN_nameDef] is frail, and doesn't take knocks well.",
      offsets={"IncomingDamageFactor": 0.10}, pain=0.06),
    T("T34","WeakGut","胃弱","Queasy","brown","Negative","Any",0.56,
      ["A007","A044","Y004"],[],"Plague",["MouseDisasterHunger"],["Gourmand"],
      "[PAWN_nameDef]胃不好，刚吃过不久就又饿了。",
      "[PAWN_nameDef] has a weak gut, and gets hungry again soon after a meal.",
      hunger=1.20),
    T("T35","Chillblood","怕冷","Chilly","gray","Negative","Any",0.52,
      ["A017","Y044","Y018"],[],"None",["MouseDisasterTemp"],[],
      "[PAWN_nameDef]很怕冷。寒风一来，[PAWN_pronoun]就容易难受。",
      "[PAWN_nameDef] hates the cold. When the wind picks up, [PAWN_pronoun] suffers for it.",
      offsets={"ComfyTemperatureMin": 8},
      thoughts=[cold_thought("怕冷","太冷了。我比别人更受不了。","chilly","It is too cold. I can take less of this than the others.")]),
    T("T36","FamineGloom","悲观","Gloomy","gray","Negative","Any",0.62,
      ["A020","A022","Y011","Y041"],["O-003","N-030"],"None",["MouseDisasterMood"],["NaturalMood"],
      "[PAWN_nameDef]很难相信接下来会有好事，压力一大就容易撑不住。",
      "[PAWN_nameDef] has a hard time believing anything good is coming, and cracks more easily under pressure.",
      offsets={"MentalBreakThreshold": 0.10},
      thoughts=[dict(suffix="Gloom", worker="ThoughtWorker_AlwaysActive", valid_while_despawned=True,
                     stages=[dict(zh_label="悲观", zh_desc="接下来大概也不会有好事。", en_label="gloomy", en_desc="Nothing good is coming after this either.", mood=-6)])]),
    T("T37","WailingYoung","爱哭","Tearful","gray","Negative","Young",0.70,
      ["Y001","Y011","Y013","Y015","Y041"],["O-002","O-009","N-029"],"None",["MouseDisasterChildTemper","MouseDisasterSocial"],[],
      "[PAWN_nameDef]还小，饿了、冷了就爱哭。",
      "[PAWN_nameDef] is still small, and cries easily when hungry or cold.",
      offsets={"SocialImpact": -0.10, "RestFallRateFactor": 0.10, "MentalBreakThreshold": 0.05}),
    T("T38","BitingYoung","好咬","Biting","red","Negative","Young",0.68,
      ["Y017","Y043","Y019"],["O-007","N-039"],"Theft",["MouseDisasterChildTemper","MouseDisasterCombat"],["Kind"],
      "[PAWN_nameDef]还没学会把饥饿藏起来。靠近食物或被人抓住时，就会咬人。",
      "[PAWN_nameDef] hasn't learned to hide hunger. Near food, or when grabbed, biting follows.",
      social_fight=2.4, offsets={"SocialImpact": -0.08}),
    T("T39","AlmsHate","傲骨","Proud","red","Negative","Adult",0.58,
      ["A026","A058","A039"],["O-004","O-005","O-014"],"None",["MouseDisasterSocial"],["Kind"],
      "[PAWN_nameDef]把别人的施舍看成羞辱，接过东西也不肯低头道谢。",
      "[PAWN_nameDef] treats charity as a humiliation, and won't bow to give thanks after taking what's offered."),
    T("T40","GrudgeKeeper","记仇","Vindictive","red","Negative","Adult",0.60,
      ["A039","A040","A048"],["O-014","N-026","N-031"],"Conflict",["MouseDisasterCombat","MouseDisasterMood"],["Kind"],
      "[PAWN_nameDef]谁关过门、谁抢过东西都还记着。再碰面，旧账会先翻出来。",
      "[PAWN_nameDef] remembers who shut a gate and who took what was needed. The next meeting starts with the old grudge.",
      offsets={"MentalBreakThreshold": 0.05}, social_fight=1.6),
    T("T41","PlagueSurvivor","抗疫","Hardened","purple","Mixed","Any",0.66,
      ["A046","A070","Y028"],[],"Plague",["MouseDisasterPlague"],["Immunity"],
      "[PAWN_nameDef]从疫病里活下来过。病再来也还能撑，只是人容易乏。",
      "[PAWN_nameDef] survived plague once. Illness is easier to endure now, but tiredness comes more quickly.",
      offsets={"ImmunityGainSpeed": 0.20, "RestFallRateFactor": 0.08}),
    T("T42","GrainSneak","藏锋","Guileful","brown","Mixed","Any",0.64,
      ["A047","A041","Y017"],["O-006","O-007"],"Theft",["MouseDisasterStealth","MouseDisasterSocial"],[],
      "[PAWN_nameDef]不爱把真实想法写在脸上，靠近时脚步很轻。",
      "[PAWN_nameDef] doesn't show what is really wanted, and moves quietly when closing in.",
      offsets={"HuntingStealth": 0.15, "SocialImpact": -0.08}),
    T("T43","SiegeDigger","善掘","Digger","red","Mixed","Adult",0.62,
      ["A048","A072","A015"],["O-014","N-026","N-041"],"Siege",["MouseDisasterBuild"],[],
      "[PAWN_nameDef]挖出路和壕沟很在行。",
      "[PAWN_nameDef] is good with a shovel, especially digging ways out and trenches.",
      offsets={"MiningSpeed": 0.20, "ConstructionSpeed": 0.08}),
    T("T44","GranaryBreaker","好斗","Fierce","red","Mixed","Adult",0.60,
      ["A049","A038"],["N-030","N-044"],"Conflict",["MouseDisasterCombat"],["Brawler"],
      "[PAWN_nameDef]不怕近身打架。话还没说完，拳头就已经准备好了。",
      "[PAWN_nameDef] doesn't mind a close fight. The fists are ready before the sentence is finished.",
      offsets={"MeleeHitChance": 3}, social_fight=1.35),
    T("T45","AilingMother","舐犊","Maternal","purple","Mixed","Adult",0.70,
      ["A051","A052","A065"],["O-003","N-025","N-040"],"None",["MouseDisasterMedical","MouseDisasterWorkSpeed"],[],
      "[PAWN_nameDef]总是把孩子放在自己前面。孩子挨饿或病倒时，[PAWN_pronoun]会先乱。",
      "[PAWN_nameDef] puts the children first. If a child is hungry or sick, [PAWN_pronoun] unravels.",
      offsets={"MedicalTendSpeed": 0.10},
      thoughts=[dict(suffix="YoungInNeed", worker="MouseDisaster.ThoughtWorker_MouseDisasterTraitYoungInNeed",
                     stages=[dict(zh_label="孩子在饿", zh_desc="我的孩子还在挨饿。", en_label="child going hungry", en_desc="My child is still hungry.", mood=-6),
                             dict(zh_label="孩子病了", zh_desc="我的孩子病了或倒下了。", en_label="child in danger", en_desc="My child is sick or downed.", mood=-10)])]),
    T("T46","FamilyThief","护窝","Clannish","brown","Mixed","Any",0.66,
      ["A053","Y017"],["O-006","O-007","N-039"],"Theft",["MouseDisasterStealth"],[],
      "[PAWN_nameDef]把自己人看得比规矩重。窝里的幼小挨饿或病倒时，[PAWN_pronoun]会坐不住。",
      "[PAWN_nameDef] puts kin above rules. If the young of the den go hungry or fall ill, [PAWN_pronoun] can't sit still.",
      thoughts=[dict(suffix="YoungInNeed", worker="MouseDisaster.ThoughtWorker_MouseDisasterTraitYoungInNeed",
                     stages=[dict(zh_label="窝里在饿", zh_desc="窝里的幼小还在挨饿。", en_label="den going hungry", en_desc="The young of the den are still hungry.", mood=-5),
                             dict(zh_label="窝里病了", zh_desc="窝里的幼小病了或倒下了。", en_label="den in danger", en_desc="The young of the den are sick or downed.", mood=-9)])]),
    T("T47","QuarantineGuard","冷面","Aloof","purple","Mixed","Adult",0.60,
      ["A045","A071"],[],"Plague",["MouseDisasterPlague","MouseDisasterSocial"],[],
      "[PAWN_nameDef]不容易被亲近。把人分开时手硬，话也少。",
      "[PAWN_nameDef] is hard to get close to, and keeps people apart with a firm hand and few words.",
      offsets={"SocialImpact": -0.12}),
    T("T48","WrongKin","轻信","Gullible","gold","Mixed","Any",0.58,
      ["A067","Y026"],["N-029","N-043","N-028","N-042"],"None",["MouseDisasterSocial","MouseDisasterMood"],[],
      "[PAWN_nameDef]容易把陌生的门认成家，认错了也容易失落。",
      "[PAWN_nameDef] will take a strange door for home, and takes it hard when that guess is wrong.",
      offsets={"SocialImpact": 0.08, "MentalBreakThreshold": 0.06}),
    T("T49","CrowdChild","扎堆","Gregarious","gray","Mixed","Young",0.62,
      ["Y013","Y025","Y027"],["O-001","N-028"],"None",["MouseDisasterMove","MouseDisasterSocial"],[],
      "[PAWN_nameDef]在人堆里更自在。单独待着时反而说不好话。",
      "[PAWN_nameDef] is more at ease in a crowd, and talks poorly when left alone.",
      offsets={"MoveSpeed": 0.10, "SocialImpact": -0.06}),
    T("T50","WildHole","野性","Feral","green","Mixed","Any",0.64,
      ["A054","Y018","Y001"],["O-008","O-009","O-010"],"None",["MouseDisasterForage","MouseDisasterStealth"],[],
      "[PAWN_nameDef]习惯先找藏身处，再找能入口的东西。荒地边角比仓库更让[PAWN_objective]安心。",
      "[PAWN_nameDef] looks first for cover, then for anything edible. Waste ground feels safer to [PAWN_objective] than a storehouse.",
      offsets={"ForagedNutritionPerDay": 0.35, "HuntingStealth": 0.10}),
]

KNOWN_EVENTS = {
    "O-001","O-002","O-003","O-004","O-005","O-006","O-007","O-008","O-009","O-010","O-011","O-012","O-013","O-014",
    "N-011","N-012","N-013","N-014","N-015","N-016","N-017","N-018","N-019","N-020","N-021","N-022","N-023","N-024",
    "N-025","N-026","N-027","N-028","N-029","N-030","N-031","N-032","N-033","N-034","N-035","N-036","N-037","N-038",
    "N-039","N-040","N-041","N-042","N-043","N-044","N-045","N-046","N-047",
}

WORKER_STAGE_COUNTS = {
    "ThoughtWorker_NeedFood": 7,
    "ThoughtWorker_NeedRest": 3,
    "ThoughtWorker_Cold": 4,
    "ThoughtWorker_AlwaysActive": 1,
    "ThoughtWorker_Sick": 1,
    "MouseDisaster.ThoughtWorker_MouseDisasterTraitNearbyDisease": 1,
    "MouseDisaster.ThoughtWorker_MouseDisasterTraitYoungInNeed": 2,
}

# ColorLibrary named colors, written in the same byte triple form as Core ColorDefs.
LIBRARY_COLORS = {
    "blue": (3, 67, 223),
    "green": (21, 176, 26),
    "gold": (219, 180, 12),
    "purple": (126, 30, 156),
    "brown": (101, 55, 0),
    "red": (229, 0, 0),
    "gray": (146, 149, 145),
}

def xml_escape(text):
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))

def fmt_num(value):
    if isinstance(value, float):
        text = f"{value:.2f}".rstrip("0").rstrip(".")
        if "." not in text:
            return text
        return text
    return str(value)

def csharp_strings(items):
    if not items:
        return "new string[0]"
    return "new[] { " + ", ".join(f'"{item}"' for item in items) + " }"

def xml_map(tag, mapping, indent=8):
    if not mapping:
        return ""
    pad = " " * indent
    inner = "\n".join(f"{pad}  <{key}>{fmt_num(value)}</{key}>" for key, value in mapping.items())
    return f"{pad}<{tag}>\n{inner}\n{pad}</{tag}>\n"

def load_history_ids():
    text = (ROOT / "1.6/Source/MouseDisasterPawnHistoryData.cs").read_text(encoding="utf-8")
    return set(re.findall(r'"((?:A|Y)\d{3})"', text))

def thought_def_name(trait, thought):
    return f"MouseDisaster_Thought_{trait['key']}_{thought['suffix']}"

def placeholders(text):
    return sorted(set(re.findall(r"\{[^{}]+\}|\[[A-Za-z][A-Za-z0-9_]*\]", text or "")))

def validate():
    if len(TRAITS) != 50:
        raise SystemExit(f"expected 50 traits, got {len(TRAITS)}")
    ids = [t["id"] for t in TRAITS]
    keys = [t["key"] for t in TRAITS]
    zhs = [t["zh"] for t in TRAITS]
    ens = [t["en"] for t in TRAITS]
    if len(set(ids)) != 50 or ids != [f"T{i:02d}" for i in range(1, 51)]:
        raise SystemExit(f"bad ids: {ids}")
    if len(set(keys)) != 50:
        raise SystemExit("duplicate keys")
    if len(set(zhs)) != 50:
        raise SystemExit("duplicate zh names")
    if len(set(ens)) != 50:
        raise SystemExit("duplicate en names")
    history_ids = load_history_ids()
    for trait in TRAITS:
        zh_len = len(re.findall(r"[\u3400-\u9fff]", trait["zh"]))
        if zh_len < 1 or zh_len > 4:
            raise SystemExit(f"{trait['id']} zh name length {zh_len}: {trait['zh']}")
        for hid in trait["histories"]:
            if hid not in history_ids:
                raise SystemExit(f"{trait['id']} unknown history {hid}")
        for eid in trait["events"]:
            if eid not in KNOWN_EVENTS:
                raise SystemExit(f"{trait['id']} unknown event {eid}")
        if trait["category"] not in {"None", "Plague", "Theft", "Conflict", "Siege"}:
            raise SystemExit(f"{trait['id']} bad category")
        if trait["slot"] not in {"Any", "Young", "Adult"}:
            raise SystemExit(f"{trait['id']} bad slot")
        if trait["polarity"] not in {"Positive", "Negative", "Mixed"}:
            raise SystemExit(f"{trait['id']} bad polarity")
        if trait["color"] not in LIBRARY_COLORS:
            raise SystemExit(f"{trait['id']} unknown ColorLibrary color {trait['color']}")
        if "[PAWN_nameDef]" not in trait["desc_zh"] or "[PAWN_nameDef]" not in trait["desc_en"]:
            raise SystemExit(f"{trait['id']} missing pawn placeholder")
        zh_placeholders = placeholders(trait["desc_zh"])
        en_placeholders = placeholders(trait["desc_en"])
        if zh_placeholders != en_placeholders:
            raise SystemExit(f"{trait['id']} placeholder mismatch: {zh_placeholders} vs {en_placeholders}")
        if re.search(r"[\u3400-\u9fff]", trait["desc_en"] + trait["en"]):
            raise SystemExit(f"{trait['id']} chinese in english")
        for thought in trait.get("thoughts") or []:
            expected = WORKER_STAGE_COUNTS.get(thought["worker"])
            if expected is not None and len(thought["stages"]) != expected:
                raise SystemExit(f"{trait['id']} {thought['suffix']} expected {expected} stages, got {len(thought['stages'])}")
            for stage in thought["stages"]:
                if placeholders(stage["zh_label"]) != placeholders(stage["en_label"]):
                    raise SystemExit(f"{trait['id']} thought label placeholder mismatch")
                if placeholders(stage["zh_desc"]) != placeholders(stage["en_desc"]):
                    raise SystemExit(f"{trait['id']} thought desc placeholder mismatch")
                if re.search(r"[\u3400-\u9fff]", stage["en_label"] + stage["en_desc"]):
                    raise SystemExit(f"{trait['id']} chinese in thought english")

def emit_xml():
    blocks = ['<?xml version="1.0" encoding="utf-8" ?>', "<Defs>"]
    for trait in TRAITS:
        def_name = f"MouseDisaster_Trait_{trait['key']}"
        blocks.append("  <TraitDef>")
        blocks.append(f"    <defName>{def_name}</defName>")
        blocks.append("    <commonality>0</commonality>")
        if trait["tags"]:
            blocks.append("    <exclusionTags>")
            for tag in trait["tags"]:
                blocks.append(f"      <li>{tag}</li>")
            blocks.append("    </exclusionTags>")
        if trait["conflicts"]:
            blocks.append("    <conflictingTraits>")
            for name in trait["conflicts"]:
                blocks.append(f"      <li>{name}</li>")
            blocks.append("    </conflictingTraits>")
        blocks.append("    <degreeDatas>")
        blocks.append('      <li Class="MouseDisaster.MouseDisasterTraitDegreeData">')
        blocks.append(f"        <label>{xml_escape(trait['zh'])}</label>")
        blocks.append(f"        <description>{xml_escape(trait['desc_zh'])}</description>")
        red, green, blue = LIBRARY_COLORS[trait["color"]]
        blocks.append(f"        <color>({red}, {green}, {blue})</color>")
        offsets = trait.get("offsets") or {}
        factors = trait.get("factors") or {}
        if offsets:
            blocks.append(xml_map("statOffsets", offsets, 8).rstrip("\n"))
        if factors:
            blocks.append(xml_map("statFactors", factors, 8).rstrip("\n"))
        if "hunger" in trait:
            blocks.append(f"        <hungerRateFactor>{fmt_num(trait['hunger'])}</hungerRateFactor>")
        if "social_fight" in trait:
            blocks.append(f"        <socialFightChanceFactor>{fmt_num(trait['social_fight'])}</socialFightChanceFactor>")
        if "market" in trait:
            blocks.append(f"        <marketValueFactorOffset>{fmt_num(trait['market'])}</marketValueFactorOffset>")
        if "pain" in trait:
            blocks.append(f"        <painOffset>{fmt_num(trait['pain'])}</painOffset>")
        blocks.append("      </li>")
        blocks.append("    </degreeDatas>")
        blocks.append("    <modExtensions>")
        blocks.append('      <li Class="MouseDisaster.MouseDisasterGenerationExtension">')
        blocks.append("        <allowTrait>true</allowTrait>")
        blocks.append("      </li>")
        blocks.append("    </modExtensions>")
        blocks.append("  </TraitDef>")
        blocks.append("")
    blocks.append("</Defs>")
    blocks.append("")
    path = ROOT / "Defs/TraitDefs/Traits_MouseDisaster.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(blocks), encoding="utf-8", newline="\n")

def emit_thought_xml():
    blocks = ['<?xml version="1.0" encoding="utf-8" ?>', "<Defs>"]
    for trait in TRAITS:
        for thought in trait.get("thoughts") or []:
            blocks.append("  <ThoughtDef>")
            blocks.append(f"    <defName>{thought_def_name(trait, thought)}</defName>")
            blocks.append(f"    <workerClass>{thought['worker']}</workerClass>")
            if thought.get("valid_while_despawned"):
                blocks.append("    <validWhileDespawned>true</validWhileDespawned>")
            blocks.append("    <requiredTraits>")
            blocks.append(f"      <li>MouseDisaster_Trait_{trait['key']}</li>")
            blocks.append("    </requiredTraits>")
            blocks.append("    <developmentalStageFilter>Baby, Child, Adult</developmentalStageFilter>")
            blocks.append("    <stages>")
            for stage in thought["stages"]:
                blocks.append("      <li>")
                blocks.append(f"        <label>{xml_escape(stage['zh_label'])}</label>")
                blocks.append(f"        <description>{xml_escape(stage['zh_desc'])}</description>")
                blocks.append(f"        <baseMoodEffect>{fmt_num(stage['mood'])}</baseMoodEffect>")
                blocks.append("      </li>")
            blocks.append("    </stages>")
            blocks.append("  </ThoughtDef>")
            blocks.append("")
    blocks.append("</Defs>")
    blocks.append("")
    path = ROOT / "Defs/ThoughtDefs/Thoughts_MouseDisaster_Traits.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(blocks), encoding="utf-8", newline="\n")

def emit_lang(language, name_key, desc_key):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    for trait in TRAITS:
        def_name = f"MouseDisaster_Trait_{trait['key']}"
        lines.append(f"  <{def_name}.degreeDatas.0.label>{xml_escape(trait[name_key])}</{def_name}.degreeDatas.0.label>")
        lines.append(f"  <{def_name}.degreeDatas.0.description>{xml_escape(trait[desc_key])}</{def_name}.degreeDatas.0.description>")
    lines.append("</LanguageData>")
    lines.append("")
    path = ROOT / f"Languages/{language}/DefInjected/TraitDef/MouseDisasterTraits.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")

def emit_thought_lang(language, label_key, desc_key):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    for trait in TRAITS:
        for thought in trait.get("thoughts") or []:
            def_name = thought_def_name(trait, thought)
            for index, stage in enumerate(thought["stages"]):
                lines.append(f"  <{def_name}.stages.{index}.label>{xml_escape(stage[label_key])}</{def_name}.stages.{index}.label>")
                lines.append(f"  <{def_name}.stages.{index}.description>{xml_escape(stage[desc_key])}</{def_name}.stages.{index}.description>")
    lines.append("</LanguageData>")
    lines.append("")
    path = ROOT / f"Languages/{language}/DefInjected/ThoughtDef/MouseDisasterTraitThoughts.xml"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")

def format_attrs(trait):
    attrs = []
    for key, value in (trait.get("offsets") or {}).items():
        attrs.append(f"{key} {value:+}")
    for key, value in (trait.get("factors") or {}).items():
        attrs.append(f"{key} x{value}")
    if "hunger" in trait:
        attrs.append(f"hungerRateFactor {trait['hunger']}")
    if "social_fight" in trait:
        attrs.append(f"socialFight x{trait['social_fight']}")
    if "market" in trait:
        attrs.append(f"marketValue {trait['market']:+}")
    if "pain" in trait:
        attrs.append(f"painOffset {trait['pain']:+}")
    for thought in trait.get("thoughts") or []:
        attrs.append(f"thought {thought['suffix']}")
    return ", ".join(attrs) if attrs else "-"

def emit_cache():
    lines = [
        "## Rimworld Trait for 鼠灾 Mod",
        "",
        "Example:",
        "",
        "name: 吃苦",
        "desc: [PAWN_nameDef]吃得了苦，苦活也能一直干下去。",
        "attr: +15% 全局工作速度",
        "color: blue",
        "",
        "### Traits",
        "",
    ]
    for trait in TRAITS:
        lines += [
            f"#### {trait['id']} {trait['zh']}",
            f"id: {trait['id']}",
            f"defName: MouseDisaster_Trait_{trait['key']}",
            f"name: {trait['zh']}",
            f"name_en: {trait['en']}",
            f"desc: {trait['desc_zh']}",
            f"desc_en: {trait['desc_en']}",
            f"attr: {format_attrs(trait)}",
            f"color: {trait['color']}",
            f"polarity: {trait['polarity']}",
            f"slot: {trait['slot']}",
            f"chance: {trait['chance']}",
            f"history: {', '.join(trait['histories']) if trait['histories'] else '-'}",
            f"event: {', '.join(trait['events']) if trait['events'] else '-'}",
            f"category: {trait['category']}",
            "",
        ]
    (ROOT / "pawn_trait.md.cache").write_text("\n".join(lines), encoding="utf-8", newline="\n")

def emit_csharp_data():
    lines = [
        "namespace MouseDisaster",
        "{",
        "    public static partial class MouseDisasterTraitCatalog",
        "    {",
        "        private static readonly MouseDisasterTraitDefinition[] Definitions = new MouseDisasterTraitDefinition[]",
        "        {",
    ]
    for trait in TRAITS:
        lines.append("            new MouseDisasterTraitDefinition(")
        lines.append(f"                \"{trait['id']}\", \"MouseDisaster_Trait_{trait['key']}\",")
        lines.append(f"                MouseDisasterTraitSlot.{trait['slot']}, MouseDisasterTraitPolarity.{trait['polarity']}, {trait['chance']}f,")
        lines.append(f"                {csharp_strings(trait['histories'])},")
        lines.append(f"                {csharp_strings(trait['events'])},")
        lines.append(f"                MouseDisasterPawnHistoryEventCategory.{trait['category']}),")
    lines += [
        "        };",
        "    }",
        "}",
        "",
    ]
    (ROOT / "1.6/Source/MouseDisasterTraitData.cs").write_text("\n".join(lines), encoding="utf-8", newline="\n")

def main():
    validate()
    emit_xml()
    emit_thought_xml()
    emit_lang("ChineseSimplified", "zh", "desc_zh")
    emit_lang("English", "en", "desc_en")
    emit_thought_lang("ChineseSimplified", "zh_label", "zh_desc")
    emit_thought_lang("English", "en_label", "en_desc")
    emit_cache()
    emit_csharp_data()
    print(f"PASS: emitted {len(TRAITS)} traits")

if __name__ == "__main__":
    main()
