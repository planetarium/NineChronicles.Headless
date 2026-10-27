using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bencodex.Types;
using Libplanet.Action.State;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Module;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    /// <summary>
    /// Read-only judgement of hard world openings. Everything is delegated to lib9c; with draft CSVs
    /// the sheets are laid over the given state in the same order the patch action uses.
    /// </summary>
    public static class WorldOpenEvaluator
    {
        /// <summary>Largest accepted draft CSV, in UTF-8 bytes, per sheet.</summary>
        public const int MaxDraftCsvBytes = 64 * 1024;

        public static WorldOpenResult Evaluate(
            IWorldState worldState,
            long tipBlockIndex,
            string? worldOpenSheetCsv,
            string? worldOpenConditionSheetCsv,
            string? globalCounterSheetCsv)
        {
            // The earliest block the next action can land in; same as WorldOpenGate's contract.
            var block = tipBlockIndex + 1;
            var result = new WorldOpenResult { EvaluatedBlockIndex = block };

            IWorld world = new Libplanet.Action.State.World(worldState);
            world = Overlay<GlobalCounterSheet>(world, globalCounterSheetCsv, block, result.Errors);
            world = Overlay<WorldOpenSheet>(world, worldOpenSheetCsv, block, result.Errors);
            world = Overlay<WorldOpenConditionSheet>(world, worldOpenConditionSheetCsv, block, result.Errors);

            if (world.TryGetPatchedSheet<WorldSheet>(out var worldSheet))
            {
                // Mimisbrunnr is never gated, so it carries no information here.
                result.Worlds.AddRange(
                    (worldSheet.OrderedList ?? Enumerable.Empty<WorldSheet.Row>())
                    .Where(r => r.Id != GameConfig.MimisbrunnrWorldId
                        && r.StageEnd >= GameConfig.ProgressGatedStageBegin)
                    .Select(r => new WorldOpenWorldInfo
                    {
                        WorldId = r.Id,
                        StageBegin = r.StageBegin,
                        StageEnd = r.StageEnd,
                        OpenedStageEnd = WorldOpenGate.GetOpenedStageEnd(world, r, block),
                    }));
            }

            if (world.TryGetPatchedSheet<WorldOpenSheet>(out var opens) && opens.OrderedList is { } rows)
            {
                world.TryGetPatchedSheet<GlobalCounterSheet>(out var counters);
                world.TryGetPatchedSheet<WorldOpenConditionSheet>(out var conditions);
                foreach (var row in rows.Where(r => r.IsValid))
                {
                    result.Opens.Add(ToEntry(world, opens, counters, conditions, row, block));
                }
            }

            return result;
        }

        // Same order as the patch action: validate, check identity, store. A rejected sheet is not
        // laid over, so the judgement keeps using the live one.
        private static IWorld Overlay<T>(IWorld world, string? csv, long block, List<string> errors)
            where T : ISheet, new()
        {
            if (csv is null)
            {
                return world;
            }

            var name = typeof(T).Name;
            var size = Encoding.UTF8.GetByteCount(csv);
            if (size > MaxDraftCsvBytes)
            {
                errors.Add($"{name}: draft is too large ({size} bytes, max {MaxDraftCsvBytes})");
                return world;
            }

            try
            {
                TableSheetPatchValidation.Validate(name, csv);
                var recorded = WorldOpenIdentity.CheckAndRecord(world, name, csv);
                var stored = recorded.SetLegacyState(Addresses.GetSheetAddress<T>(), (Text)csv);

                // The patch action also confirms openings the new sheet already satisfies.
                return WorldOpenCounter.ConfirmSatisfiedAfterPatch(stored, name, block);
            }
            catch (SheetRowValidateException e)
            {
                errors.Add(e.Message.StartsWith($"{name}:") ? e.Message : $"{name}: {e.Message}");
                return world;
            }
        }

        private static WorldOpenEntryInfo ToEntry(
            IWorldState state,
            WorldOpenSheet opens,
            GlobalCounterSheet? counters,
            WorldOpenConditionSheet? conditions,
            WorldOpenSheet.Row row,
            long block)
        {
            var entry = new WorldOpenEntryInfo
            {
                Id = row.Id,
                WorldId = row.WorldId,
                StageEnd = row.StageEnd,
                PrerequisiteOpenId = row.PrerequisiteOpenId,
                MinBlockIndex = row.MinBlockIndex,
                FallbackBlockIndex = row.FallbackBlockIndex,
                EffectiveDelayBlock = row.EffectiveDelayBlock,
                Applied = state.IsApplied(opens, row, block),
                Pending = state.IsPending(row, block),
            };
            if (state.TryGetConfirmedBlock(row.Id, out var confirmed))
            {
                entry.ConfirmedBlockIndex = confirmed;
            }

            if (state.TryGetAppliedBlock(opens, row, out var appliedBlock))
            {
                entry.AppliedBlockIndex = appliedBlock;
            }

            if (conditions is not null)
            {
                entry.ConditionGroups.AddRange(conditions.GroupsOf(row.Id).Select(group =>
                {
                    var items = group.Select(c =>
                    {
                        GlobalCounterSheet.Row? counter = null;
                        counters?.TryGetValue(c.CounterId, out counter);
                        return new WorldOpenConditionInfo
                        {
                            CounterId = c.CounterId,
                            Type = counter?.Type.ToString(),
                            TargetBegin = counter?.TargetBegin,
                            TargetEnd = counter?.TargetEnd,
                            MinAvatarLevel = counter?.MinAvatarLevel,
                            Current = state.GetCounter(c.CounterId),
                            Threshold = c.Threshold,
                        };
                    }).ToList();
                    return new WorldOpenConditionGroupInfo
                    {
                        // Same rule as WorldOpenCounter.IsSatisfied, per group.
                        Satisfied = items.All(i => i.Current >= i.Threshold),
                        Conditions = items,
                    };
                }));
            }

            return entry;
        }
    }
}
