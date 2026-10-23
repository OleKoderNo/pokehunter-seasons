using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PokeHunter.Catalogue;

namespace PokeHunter.Tests.Catalogue
{
    internal static class CatalogueIndexChecks
    {
        public static void Run(CatalogueDocument original)
        {
            // Work with a separate fixture so this test cannot change
            // the document used by other checks.
            CatalogueDocument source =
                JsonConvert.DeserializeObject<CatalogueDocument>(
                    JsonConvert.SerializeObject(original)
                );

            var index = new CatalogueIndex(source);

            string entryId = source.Entries[0].Id;
            string groupId = source.Entries[0].EncounterGroupId;

            CatalogueEntry entry = index.FindEntry(entryId);
            CatalogueEncounterGroup group = index.FindGroup(groupId);

            Require(entry != null, "An existing entry was not found.");
            Require(group != null, "An existing group was not found.");

            Require(
                index.EntryCount == source.Entries.Count &&
                index.GroupCount == source.EncounterGroups.Count,
                "Index counts do not match the source."
            );

            string expectedEntry = JsonConvert.SerializeObject(entry);
            string expectedGroup = JsonConvert.SerializeObject(group);
            int expectedEntryCount = index.EntryCount;
            int expectedGroupCount = index.GroupCount;

            Require(
                group.EntryIds.Contains(entryId),
                "The entry was not listed in its group."
            );

            Console.WriteLine(
                "PASS: Catalogue entries and groups were found by ID."
            );

            Require(
                index.FindEntry("missing-test-entry") == null &&
                index.FindGroup("missing-test-group") == null,
                "Unknown IDs should return null."
            );

            Require(
                index.FindEntry(entryId.ToUpperInvariant()) == null &&
                index.FindGroup(groupId.ToUpperInvariant()) == null,
                "Catalogue ID matching must be case-sensitive."
            );

            Console.WriteLine(
                "PASS: Unknown IDs returned null and matching was case-sensitive."
            );

            // Change both scalar and nested fields on returned copies.
            entry.DisplayName = "Changed by test";
            entry.Types.Clear();
            entry.Sprite.Url = "https://example.com/test.png";
            entry.Cries.Latest = "https://example.com/test.ogg";

            group.RarityClass = "changed-by-test";
            group.EntryIds.Clear();

            Require(
                JsonConvert.SerializeObject(index.FindEntry(entryId)) ==
                expectedEntry,
                "Changing a returned entry altered the index."
            );

            Require(
                JsonConvert.SerializeObject(index.FindGroup(groupId)) ==
                expectedGroup,
                "Changing a returned group altered the index."
            );

            Console.WriteLine(
                "PASS: Returned entry and group changes did not alter the index."
            );

            IReadOnlyList<CatalogueEntry> members =
                index.GetGroupEntries(groupId);

            CatalogueEncounterGroup unchangedGroup = index.FindGroup(groupId);

            Require(
                members.Count == unchangedGroup.EntryIds.Count,
                "Group lookup returned an incorrect entry count."
            );

            for (int position = 0; position < members.Count; position++)
            {
                Require(
                    members[position].Id == unchangedGroup.EntryIds[position] &&
                    members[position].EncounterGroupId == groupId,
                    "Group lookup returned an incorrect entry."
                );
            }

            string memberId = members[0].Id;
            string expectedMember =
                JsonConvert.SerializeObject(index.FindEntry(memberId));

            members[0].Types.Clear();

            Require(
                JsonConvert.SerializeObject(index.FindEntry(memberId)) ==
                expectedMember,
                "Changing a group member altered the index."
            );

            Console.WriteLine(
                "PASS: Group members were resolved correctly and independently."
            );

            // The source document must also be independent after indexing.
            source.Entries[0].DisplayName = "Changed source";
            source.Entries[0].Types.Clear();
            source.Entries[0].Sprite.Url = null;
            source.Entries[0].Cries.Latest = null;

            foreach (CatalogueEncounterGroup sourceGroup in source.EncounterGroups)
            {
                sourceGroup.EntryIds.Clear();
            }

            source.Entries.Clear();
            source.EncounterGroups.Clear();

            Require(
                index.EntryCount == expectedEntryCount &&
                index.GroupCount == expectedGroupCount &&
                JsonConvert.SerializeObject(index.FindEntry(entryId)) ==
                expectedEntry &&
                JsonConvert.SerializeObject(index.FindGroup(groupId)) ==
                expectedGroup,
                "Changing the source document altered the index."
            );

            Console.WriteLine(
                "PASS: Source-document changes did not alter the index."
            );

            bool unknownGroupRejected = false;

            try
            {
                index.GetGroupEntries("missing-test-group");
            }
            catch (KeyNotFoundException)
            {
                unknownGroupRejected = true;
            }

            Require(
                unknownGroupRejected,
                "Unknown group membership requests must be rejected."
            );

            bool invalidIdRejected = false;

            try
            {
                index.FindEntry(" ");
            }
            catch (ArgumentException)
            {
                invalidIdRejected = true;
            }

            Require(
                invalidIdRejected,
                "Blank lookup IDs must be rejected."
            );

            Console.WriteLine(
                "PASS: Unknown group membership requests and blank IDs were rejected."
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}