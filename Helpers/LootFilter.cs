﻿/**
 *   Copyright (C) 2021 okaygo
 *   
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using System.Linq;
using System.Collections.Generic;
using MapAssist.Types;
using MapAssist.Settings;

namespace MapAssist.Helpers
{
    public static class LootFilter
    {
        public static Dictionary<string, Stat> FilterOptionStats = new Dictionary<string, Stat>()
        {
            ["AllSkills"] = Stat.STAT_ITEM_ALLSKILLS,
            ["Strength"] = Stat.STAT_STRENGTH,
            ["Dexterity"] = Stat.STAT_DEXTERITY,
            ["Vitality"] = Stat.STAT_VITALITY,
            ["Energy"] = Stat.STAT_ENERGY,
            ["AttackRating"] = Stat.STAT_TOHIT,
            ["MinDamage"] = Stat.STAT_MINDAMAGE,
            ["MaxDamage"] = Stat.STAT_MAXDAMAGE,
            ["DamageReduced"] = Stat.STAT_DAMAGERESIST,
            ["LifeSteal"] = Stat.STAT_LIFEDRAINMINDAM,
            ["ManaSteal"] = Stat.STAT_MANADRAINMINDAM,
            ["ColdSkillDamage"] = Stat.STAT_PASSIVE_COLD_MASTERY,
            ["LightningSkillDamage"] = Stat.STAT_PASSIVE_LTNG_MASTERY,
            ["FireSkillDamage"] = Stat.STAT_PASSIVE_FIRE_MASTERY,
            ["PoisonSkillDamage"] = Stat.STAT_PASSIVE_POIS_MASTERY,
            ["IncreasedAttackSpeed"] = Stat.STAT_ITEM_FASTERATTACKRATE,
            ["FasterRunWalk"] = Stat.STAT_ITEM_FASTERMOVEVELOCITY,
            ["FasterHitRecovery"] = Stat.STAT_ITEM_FASTERGETHITRATE,
            ["FasterCastRate"] = Stat.STAT_ITEM_FASTERCASTRATE,
            ["MagicFind"] = Stat.STAT_ITEM_MAGICBONUS,
            ["GoldFind"] = Stat.STAT_ITEM_GOLDBONUS,
            ["ColdResist"] = Stat.STAT_COLDRESIST,
            ["LightningResist"] = Stat.STAT_LIGHTRESIST,
            ["FireResist"] = Stat.STAT_FIRERESIST,
            ["PoisonResist"] = Stat.STAT_POISONRESIST,
        };

        public static Dictionary<string, (Stat, int)> FilterOptionShifted = new Dictionary<string, (Stat, int)>()
        {
            ["MaxLife"] = (Stat.STAT_MAXHP, 8),
            ["MaxMana"] = (Stat.STAT_MAXMANA, 8),
        };

        public static (bool, ItemFilter) Filter(UnitAny unitAny)
        {
            //skip low quality items
            var lowQuality = (unitAny.ItemData.ItemFlags & ItemFlags.IFLAG_LOWQUALITY) == ItemFlags.IFLAG_LOWQUALITY;
            if (lowQuality)
            {
                return (false, null);
            }

            var baseName = Items.ItemName(unitAny.TxtFileNo);
            //populate a list of filter rules by combining rules from "Any" and the item base name
            //use only one list or the other depending on if "Any" exists
            var matches =
                LootLogConfiguration.Filters
                    .Where(f => f.Key == "Any" || f.Key == baseName).ToList();

            // Early breakout
            // We know that there is an item in here without any actual filters
            // So we know that simply having the name match means we can return true
            if (matches.Any(kv => kv.Value == null))
            {
                return (true, null);
            }

            //get other item stats to use for filtering
            var itemQuality = unitAny.ItemData.ItemQuality;
            var isEth = (unitAny.ItemData.ItemFlags & ItemFlags.IFLAG_ETHEREAL) == ItemFlags.IFLAG_ETHEREAL;
            var numSockets = unitAny.Stats.TryGetValue(Stat.STAT_ITEM_NUMSOCKETS, out var socketCount) ? socketCount : 0;

            //scan the list of rules
            foreach (var rule in matches.SelectMany(kv => kv.Value))
            {
                var testing = Items.GetStatAddSkillCharges(unitAny, Skill.Hydra);

                var qualityReqMet = rule.Qualities == null || rule.Qualities.Length == 0 || rule.Qualities.Contains(itemQuality);
                if (!qualityReqMet) { continue; }

                var socketReqMet = rule.Sockets == null || rule.Sockets.Length == 0 || rule.Sockets.Contains(numSockets);
                if (!socketReqMet) { continue; }

                var ethReqMet = (rule.Ethereal == null || rule.Ethereal == isEth);
                if (!ethReqMet) { continue; }

                var allAttrReqMet = rule.AllAttributes == null || rule.AllAttributes == 0 || Items.GetStatAllAttributes(unitAny) >= rule.AllAttributes;
                if (!allAttrReqMet) { continue; }

                var allResReqMet = rule.AllResist == null || rule.AllResist == 0 || Items.GetStatAllResist(unitAny) >= rule.AllResist;
                if (!allResReqMet) { continue; }

                // Item class skills
                var addClassSkillsReqMet = (rule.ClassSkills.Count == 0);
                foreach (var subrule in rule.ClassSkills)
                {
                    addClassSkillsReqMet = (subrule.Value == null || subrule.Value == 0 || Items.GetStatAddClassSkills(unitAny, subrule.Key) >= subrule.Value);
                    if (!addClassSkillsReqMet) { continue; }
                }
                if (!addClassSkillsReqMet) { continue; }

                // Item class tab skills
                var addClassTabSkillsReqMet = (rule.ClassTabSkills.Count == 0);
                foreach (var subrule in rule.ClassTabSkills)
                {
                    addClassTabSkillsReqMet = (subrule.Value == null || subrule.Value == 0 || Items.GetStatAddClassTabSkills(unitAny, subrule.Key) >= subrule.Value);
                    if (!addClassTabSkillsReqMet) { continue; }
                }
                if (!addClassTabSkillsReqMet) { continue; }

                // Item skill charges
                var chargedSkillsReqMet = (rule.SkillCharges.Count == 0);
                foreach (var subrule in rule.SkillCharges)
                {
                    chargedSkillsReqMet = (subrule.Value == null || subrule.Value == 0 || Items.GetStatAddSkillCharges(unitAny, subrule.Key) >= subrule.Value);
                    if (!chargedSkillsReqMet) { continue; }
                }
                if (!chargedSkillsReqMet) { continue; }

                // Item single skills
                var singleSkillsReqMet = (rule.Skills.Count == 0);
                foreach (var subrule in rule.Skills)
                {
                    singleSkillsReqMet = (subrule.Value == null || subrule.Value == 0 || Items.GetStatSingleSkills(unitAny, subrule.Key) >= subrule.Value);
                    if (!singleSkillsReqMet) { continue; }
                }
                if (!singleSkillsReqMet) { continue; }

                // Shifted item stats
                var shiftedStatReqMet = true;
                foreach (var (prop, (stat, shift)) in FilterOptionShifted.Select(x => (x.Key, x.Value)))
                {
                    shiftedStatReqMet = rule[prop] == null || (int)rule[prop] == 0 || Items.GetItemStatShifted(unitAny, stat, shift) >= (int)rule[prop];
                    if (!shiftedStatReqMet) { continue; }
                }
                if (!shiftedStatReqMet) { continue; }

                // Other item stats
                var otherStatReqMet = true;
                foreach (var (prop, stat) in FilterOptionStats.Select(x => (x.Key, x.Value)))
                {
                    otherStatReqMet = rule[prop] == null || (int)rule[prop] == 0 || Items.GetItemStat(unitAny, stat) >= (int)rule[prop];
                    if (!otherStatReqMet) { continue; }
                }
                if (!otherStatReqMet) { continue; }

                // Item meets all filter requirements
                return (true, rule);
            }

            return (false, null);
        }
    }
}
